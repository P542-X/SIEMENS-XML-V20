using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace ExportarXml
{
    /// <summary>
    /// HMI clásico (Basic/Comfort/Advanced): exportar e importar en XML con Openness.
    /// Se accede por reflexión (nombres de propiedades de Siemens.Engineering.Hmi) para que un cambio
    /// de API o un tipo de panel distinto no rompa el Add-In: lo que no exista se apunta y se sigue.
    /// WinCC Unified (HmiSoftware) no se exporta aún: no tiene XML de pantallas en Openness.
    /// </summary>
    internal static class Hmi
    {
        // Qué se exporta: (carpeta de salida, propiedad del HmiTarget, propiedad de la colección, ¿tiene subcarpetas?)
        private static readonly (string Carpeta, string Raiz, string Coleccion, bool Carpetas)[] Partes =
        {
            ("Pantallas",        "ScreenFolder",         "Screens",          true),
            ("Plantillas",       "ScreenTemplateFolder", "ScreenTemplates",  true),
            ("Emergentes",       "ScreenPopupFolder",    "ScreenPopups",     true),
            ("Deslizantes",      "ScreenSlideinFolder",  "ScreenSlideins",   false),
            ("Variables",        "TagFolder",            "TagTables",        true),
            ("Scripts",          "VBScriptFolder",       "VBScripts",        true),
            ("Listas_texto",     null,                   "TextLists",        false),
            ("Listas_graficos",  null,                   "GraphicLists",     false),
            ("Conexiones",       null,                   "Connections",      false),
            ("Ciclos",           null,                   "Cycles",           false),
        };

        // Al importar: raíz del XML -> dónde va (mismo orden de dependencias que al exportar, al revés de la lista)
        internal static readonly (string RaizXml, string Raiz, string Coleccion, int Orden)[] Destinos =
        {
            ("Hmi.Communication.Connection",      null,                   "Connections",     0),
            ("Hmi.Cycle.Cycle",                   null,                   "Cycles",          0),
            ("Hmi.TextGraphicList.TextList",      null,                   "TextLists",       1),
            ("Hmi.TextGraphicList.GraphicList",   null,                   "GraphicLists",    1),
            ("Hmi.Tag.TagTable",                  "TagFolder",            "TagTables",       2),
            ("Hmi.RuntimeScripting.VBScript",     "VBScriptFolder",       "VBScripts",       3),
            ("Hmi.Screen.ScreenTemplate",         "ScreenTemplateFolder", "ScreenTemplates", 4),
            ("Hmi.Screen.ScreenPopup",            "ScreenPopupFolder",    "ScreenPopups",    5),
            ("Hmi.Screen.ScreenSlidein",          "ScreenSlideinFolder",  "ScreenSlideins",  5),
            ("Hmi.Screen.Screen",                 "ScreenFolder",         "Screens",         6),
        };

        // ---------- Localizar el HMI ----------

        /// <summary>El HmiTarget (HMI clásico) del objeto seleccionado, o null.</summary>
        public static object ObtenerHmi(IEngineeringObject o)
        {
            object sw = Software(o);
            return sw != null && sw.GetType().Name == "HmiTarget" ? sw : null;
        }

        /// <summary>true si el objeto es (o contiene) un HMI WinCC Unified.</summary>
        public static bool EsUnified(IEngineeringObject o)
        {
            object sw = Software(o);
            return sw != null && sw.GetType().Name == "HmiSoftware";
        }

        private static object Software(IEngineeringObject o)
        {
            try
            {
                if (o == null) return null;
                string tipo = o.GetType().Name;
                if (tipo == "HmiTarget" || tipo == "HmiSoftware") return o;
                if (o is Device d)
                {
                    foreach (DeviceItem di in d.DeviceItems)
                    {
                        object s = Software(di);
                        if (s != null) return s;
                    }
                    return null;
                }
                if (o is DeviceItem item)
                {
                    SoftwareContainer c = item.GetService<SoftwareContainer>();
                    if (c?.Software != null && (c.Software.GetType().Name == "HmiTarget" || c.Software.GetType().Name == "HmiSoftware"))
                        return c.Software;
                    foreach (DeviceItem hijo in item.DeviceItems)
                    {
                        object s = Software(hijo);
                        if (s != null) return s;
                    }
                    return null;
                }
                for (IEngineeringObject x = o.Parent; x != null; x = x.Parent)
                {
                    string t = x.GetType().Name;
                    if (t == "HmiTarget" || t == "HmiSoftware") return x;
                }
            }
            catch { }
            return null;
        }

        /// <summary>Nombre, modelo y referencia del panel (sube hasta el DeviceItem).</summary>
        public static string Panel(object hmi)
        {
            string nombre = Texto(hmi, "Name");
            try
            {
                for (IEngineeringObject x = (hmi as IEngineeringObject)?.Parent; x != null; x = x.Parent)
                {
                    if (!(x is DeviceItem di)) continue;
                    string modelo = "";
                    try { modelo = Convert.ToString(di.GetAttribute("TypeName")); } catch { }
                    string tipo = di.TypeIdentifier ?? "";
                    if (tipo.Length > 0) return nombre + ": " + (modelo.Length > 0 ? modelo + " · " : "") + tipo.Replace("OrderNumber:", "");
                }
            }
            catch { }
            return nombre;
        }

        // ---------- Exportar ----------

        public static void Exportar(object hmi, string carpeta, ExclusiveAccess ea, StringBuilder log, Dictionary<string, int> recuento, ref int ok, ref int errores)
        {
            string raiz = Path.Combine(carpeta, Limpio(Texto(hmi, "Name")));
            foreach (var p in Partes)
            {
                if (ea.IsCancellationRequested) return;
                object origen = p.Raiz == null ? hmi : Prop(hmi, p.Raiz);
                if (origen == null)
                {
                    log.AppendLine("       (" + p.Carpeta + ": este panel no tiene " + (p.Raiz ?? p.Coleccion) + ")");
                    continue;
                }
                int n = 0;
                ExportarCarpeta(origen, p.Coleccion, p.Carpetas, Path.Combine(raiz, p.Carpeta), ea, log, ref n, ref errores);
                ok += n;
                if (n > 0) recuento[p.Carpeta] = (recuento.TryGetValue(p.Carpeta, out int k) ? k : 0) + n;
            }
        }

        private static void ExportarCarpeta(object carpetaTia, string coleccion, bool subcarpetas, string destino, ExclusiveAccess ea,
                                            StringBuilder log, ref int ok, ref int errores)
        {
            foreach (object o in Items(Prop(carpetaTia, coleccion)))
            {
                if (ea.IsCancellationRequested) return;
                string nombre = Texto(o, "Name");
                try
                {
                    ea.Text = "Exportando HMI " + nombre + "...";
                    Directory.CreateDirectory(destino);
                    FileInfo f = new FileInfo(Path.Combine(destino, Limpio(nombre) + ".xml"));
                    if (f.Exists) f.Delete();
                    Llamar(o, "Export", f, ExportOptions.WithDefaults);
                    ok++;
                    log.AppendLine("OK     HMI    " + f.FullName);
                }
                catch (Exception ex)
                {
                    errores++;
                    log.AppendLine("ERROR  HMI    " + nombre + " -> " + Mensaje(ex));
                }
            }
            if (!subcarpetas) return;
            foreach (object sub in Items(Prop(carpetaTia, "Folders")))
                ExportarCarpeta(sub, coleccion, true, Path.Combine(destino, Limpio(Texto(sub, "Name"))), ea, log, ref ok, ref errores);
        }

        // ---------- Importar ----------

        public static void Importar(TiaPortal tia, object hmi, IEnumerable<string> rutas)
        {
            Project proyecto = tia.Projects.FirstOrDefault();
            if (proyecto == null) return;
            StringBuilder log = new StringBuilder();
            log.AppendLine("HMI destino: " + Panel(hmi));
            log.AppendLine(Idiomas.Preparar(tia));
            log.AppendLine();

            // Ficheros HMI (raíz Hmi.*); el resto se ignora (se importa sobre el PLC)
            List<(FileInfo F, string Nombre, (string RaizXml, string Raiz, string Coleccion, int Orden) D)> elementos = new List<(FileInfo, string, (string, string, string, int))>();
            int ignorados = 0;
            foreach (string ruta in Importador.AbrirZipsPublico(tia, rutas, log))
            {
                if (!ruta.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) { ignorados++; continue; }
                try
                {
                    XElement raizXml = XDocument.Load(ruta).Root;
                    XElement obj = raizXml?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("Hmi."));
                    if (obj == null) { ignorados++; continue; }
                    var d = Destinos.FirstOrDefault(x => x.RaizXml == obj.Name.LocalName);
                    if (d.RaizXml == null)
                    {
                        log.AppendLine("IGNORADO " + ruta + " (" + obj.Name.LocalName + ": tipo HMI aún no soportado)");
                        continue;
                    }
                    string nombre = obj.Descendants().FirstOrDefault(e => e.Name.LocalName == "Name" && e.Parent?.Name.LocalName == "AttributeList")?.Value
                                    ?? Path.GetFileNameWithoutExtension(ruta);
                    elementos.Add((new FileInfo(ruta), nombre, d));
                }
                catch (Exception ex)
                {
                    log.AppendLine("IGNORADO " + ruta + " (no se puede leer: " + Mensaje(ex) + ")");
                }
            }
            if (elementos.Count == 0)
            {
                Dialogos.Informar("No hay ficheros de HMI clásico (pantallas, variables HMI, listas, scripts…) en lo elegido." +
                                  (ignorados > 0 ? Environment.NewLine + ignorados + " fichero(s) de PLC u otros ignorados: impórtalos sobre el PLC." : ""),
                                  "Importar HMI", true);
                return;
            }

            // ¿Cuáles existen?
            var existentes = elementos.Where(e => Buscar(hmi, e.D, e.Nombre) != null).ToList();
            bool sobrescribir = false;
            if (existentes.Count > 0)
            {
                Respuesta r = Dialogos.Preguntar(existentes.Count + " objeto(s) HMI ya existen (" +
                                                 string.Join(", ", existentes.Take(6).Select(e => e.Nombre)) + (existentes.Count > 6 ? ", …" : "") + ")." +
                                                 Environment.NewLine + Environment.NewLine + "Sí / Yes → sobrescribir" + Environment.NewLine +
                                                 "No → importar solo los nuevos" + Environment.NewLine + "Cancelar → no importar nada", "Importar HMI");
                if (r == Respuesta.Cancelar) return;
                sobrescribir = r == Respuesta.Si;
            }
            var pendientes = elementos.Where(e => sobrescribir || !existentes.Contains(e)).OrderBy(e => e.D.Orden).ThenBy(e => e.Nombre).ToList();

            string copia = Path.Combine(proyecto.Path.DirectoryName, "Import_XML", "copia_hmi_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            int importados = 0;
            Dictionary<string, string> ultimoError = new Dictionary<string, string>();
            using (ExclusiveAccess ea = tia.ExclusiveAccess("Importando HMI..."))
            {
                try { proyecto.Save(); log.AppendLine("Proyecto guardado antes de importar."); } catch { }

                // Copia de lo que se va a sobrescribir
                foreach (var e in pendientes)
                {
                    object actual = Buscar(hmi, e.D, e.Nombre);
                    if (actual == null) continue;
                    try
                    {
                        Directory.CreateDirectory(copia);
                        Llamar(actual, "Export", new FileInfo(Path.Combine(copia, Limpio(e.Nombre) + ".xml")), ExportOptions.WithDefaults);
                    }
                    catch (Exception ex) { log.AppendLine("Aviso: sin copia de " + e.Nombre + " (" + Mensaje(ex) + ")"); }
                }
                if (Directory.Exists(copia)) log.AppendLine("Copia de lo sobrescrito en " + copia);
                log.AppendLine();

                bool progreso = true;
                while (pendientes.Count > 0 && progreso && !ea.IsCancellationRequested)
                {
                    progreso = false;
                    foreach (var e in pendientes.ToList())
                    {
                        ea.Text = "Importando HMI " + e.Nombre + "...";
                        try
                        {
                            object destino = e.D.Raiz == null ? hmi : Prop(hmi, e.D.Raiz);
                            object coleccion = Prop(destino, e.D.Coleccion);
                            if (coleccion == null) throw new Exception("el panel no tiene " + (e.D.Raiz ?? "") + "." + e.D.Coleccion);
                            FileInfo f = Idiomas.Adaptar(e.F, out string nota);
                            Llamar(coleccion, "Import", f, sobrescribir ? ImportOptions.Override : ImportOptions.None);
                            pendientes.Remove(e);
                            importados++;
                            progreso = true;
                            log.AppendLine("OK       " + e.Nombre + "  <-  " + e.F.FullName);
                            if (nota != null) log.AppendLine("         " + nota);
                        }
                        catch (Exception ex) { ultimoError[e.F.FullName] = Mensaje(ex); }
                    }
                }
                foreach (var e in pendientes)
                    log.AppendLine("ERROR    " + e.Nombre + " -> " + (ultimoError.TryGetValue(e.F.FullName, out string m) ? m : "cancelado"));

                // Compilar el HMI
                string compilacion = "no realizada";
                if (importados > 0)
                {
                    try
                    {
                        ea.Text = "Compilando HMI...";
                        ICompilable c = (hmi as IEngineeringServiceProvider)?.GetService<ICompilable>();
                        if (c != null)
                        {
                            CompilerResult r = c.Compile();
                            compilacion = r.State + " (errores: " + r.ErrorCount + ", avisos: " + r.WarningCount + ")";
                            foreach (CompilerResultMessage msg in r.Messages) Mensajes(msg, log, 1);
                        }
                    }
                    catch (Exception ex) { compilacion = "error al compilar: " + Mensaje(ex); }
                }

                string resumen = "Importados: " + importados + "   Errores: " + pendientes.Count + "   Ignorados (no HMI): " + ignorados + Environment.NewLine +
                                 "Compilación HMI: " + compilacion + Environment.NewLine +
                                 "Deshacer: aún no hay menú para el HMI; la copia de lo sobrescrito está en Import_XML\\copia_hmi_<fecha>";
                string dir = Path.Combine(proyecto.Path.DirectoryName, "Import_XML");
                Directory.CreateDirectory(dir);
                string ruta = Path.Combine(dir, "resumen_hmi_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(ruta, resumen + Environment.NewLine + Environment.NewLine + log, Encoding.UTF8);
                Dialogos.Informar(resumen + Environment.NewLine + Environment.NewLine + "Detalle en:" + Environment.NewLine + ruta, "Importar HMI", pendientes.Count > 0);
            }
        }

        private static void Mensajes(CompilerResultMessage m, StringBuilder log, int nivel)
        {
            if (m.State == CompilerResultState.Error || m.State == CompilerResultState.Warning)
                if (!string.IsNullOrEmpty(m.Description)) log.AppendLine(new string(' ', nivel * 3) + m.Path + ": " + m.Description);
            if (m.Messages != null) foreach (CompilerResultMessage h in m.Messages) Mensajes(h, log, nivel + 1);
        }

        private static object Buscar(object hmi, (string RaizXml, string Raiz, string Coleccion, int Orden) d, string nombre)
        {
            try
            {
                object destino = d.Raiz == null ? hmi : Prop(hmi, d.Raiz);
                return Items(Prop(destino, d.Coleccion)).FirstOrDefault(o => string.Equals(Texto(o, "Name"), nombre, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }

        // ---------- Reflexión ----------

        private static object Prop(object o, string nombre)
        {
            if (o == null || nombre == null) return null;
            try { return o.GetType().GetProperty(nombre, BindingFlags.Public | BindingFlags.Instance)?.GetValue(o, null); }
            catch { return null; }
        }

        private static IEnumerable<object> Items(object coleccion)
        {
            return coleccion is IEnumerable e ? e.Cast<object>().ToList() : new List<object>();
        }

        private static string Texto(object o, string nombre) => Convert.ToString(Prop(o, nombre)) ?? "";

        private static object Llamar(object o, string metodo, params object[] args)
        {
            MethodInfo m = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(x => x.Name == metodo && x.GetParameters().Length == args.Length &&
                                                 x.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(args[i])).All(b => b));
            if (m == null) throw new Exception(o.GetType().Name + " no tiene " + metodo + "(" + string.Join(", ", args.Select(a => a.GetType().Name)) + ")");
            try { return m.Invoke(o, args); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        private static string Mensaje(Exception ex) => (ex.Message ?? "").Replace(Environment.NewLine, " ");

        private static string Limpio(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Length == 0 ? "_" : n;
        }
    }
}
