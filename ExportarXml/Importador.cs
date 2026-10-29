using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

namespace ExportarXml
{
    /// <summary>
    /// Importa XML (SimaticML) en el PLC del objeto seleccionado.
    /// - Detecta solo qué es cada fichero (UDT, tabla de variables, FB, FC, OB, DB).
    /// - Orden: UDT > tablas > FB/FC > OB/DB global > DB de instancia, con varias
    ///   pasadas por si unos dependen de otros.
    /// - Si el objeto ya existe, pregunta si sobrescribir o saltar; al sobrescribir
    ///   lo deja en la carpeta donde ya estaba.
    /// - Al final compila el software del PLC y deja un resumen.
    /// </summary>
    internal static class Importador
    {
        private const string Titulo = "Importar XML";

        private enum Tipo { Udt, TablaVariables, Bloque }

        private sealed class Elemento
        {
            public FileInfo Fichero;
            public string Nombre;
            public Tipo Tipo;
            public int Prioridad;
            public bool Existe;
            public string UltimoError;
        }

        // ---------- Destino de la importación ----------

        public static bool EsDestino(IEngineeringObject o)
        {
            return ObtenerPlc(o) != null;
        }

        private static PlcSoftware ObtenerPlc(IEngineeringObject o)
        {
            if (o is Device dispositivo)
            {
                foreach (DeviceItem di in dispositivo.DeviceItems)
                {
                    PlcSoftware p = ObtenerPlc(di);
                    if (p != null) return p;
                }
                return null;
            }

            if (o is DeviceItem item)
            {
                SoftwareContainer contenedor = item.GetService<SoftwareContainer>();
                if (contenedor?.Software is PlcSoftware sw) return sw;
                foreach (DeviceItem hijo in item.DeviceItems)
                {
                    PlcSoftware p = ObtenerPlc(hijo);
                    if (p != null) return p;
                }
                return null;
            }

            for (IEngineeringObject x = o; x != null; x = x.Parent)
            {
                if (x is PlcSoftware plc) return plc;
            }
            return null;
        }

        private static T Ancestro<T>(IEngineeringObject o) where T : class
        {
            for (IEngineeringObject x = o; x != null; x = x.Parent)
            {
                if (x is T encontrado) return encontrado;
            }
            return null;
        }

        // ---------- Proceso principal ----------

        public static void Ejecutar(TiaPortal tia, IEngineeringObject destino, IEnumerable<string> rutas)
        {
            PlcSoftware plc = ObtenerPlc(destino);
            if (plc == null)
            {
                Dialogos.Informar("No se ha encontrado el PLC de destino.", Titulo, true);
                return;
            }

            StringBuilder log = new StringBuilder();
            int ignorados = 0;
            List<Elemento> elementos = new List<Elemento>();
            foreach (string ruta in rutas)
            {
                Elemento e = Leer(ruta, log);
                if (e != null) elementos.Add(e);
                else ignorados++;
            }

            if (elementos.Count == 0)
            {
                Dialogos.Informar("Ninguno de los ficheros es un XML de TIA (SimaticML) que se pueda importar.", Titulo, true);
                return;
            }

            // ¿Cuáles existen ya en el PLC?
            foreach (Elemento e in elementos) e.Existe = Existe(plc, e);
            List<Elemento> existentes = elementos.Where(e => e.Existe).ToList();

            bool sobrescribir = false;
            if (existentes.Count > 0)
            {
                string ejemplos = string.Join(", ", existentes.Take(6).Select(e => e.Nombre))
                                  + (existentes.Count > 6 ? ", ..." : "");
                Respuesta r = Dialogos.Preguntar(
                    "Ya existen " + existentes.Count + " de los " + elementos.Count + " objetos en \"" + plc.Name + "\":" +
                    Environment.NewLine + ejemplos + Environment.NewLine + Environment.NewLine +
                    "Sí / Yes  →  sobrescribirlos" + Environment.NewLine +
                    "No  →  saltarlos e importar solo los nuevos" + Environment.NewLine +
                    "Cancelar / Cancel  →  no importar nada",
                    Titulo);
                if (r == Respuesta.Cancelar) return;
                sobrescribir = r == Respuesta.Si;
            }

            int saltados = 0;
            foreach (Elemento e in existentes.Where(e => !sobrescribir))
            {
                saltados++;
                log.AppendLine("SALTADO  " + e.Nombre + " (ya existe)");
            }

            List<Elemento> pendientes = elementos
                .Where(e => sobrescribir || !e.Existe)
                .OrderBy(e => e.Prioridad)
                .ThenBy(e => e.Nombre)
                .ToList();

            int importados = 0;
            string compilacion = "no realizada";

            using (ExclusiveAccess ea = tia.ExclusiveAccess("Importando XML..."))
            {
                // Varias pasadas: si algo falla por depender de otro objeto, se reintenta
                bool progreso = true;
                while (pendientes.Count > 0 && progreso && !ea.IsCancellationRequested)
                {
                    progreso = false;
                    foreach (Elemento e in pendientes.ToList())
                    {
                        if (ea.IsCancellationRequested) break;
                        ea.Text = "Importando " + e.Nombre + "...";
                        try
                        {
                            Importar(plc, destino, e, sobrescribir);
                            pendientes.Remove(e);
                            importados++;
                            progreso = true;
                            log.AppendLine("OK       " + e.Nombre + "  <-  " + e.Fichero.FullName);
                        }
                        catch (Exception ex)
                        {
                            e.UltimoError = ex.Message;
                        }
                    }
                }

                foreach (Elemento e in pendientes)
                    log.AppendLine("ERROR    " + e.Nombre + " -> " + (e.UltimoError ?? "cancelado"));

                if (importados > 0 && !ea.IsCancellationRequested)
                {
                    ea.Text = "Compilando " + plc.Name + "...";
                    compilacion = Compilar(plc, log);
                }
            }

            string resumen =
                "Importados: " + importados + "   Saltados: " + saltados +
                "   Errores: " + pendientes.Count + "   No válidos: " + ignorados + Environment.NewLine +
                "Compilación: " + compilacion;

            string rutaResumen = GuardarResumen(tia, resumen, log);

            Dialogos.Informar(
                resumen + Environment.NewLine + Environment.NewLine +
                (rutaResumen != null ? "Detalle en:" + Environment.NewLine + rutaResumen : ""),
                Titulo,
                pendientes.Count > 0 || compilacion.StartsWith("Error"));
        }

        // ---------- Lectura y clasificación del XML ----------

        private static Elemento Leer(string ruta, StringBuilder log)
        {
            try
            {
                XElement raiz = XDocument.Load(ruta).Root;
                if (raiz == null || raiz.Name.LocalName != "Document")
                {
                    log.AppendLine("IGNORADO " + ruta + " (no es SimaticML)");
                    return null;
                }

                XElement obj = raiz.Elements().FirstOrDefault(x => x.Name.LocalName.StartsWith("SW."));
                if (obj == null)
                {
                    log.AppendLine("IGNORADO " + ruta + " (no contiene ningún objeto de TIA)");
                    return null;
                }

                string nombre = obj.Elements().FirstOrDefault(x => x.Name.LocalName == "AttributeList")?
                                   .Elements().FirstOrDefault(x => x.Name.LocalName == "Name")?.Value;
                if (string.IsNullOrEmpty(nombre)) nombre = Path.GetFileNameWithoutExtension(ruta);

                Elemento e = new Elemento { Fichero = new FileInfo(ruta), Nombre = nombre };
                switch (obj.Name.LocalName)
                {
                    case "SW.Types.PlcStruct":   e.Tipo = Tipo.Udt;            e.Prioridad = 0; break;
                    case "SW.Tags.PlcTagTable":  e.Tipo = Tipo.TablaVariables; e.Prioridad = 1; break;
                    case "SW.Blocks.FB":
                    case "SW.Blocks.FC":         e.Tipo = Tipo.Bloque;         e.Prioridad = 2; break;
                    case "SW.Blocks.OB":
                    case "SW.Blocks.GlobalDB":
                    case "SW.Blocks.ArrayDB":    e.Tipo = Tipo.Bloque;         e.Prioridad = 3; break;
                    case "SW.Blocks.InstanceDB": e.Tipo = Tipo.Bloque;         e.Prioridad = 4; break;
                    default:
                        log.AppendLine("IGNORADO " + ruta + " (tipo no soportado: " + obj.Name.LocalName + ")");
                        return null;
                }
                return e;
            }
            catch (Exception ex)
            {
                log.AppendLine("IGNORADO " + ruta + " (no se puede leer: " + ex.Message + ")");
                return null;
            }
        }

        // ---------- Búsqueda de objetos existentes ----------

        private static bool Existe(PlcSoftware plc, Elemento e)
        {
            switch (e.Tipo)
            {
                case Tipo.Bloque:         return BuscarBloque(plc.BlockGroup, e.Nombre) != null;
                case Tipo.Udt:            return BuscarTipo(plc.TypeGroup, e.Nombre) != null;
                case Tipo.TablaVariables: return BuscarTabla(plc.TagTableGroup, e.Nombre) != null;
            }
            return false;
        }

        private static PlcBlock BuscarBloque(PlcBlockGroup grupo, string nombre)
        {
            PlcBlock b = grupo.Blocks.Find(nombre);
            if (b != null) return b;
            foreach (PlcBlockUserGroup sub in grupo.Groups)
            {
                b = BuscarBloque(sub, nombre);
                if (b != null) return b;
            }
            return null;
        }

        private static PlcType BuscarTipo(PlcTypeGroup grupo, string nombre)
        {
            PlcType t = grupo.Types.Find(nombre);
            if (t != null) return t;
            foreach (PlcTypeUserGroup sub in grupo.Groups)
            {
                t = BuscarTipo(sub, nombre);
                if (t != null) return t;
            }
            return null;
        }

        private static PlcTagTable BuscarTabla(PlcTagTableGroup grupo, string nombre)
        {
            PlcTagTable t = grupo.TagTables.Find(nombre);
            if (t != null) return t;
            foreach (PlcTagTableUserGroup sub in grupo.Groups)
            {
                t = BuscarTabla(sub, nombre);
                if (t != null) return t;
            }
            return null;
        }

        // ---------- Importación ----------

        private static void Importar(PlcSoftware plc, IEngineeringObject destino, Elemento e, bool sobrescribir)
        {
            ImportOptions opciones = sobrescribir ? ImportOptions.Override : ImportOptions.None;

            switch (e.Tipo)
            {
                case Tipo.Bloque:
                {
                    // Si ya existe se sobrescribe en su carpeta actual; si no, en la carpeta seleccionada
                    PlcBlock existente = BuscarBloque(plc.BlockGroup, e.Nombre);
                    PlcBlockGroup grupo = (existente?.Parent as PlcBlockGroup)
                                          ?? Ancestro<PlcBlockGroup>(destino)
                                          ?? plc.BlockGroup;
                    grupo.Blocks.Import(e.Fichero, opciones);
                    break;
                }
                case Tipo.Udt:
                {
                    PlcType existente = BuscarTipo(plc.TypeGroup, e.Nombre);
                    PlcTypeGroup grupo = (existente?.Parent as PlcTypeGroup)
                                         ?? Ancestro<PlcTypeGroup>(destino)
                                         ?? plc.TypeGroup;
                    grupo.Types.Import(e.Fichero, opciones);
                    break;
                }
                case Tipo.TablaVariables:
                {
                    PlcTagTable existente = BuscarTabla(plc.TagTableGroup, e.Nombre);
                    PlcTagTableGroup grupo = (existente?.Parent as PlcTagTableGroup)
                                             ?? Ancestro<PlcTagTableGroup>(destino)
                                             ?? plc.TagTableGroup;
                    grupo.TagTables.Import(e.Fichero, opciones);
                    break;
                }
            }
        }

        // ---------- Compilación y resumen ----------

        private static string Compilar(PlcSoftware plc, StringBuilder log)
        {
            try
            {
                CompilerResult r = plc.GetService<ICompilable>().Compile();
                string texto = (r.State == CompilerResultState.Error ? "Error" :
                                r.State == CompilerResultState.Warning ? "Con avisos" : "Correcta") +
                               " (errores: " + r.ErrorCount + ", avisos: " + r.WarningCount + ")";
                log.AppendLine();
                log.AppendLine("COMPILACIÓN " + plc.Name + ": " + texto);
                AnadirErrores(r.Messages, log);
                return texto;
            }
            catch (Exception ex)
            {
                log.AppendLine();
                log.AppendLine("COMPILACIÓN no realizada: " + ex.Message);
                return "no realizada (" + ex.Message + ")";
            }
        }

        private static void AnadirErrores(CompilerResultMessageComposition mensajes, StringBuilder log)
        {
            foreach (CompilerResultMessage m in mensajes)
            {
                if (m.State == CompilerResultState.Error && !string.IsNullOrEmpty(m.Description))
                    log.AppendLine("   " + m.Path + ": " + m.Description);
                AnadirErrores(m.Messages, log);
            }
        }

        private static string GuardarResumen(TiaPortal tia, string resumen, StringBuilder log)
        {
            try
            {
                Project proyecto = tia.Projects.FirstOrDefault();
                if (proyecto == null) return null;
                string carpeta = Path.Combine(proyecto.Path.DirectoryName, "Import_XML");
                Directory.CreateDirectory(carpeta);
                string ruta = Path.Combine(carpeta, "resumen_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(ruta, resumen + Environment.NewLine + Environment.NewLine + log, Encoding.UTF8);
                return ruta;
            }
            catch
            {
                return null;
            }
        }
    }
}
