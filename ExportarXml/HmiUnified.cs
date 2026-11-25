using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;

namespace ExportarXml
{
    /// <summary>
    /// WinCC Unified (probado en Unified Comfort Panel MTP1500): exportar e importar pantallas.
    ///
    /// Openness no tiene XML de pantallas Unified: cada pantalla se lee con la API y se guarda en JSON;
    /// al importar se recrea con la API. Todo va por reflexión sobre las propiedades .NET de los objetos
    /// (Siemens.Engineering.HmiUnified.*), sin nombres fijos, para que un tipo de objeto nuevo o una
    /// propiedad que no conocemos se exporte e importe igual:
    ///   - propiedad simple con set           → valor (enum por nombre, color "#AARRGGBB")
    ///   - propiedad de solo lectura que es un objeto (Font, Padding, Script, MappingTable…) → objeto anidado
    ///   - propiedad con set que es un objeto (p. ej. la pantalla de una ventana) → {"$ref": nombre}
    ///   - texto multilingüe                 → {"$textos": {"es-ES": "…"}}
    ///   - composición (ScreenItems, Dynamizations, EventHandlers, PropertyEventHandlers, Thresholds…)
    ///     → lista de objetos con "$tipo" (y "$nombre" si tienen nombre)
    /// Para crear un elemento de una composición se busca un Create(...) cuyos parámetros salgan del propio JSON
    /// ($tipo para Create&lt;T&gt;, $nombre, PropertyName, EventType…).
    /// </summary>
    internal static class HmiUnified
    {
        public const string Formato = "SIEMENS-XML-V20 Unified 1";
        private const int ProfundidadMax = 12;

        // Propiedades que no se exportan nunca
        private static readonly HashSet<string> Ignoradas = new HashSet<string>(StringComparer.Ordinal)
        {
            "Parent", "Name", "CurrentQuality"
        };

        // De solo lectura, pero hacen falta para crear el elemento (Create(...)) o para reconocerlo
        private static readonly HashSet<string> Claves = new HashSet<string>(StringComparer.Ordinal)
        {
            "EventType", "PropertyName", "DynamizationType", "ContainedType", "BitDynamizationType"
        };

        // ---------- Exportar ----------

        /// <summary>Panel, software, pantalla o grupo de pantallas Unified (para el menú Exportar).</summary>
        public static bool EsSeleccionable(IEngineeringObject o)
        {
            return o != null && (o.GetType().Name == "HmiSoftware" || EsObjetoUnified(o));
        }

        /// <summary>Cualquier objeto de la API de WinCC Unified (Siemens.Engineering.HmiUnified.*).</summary>
        public static bool EsObjetoUnified(object o)
        {
            return (o?.GetType().Namespace ?? "").StartsWith("Siemens.Engineering.HmiUnified", StringComparison.Ordinal);
        }

        /// <summary>Una pantalla o un grupo de pantallas (lo que tiene ScreenItems o Screens y no es el software).</summary>
        public static bool EsPantallaOGrupo(object o)
        {
            if (!EsObjetoUnified(o) || o.GetType().Name == "HmiSoftware") return false;
            return Propiedad(o.GetType(), "ScreenItems") != null || Propiedad(o.GetType(), "Screens") != null;
        }

        /// <summary>
        /// El HmiSoftware de lo seleccionado. Primero subiendo por Parent; si no llega (objetos de pantalla
        /// en el árbol), busca en los dispositivos del proyecto el software Unified que contiene el objeto.
        /// </summary>
        /// <summary>
        /// "HMI_RT_1: MTP1500 Unified Comfort · 6AV2…": el Parent del software Unified no siempre llega
        /// al DeviceItem, así que se busca el dispositivo del proyecto que lo contiene.
        /// </summary>
        public static string Panel(TiaPortal tia, object sw)
        {
            string p = Hmi.Panel(sw);
            if (p.IndexOf(':') >= 0) return p;
            try
            {
                Project proy = tia?.Projects.FirstOrDefault();
                if (proy != null)
                    foreach (object d in Dispositivos(proy))
                        if (Igual(Hmi.ObtenerUnified(d as IEngineeringObject), sw))
                        {
                            string m = Modelo(d, 0);
                            return Texto(sw, "Name") + " (" + Texto(d, "Name") + ")" + (m.Length > 0 ? ": " + m : "");
                        }
            }
            catch { }
            return p;
        }

        /// <summary>"HMI_RT_1 (HMI_1): MTP1500 Unified Comfort · 6AV2…" a partir del dispositivo seleccionado (sin buscar en el proyecto).</summary>
        public static string PanelDe(object seleccionado, object sw)
        {
            string p = Hmi.Panel(sw);
            if (p.IndexOf(':') >= 0) return p;
            try
            {
                string m = Modelo(seleccionado, 0);
                if (m.Length > 0) return Texto(sw, "Name") + " (" + Texto(seleccionado, "Name") + "): " + m;
            }
            catch { }
            return p;
        }

        private static string Modelo(object d, int nivel)
        {
            if (d == null || nivel > 4) return "";
            foreach (object di in Items(Prop(d, "DeviceItems")))
            {
                string tipo = Texto(di, "TypeIdentifier");
                if (tipo.StartsWith("OrderNumber:", StringComparison.OrdinalIgnoreCase))
                {
                    string modelo = "";
                    try { modelo = Convert.ToString(Llamar(di, "GetAttribute", "TypeName"), CultureInfo.InvariantCulture) ?? ""; } catch { }
                    return (modelo.Length > 0 ? modelo + " · " : "") + tipo.Substring("OrderNumber:".Length);
                }
                string sub = Modelo(di, nivel + 1);
                if (sub.Length > 0) return sub;
            }
            return "";
        }

        public static object Software(TiaPortal tia, IEngineeringObject o)
        {
            object sw = Hmi.ObtenerUnified(o);
            if (sw != null || !EsObjetoUnified(o)) return sw;
            try
            {
                Project p = tia?.Projects.FirstOrDefault();
                if (p == null) return null;
                foreach (object d in Dispositivos(p))
                {
                    object s = Hmi.ObtenerUnified(d as IEngineeringObject);
                    if (s != null && (Igual(s, o) || Contiene(s, o, 0))) return s;
                }
            }
            catch { }
            return null;
        }

        private static IEnumerable<object> Dispositivos(object p)
        {
            List<object> l = new List<object>(Items(Prop(p, "Devices")));
            object sueltos = Prop(p, "UngroupedDevicesGroup");
            if (sueltos != null) l.AddRange(Items(Prop(sueltos, "Devices")));
            Pila(Items(Prop(p, "DeviceGroups")), l, 0);
            return l;
        }

        private static void Pila(IEnumerable<object> grupos, List<object> l, int nivel)
        {
            if (nivel > 10) return;
            foreach (object g in grupos)
            {
                l.AddRange(Items(Prop(g, "Devices")));
                Pila(Items(Prop(g, "Groups")), l, nivel + 1);
            }
        }

        private static bool Contiene(object contenedor, object o, int nivel)
        {
            if (nivel > 10) return false;
            foreach (string c in new[] { "Screens", "ScreenGroups", "Groups" })
                foreach (object x in Items(Prop(contenedor, c)))
                {
                    if (Igual(x, o)) return true;
                    if (c != "Screens" && Contiene(x, o, nivel + 1)) return true;
                }
            return false;
        }

        private static bool Igual(object a, object b)
        {
            try { return ReferenceEquals(a, b) || (a != null && a.Equals(b)); } catch { return false; }
        }

        /// <summary>Texto para el menú de diagnóstico: tipo, ensamblado y cadena de Parent de cada objeto.</summary>
        public static string Diagnostico(TiaPortal tia, IEnumerable<object> seleccion)
        {
            StringBuilder sb = new StringBuilder();
            foreach (object o in seleccion)
            {
                sb.AppendLine("Objeto: " + (o?.GetType().FullName ?? "null") + "  [" + (o?.GetType().Assembly.GetName().Name ?? "") + "]");
                sb.AppendLine("   Nombre: " + Texto(o, "Name") + "   IEngineeringObject: " + (o is IEngineeringObject ? "sí" : "no"));
                try
                {
                    int n = 0;
                    for (IEngineeringObject x = Hmi.Padre(o as IEngineeringObject); x != null && n < 12; x = Hmi.Padre(x), n++)
                        sb.AppendLine("   Parent " + (n + 1) + ": " + x.GetType().FullName + " (" + Texto(x, "Name") + ")");
                }
                catch (Exception ex) { sb.AppendLine("   Error al leer Parent: " + Mensaje(ex)); }
                object sw = null;
                try { sw = Software(tia, o as IEngineeringObject); } catch (Exception ex) { sb.AppendLine("   Error al buscar el software: " + Mensaje(ex)); }
                sb.AppendLine("   Software Unified: " + (sw == null ? "no encontrado" : Texto(sw, "Name")) +
                              "   Pantalla/grupo: " + (EsPantallaOGrupo(o) ? "sí" : "no"));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string Nombre(object o) => Texto(o, "Name");

        /// <summary>
        /// Exporta las pantallas del panel a carpeta\&lt;software&gt;\Pantallas\&lt;grupo&gt;\&lt;pantalla&gt;.json.
        /// filtro: una pantalla o un grupo de pantallas (solo eso), o null (todas).
        /// </summary>
        public static void Exportar(object sw, string carpeta, ExclusiveAccess ea, StringBuilder log, Dictionary<string, int> recuento,
                                    ref int ok, ref int errores, object filtro = null, ICollection<string> elegidas = null, string panel = null)
        {
            if (string.IsNullOrEmpty(panel)) panel = Hmi.Panel(sw);
            string raiz = Path.Combine(carpeta, Limpio(Texto(sw, "Name")));
            if (!EsComfort(panel))
                log.AppendLine("Aviso: " + panel + " no parece un Unified Comfort Panel; esta parte del Add-In está probada en MTP1500.");

            int n = 0;
            string rutaFiltro = filtro != null && filtro.GetType().Name == "HmiScreenGroup" ? RutaGrupo(filtro) : null;
            foreach (var (pantalla, grupo) in Pantallas(sw))
            {
                if (elegidas != null && !elegidas.Contains(Ruta(grupo, Texto(pantalla, "Name")))) continue;
                if (filtro != null)
                {
                    if (rutaFiltro == null && !string.Equals(Texto(pantalla, "Name"), Texto(filtro, "Name"), StringComparison.OrdinalIgnoreCase)) continue;
                    if (rutaFiltro != null && !(grupo == rutaFiltro || grupo.StartsWith(rutaFiltro + "/", StringComparison.OrdinalIgnoreCase))) continue;
                }
                if (ea.IsCancellationRequested) return;
                string nombre = Texto(pantalla, "Name");
                try
                {
                    ea.Text = "Exportando pantalla Unified " + nombre + "...";
                    Dictionary<string, object> d = new Dictionary<string, object>
                    {
                        ["$formato"] = Formato,
                        ["$grupo"] = grupo
                    };
                    foreach (var kv in Objeto(pantalla, true, 0)) d[kv.Key] = kv.Value;
                    string dir = Path.Combine(new[] { raiz, "Pantallas" }.Concat(Partes(grupo).Select(Limpio)).ToArray());
                    Directory.CreateDirectory(dir);
                    string f = Path.Combine(dir, Limpio(nombre) + ".json");
                    File.WriteAllText(f, Json.Escribir(d), new UTF8Encoding(false));
                    n++;
                    log.AppendLine("OK     UNIF   " + f);
                }
                catch (Exception ex)
                {
                    errores++;
                    log.AppendLine("ERROR  UNIF   " + nombre + " -> " + Mensaje(ex));
                }
            }
            ok += n;
            if (n > 0) recuento["Pantallas"] = (recuento.TryGetValue("Pantallas", out int k) ? k : 0) + n;

            // Variables HMI: solo como referencia (nombres para las dinamizaciones); no se importan
            if (filtro == null && elegidas == null) try
            {
                List<object> vars = new List<object>();
                List<object> tags = Items(Prop(sw, "Tags")).ToList();
                if (tags.Count == 0) // en algunos paneles las variables solo cuelgan de sus tablas
                    foreach (object tabla in Items(Prop(sw, "TagTables"))) tags.AddRange(Items(Prop(tabla, "Tags")));
                foreach (object t in tags)
                {
                    Dictionary<string, object> v = new Dictionary<string, object> { ["$nombre"] = Texto(t, "Name") };
                    foreach (string p in new[] { "TagTable", "DataType", "HmiDataType", "Connection", "PlcTag", "Address", "AccessMode", "AcquisitionCycle" })
                    {
                        object x = Prop(t, p);
                        if (x == null) continue;
                        v[p] = EsEscalar(x.GetType()) ? Escalar(x) : Texto(x, "Name");
                    }
                    vars.Add(v);
                }
                if (vars.Count == 0) log.AppendLine("Aviso: no se han encontrado variables HMI (Tags / TagTables) para Variables_referencia.json.");
                if (vars.Count > 0)
                {
                    Directory.CreateDirectory(raiz);
                    File.WriteAllText(Path.Combine(raiz, "Variables_referencia.json"),
                        Json.Escribir(new Dictionary<string, object>
                        {
                            ["$formato"] = Formato,
                            ["$nota"] = "Solo referencia para las dinamizaciones: este fichero no se importa.",
                            ["Variables"] = vars
                        }), new UTF8Encoding(false));
                    recuento["Variables (referencia)"] = vars.Count;
                }
            }
            catch (Exception ex) { log.AppendLine("Aviso: variables HMI sin exportar (" + Mensaje(ex) + ")"); }
        }

        /// <summary>"Grupo/Subgrupo/Pantalla" (o solo "Pantalla" en la raíz): lo que se ve en el selector.</summary>
        public static string Ruta(string grupo, string nombre) => string.IsNullOrEmpty(grupo) ? nombre : grupo + "/" + nombre;

        /// <summary>Rutas de todas las pantallas del panel, en el orden del árbol.</summary>
        public static List<string> ListaPantallas(object sw) => Pantallas(sw).Select(x => Ruta(x.Grupo, Texto(x.Pantalla, "Name"))).ToList();

        /// <summary>Todas las pantallas del panel con la ruta de su grupo ("" = raíz, "A/B" = grupo B dentro de A).</summary>
        private static IEnumerable<(object Pantalla, string Grupo)> Pantallas(object sw)
        {
            foreach (object p in Items(Prop(sw, "Screens"))) yield return (p, "");
            foreach (object g in Items(Prop(sw, "ScreenGroups")))
                foreach (var x in PantallasGrupo(g, Texto(g, "Name"))) yield return x;
        }

        /// <summary>Ruta "A/B" de un grupo de pantallas (subiendo por Parent hasta el software).</summary>
        private static string RutaGrupo(object g)
        {
            List<string> partes = new List<string>();
            object x = g;
            for (int i = 0; x != null && i < 50; i++)
            {
                string t = x.GetType().Name;
                if (t == "HmiSoftware" || x is Project || x is TiaPortal) break;
                if (t == "HmiScreenGroup") partes.Insert(0, Texto(x, "Name"));
                x = Prop(x, "Parent"); // grupo → composición → grupo… → software
            }
            return string.Join("/", partes);
        }

        private static IEnumerable<(object, string)> PantallasGrupo(object g, string ruta)
        {
            foreach (object p in Items(Prop(g, "Screens"))) yield return (p, ruta);
            foreach (object sub in Items(Prop(g, "Groups")))
                foreach (var x in PantallasGrupo(sub, ruta + "/" + Texto(sub, "Name"))) yield return x;
        }

        /// <summary>Un objeto de la API → diccionario JSON (recursivo).</summary>
        private static Dictionary<string, object> Objeto(object o, bool conTipo, int nivel)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            if (conTipo) d["$tipo"] = o.GetType().Name;
            PropertyInfo pn = o.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            if (pn != null && pn.GetIndexParameters().Length == 0)
            {
                string nombre = Texto(o, "Name");
                if (nombre.Length > 0) d["$nombre"] = nombre;
            }
            if (nivel > ProfundidadMax) return d;

            foreach (PropertyInfo p in Propiedades(o.GetType()))
            {
                object v;
                try { v = p.GetValue(o, null); }
                catch { continue; } // propiedad que no aplica a este objeto
                if (v == null) continue;

                if (EsMultilingue(v))
                {
                    Dictionary<string, object> textos = LeerTextos(v);
                    if (textos.Count > 0) d[p.Name] = new Dictionary<string, object> { ["$textos"] = textos };
                }
                else if (EsComposicion(v))
                {
                    List<object> l = Items(v).Select(e => (object)Objeto(e, true, nivel + 1)).ToList();
                    if (l.Count > 0) d[p.Name] = l;
                }
                else if (EsEscalar(v.GetType()))
                {
                    if (!p.CanWrite && !Claves.Contains(p.Name)) continue;
                    // color guardado como número ARGB (uint): en hexadecimal, que se lee mejor
                    d[p.Name] = (v is uint u && p.Name.IndexOf("Color", StringComparison.Ordinal) >= 0) ? "#" + u.ToString("X8") : Escalar(v);
                }
                else if (v is IEngineeringObject)
                {
                    if (p.CanWrite && Texto(v, "Name").Length > 0)
                        d[p.Name] = new Dictionary<string, object> { ["$ref"] = Texto(v, "Name"), ["$tipo"] = v.GetType().Name };
                    else if (p.CanWrite || EsParte(o, v))
                    {
                        Dictionary<string, object> sub = Objeto(v, false, nivel + 1);
                        sub.Remove("$nombre");
                        if (sub.Count > 0) d[p.Name] = sub;
                    }
                }
                else if (v is IEnumerable lista && !(v is string))
                {
                    List<object> l = lista.Cast<object>().Where(x => x != null && EsEscalar(x.GetType())).Select(Escalar).ToList();
                    if (l.Count > 0 && (p.CanWrite || v is IList)) d[p.Name] = l;
                }
            }
            return d;
        }

        // ---------- Importar ----------

        private class Contexto
        {
            public object Sw;
            public StringBuilder Log;
            public int Avisos;
            public HashSet<string> Idiomas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> IdiomasQuitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<(object Obj, PropertyInfo P, string Nombre, string Tipo, string Ruta)> Referencias = new List<(object, PropertyInfo, string, string, string)>();
            public Dictionary<string, Type> Tipos = new Dictionary<string, Type>(StringComparer.Ordinal);
            public void Aviso(string ruta, string texto) { Avisos++; Log.AppendLine("   aviso  " + ruta + ": " + texto); }
        }

        public static void Importar(TiaPortal tia, object sw, IEnumerable<string> rutas, IEngineeringObject seleccionado = null)
        {
            Project proyecto = tia.Projects.FirstOrDefault();
            if (proyecto == null) return;
            string panel = Panel(tia, sw);
            StringBuilder log = new StringBuilder();
            log.AppendLine("HMI destino: " + panel + " (WinCC Unified)");
            if (!EsComfort(panel)) log.AppendLine("Aviso: no parece un Unified Comfort Panel; esta parte del Add-In está probada en MTP1500.");
            log.AppendLine(ExportarXml.Idiomas.Preparar(tia));
            log.AppendLine();

            // Pantallas en JSON; lo demás se ignora
            List<(string Ruta, string Nombre, string Grupo, Dictionary<string, object> D)> pantallas = new List<(string, string, string, Dictionary<string, object>)>();
            int ignorados = 0;
            foreach (string ruta in Importador.AbrirZipsPublico(tia, rutas, log))
            {
                if (!ruta.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) { ignorados++; continue; }
                try
                {
                    Dictionary<string, object> d = Json.Leer(File.ReadAllText(ruta, Encoding.UTF8)) as Dictionary<string, object>;
                    if (d == null || Cad(d, "$tipo") != "HmiScreen")
                    {
                        ignorados++;
                        if (!Path.GetFileName(ruta).StartsWith("Variables_referencia", StringComparison.OrdinalIgnoreCase))
                            log.AppendLine("IGNORADO " + ruta + " (no es una pantalla Unified: falta \"$tipo\": \"HmiScreen\")");
                        continue;
                    }
                    string nombre = Cad(d, "$nombre");
                    if (string.IsNullOrEmpty(nombre)) throw new Exception("falta \"$nombre\"");
                    pantallas.Add((ruta, nombre, Cad(d, "$grupo") ?? "", d));
                }
                catch (Exception ex)
                {
                    log.AppendLine("ERROR    " + ruta + " -> " + Mensaje(ex));
                }
            }
            if (pantallas.Count == 0)
            {
                Dialogos.Informar("No hay pantallas WinCC Unified (.json con \"$tipo\": \"HmiScreen\") en lo elegido." + Environment.NewLine +
                                  "Detalle:" + Environment.NewLine + log, "Importar HMI Unified", true);
                return;
            }

            var existentes = pantallas.Where(p => BuscarPantalla(sw, p.Nombre) != null).ToList();
            bool sobrescribir = false;
            if (existentes.Count > 0)
            {
                Respuesta r = Dialogos.Preguntar(existentes.Count + " pantalla(s) ya existen (" +
                                                 string.Join(", ", existentes.Take(6).Select(e => e.Nombre)) + (existentes.Count > 6 ? ", …" : "") + ")." +
                                                 Environment.NewLine + Environment.NewLine +
                                                 "Sí / Yes → sobrescribir (se vacían y se rellenan; la pantalla se mantiene y no se rompen las referencias a ella)" +
                                                 Environment.NewLine + "No → importar solo las nuevas" + Environment.NewLine + "Cancelar → no importar nada",
                                                 "Importar HMI Unified");
                if (r == Respuesta.Cancelar) return;
                sobrescribir = r == Respuesta.Si;
            }
            var pendientes = pantallas.Where(p => sobrescribir || !existentes.Contains(p)).ToList();

            Contexto c = new Contexto { Sw = sw, Log = log };
            try { foreach (Language l in proyecto.LanguageSettings.ActiveLanguages) c.Idiomas.Add(l.Culture.Name); } catch { }

            string fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dirImport = Path.Combine(proyecto.Path.DirectoryName, "Import_XML");
            string copia = Path.Combine(dirImport, "copia_unified_" + fecha);
            int importadas = 0, conError = 0;
            string compilacion = "no realizada";

            using (ExclusiveAccess ea = tia.ExclusiveAccess("Importando pantallas Unified..."))
            {
                try { proyecto.Save(); log.AppendLine("Proyecto guardado antes de importar."); } catch { }

                // Copia en JSON de lo que se va a sobrescribir
                foreach (var p in pendientes)
                {
                    object actual = BuscarPantalla(sw, p.Nombre);
                    if (actual == null) continue;
                    try
                    {
                        Directory.CreateDirectory(copia);
                        Dictionary<string, object> d = new Dictionary<string, object> { ["$formato"] = Formato, ["$grupo"] = GrupoDe(sw, actual) };
                        foreach (var kv in Objeto(actual, true, 0)) d[kv.Key] = kv.Value;
                        File.WriteAllText(Path.Combine(copia, Limpio(p.Nombre) + ".json"), Json.Escribir(d), new UTF8Encoding(false));
                    }
                    catch (Exception ex) { log.AppendLine("Aviso: sin copia de " + p.Nombre + " (" + Mensaje(ex) + ")"); }
                }
                if (Directory.Exists(copia)) log.AppendLine("Copia de lo sobrescrito (para volver atrás, importarla) en " + copia);
                log.AppendLine();

                foreach (var p in pendientes)
                {
                    if (ea.IsCancellationRequested) break;
                    ea.Text = "Importando pantalla Unified " + p.Nombre + "...";
                    int avisosAntes = c.Avisos;
                    try
                    {
                        object pantalla = BuscarPantalla(sw, p.Nombre);
                        if (pantalla != null) Vaciar(pantalla);
                        else
                        {
                            object grupo = Grupo(sw, p.Grupo);
                            pantalla = Llamar(Prop(grupo, "Screens"), "Create", p.Nombre);
                        }
                        log.AppendLine("PANTALLA " + p.Nombre + (p.Grupo.Length > 0 ? "  (grupo " + p.Grupo + ")" : "") + "  <-  " + p.Ruta);
                        Rellenar(pantalla, p.D, c, p.Nombre);
                        importadas++;
                        log.AppendLine("OK       " + p.Nombre + (c.Avisos > avisosAntes ? "  (" + (c.Avisos - avisosAntes) + " aviso/s)" : ""));
                    }
                    catch (Exception ex)
                    {
                        conError++;
                        log.AppendLine("ERROR    " + p.Nombre + " -> " + Mensaje(ex));
                    }
                }

                // Referencias a objetos que pueden haberse creado después (pantallas de una ventana, etc.)
                foreach (var r in c.Referencias)
                {
                    try
                    {
                        object destino = Resolver(sw, r.Nombre, r.Tipo);
                        if (destino == null) { c.Aviso(r.Ruta, "no existe " + r.Tipo + " \"" + r.Nombre + "\""); continue; }
                        Asignar(r.Obj, r.P, destino);
                    }
                    catch (Exception ex) { c.Aviso(r.Ruta, Mensaje(ex)); }
                }

                if (c.IdiomasQuitados.Count > 0)
                    log.AppendLine("Textos en " + string.Join(", ", c.IdiomasQuitados) + " no importados: el proyecto no tiene ese idioma.");

                if (importadas > 0)
                {
                    try
                    {
                        ea.Text = "Compilando HMI Unified...";
                        ICompilable comp = Compilador(sw, seleccionado, out string donde);
                        if (comp == null) compilacion = "no realizada: no se encuentra el servicio de compilación (compila el HMI a mano)";
                        else
                        {
                            log.AppendLine("Compilando con " + donde + ".");
                            CompilerResult res = comp.Compile();
                            compilacion = res.State + " (errores: " + res.ErrorCount + ", avisos: " + res.WarningCount + ")";
                            foreach (CompilerResultMessage m in res.Messages) Mensajes(m, log, 1);
                        }
                    }
                    catch (Exception ex) { compilacion = "error al compilar: " + Mensaje(ex); }
                }
            }

            string resumen = "Pantallas importadas: " + importadas + "   Con error: " + conError + "   Avisos: " + c.Avisos +
                             "   Ignorados: " + ignorados + Environment.NewLine +
                             "Compilación HMI: " + compilacion + Environment.NewLine +
                             "Volver atrás: deshacer en TIA no aplica; reimportar la copia de Import_XML\\copia_unified_<fecha> o recuperar el proyecto guardado.";
            Directory.CreateDirectory(dirImport);
            string fichero = Path.Combine(dirImport, "resumen_unified_" + fecha + ".txt");
            File.WriteAllText(fichero, resumen + Environment.NewLine + Environment.NewLine + log, Encoding.UTF8);
            Dialogos.Informar(resumen + Environment.NewLine + Environment.NewLine + "Detalle en:" + Environment.NewLine + fichero,
                              "Importar HMI Unified", conError > 0);
        }

        /// <summary>
        /// Servicio de compilación del HMI Unified: el del software; si no lo da, el del módulo del panel que
        /// contiene el software, o el del dispositivo seleccionado.
        /// </summary>
        private static ICompilable Compilador(object sw, IEngineeringObject seleccionado, out string donde)
        {
            donde = "";
            ICompilable c = null;
            try { c = (sw as IEngineeringServiceProvider)?.GetService<ICompilable>(); } catch { }
            if (c != null) { donde = "el software " + Texto(sw, "Name"); return c; }
            try
            {
                List<IEngineeringObject> pendientes = new List<IEngineeringObject>();
                if (seleccionado != null) pendientes.Add(seleccionado);
                for (int i = 0; i < pendientes.Count && i < 200; i++)
                {
                    IEngineeringObject x = pendientes[i];
                    if (x is Siemens.Engineering.HW.DeviceItem di)
                    {
                        Siemens.Engineering.HW.Features.SoftwareContainer sc = di.GetService<Siemens.Engineering.HW.Features.SoftwareContainer>();
                        if (sc?.Software != null && (ReferenceEquals(sc.Software, sw) || sc.Software.Equals(sw)))
                        {
                            try { c = (sc as object as IEngineeringServiceProvider)?.GetService<ICompilable>(); } catch { }
                            if (c != null) { donde = "el contenedor de software de " + Texto(di, "Name"); return c; }
                            try { c = di.GetService<ICompilable>(); } catch { }
                            if (c != null) { donde = "el módulo " + Texto(di, "Name"); return c; }
                        }
                        pendientes.AddRange(di.DeviceItems);
                    }
                    else if (x is Siemens.Engineering.HW.Device d) pendientes.AddRange(d.DeviceItems);
                }
                if (seleccionado is IEngineeringServiceProvider sp)
                {
                    try { c = sp.GetService<ICompilable>(); } catch { }
                    if (c != null) { donde = "el dispositivo seleccionado"; return c; }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Rellena un objeto de la API con un diccionario JSON (recursivo).</summary>
        private static void Rellenar(object o, Dictionary<string, object> d, Contexto c, string ruta)
        {
            // Orden: valores simples → objetos anidados → composiciones (los objetos de la pantalla, al final)
            foreach (int fase in new[] { 0, 1, 2 })
            {
                foreach (var kv in d)
                {
                    if (kv.Key.StartsWith("$")) continue;
                    int f = kv.Value is List<object> l2 && l2.Any(x => x is Dictionary<string, object>) ? 2
                          : kv.Value is Dictionary<string, object> dd && !dd.ContainsKey("$ref") && !dd.ContainsKey("$textos") ? 1 : 0;
                    if (f != fase) continue;
                    string aqui = ruta + "." + kv.Key;

                    PropertyInfo p = Propiedad(o.GetType(), kv.Key);
                    if (p == null) { c.Aviso(aqui, o.GetType().Name + " no tiene la propiedad " + kv.Key); continue; }
                    try
                    {
                        switch (kv.Value)
                        {
                            case Dictionary<string, object> r when r.ContainsKey("$ref"):
                                if (!p.CanWrite) { c.Aviso(aqui, "es de solo lectura"); break; }
                                c.Referencias.Add((o, p, Cad(r, "$ref"), Cad(r, "$tipo"), aqui));
                                break;
                            case Dictionary<string, object> t when t.ContainsKey("$textos"):
                                EscribirTextos(p.GetValue(o, null), t["$textos"] as Dictionary<string, object>, c, aqui);
                                break;
                            case Dictionary<string, object> parte:
                                object sub = p.GetValue(o, null);
                                if (sub == null) { c.Aviso(aqui, "no existe en este objeto"); break; }
                                Rellenar(sub, parte, c, aqui);
                                break;
                            case List<object> lista when fase == 2:
                                object comp = p.GetValue(o, null);
                                if (comp == null) { c.Aviso(aqui, "no existe en este objeto"); break; }
                                LlenarComposicion(comp, lista.OfType<Dictionary<string, object>>().ToList(), c, aqui);
                                break;
                            case List<object> valores:
                                EscribirLista(o, p, valores);
                                break;
                            default:
                                if (!p.CanWrite) break; // clave de creación (EventType, PropertyName…) o de solo lectura
                                Asignar(o, p, Convertir(kv.Value, p.PropertyType));
                                break;
                        }
                    }
                    catch (Exception ex) { c.Aviso(aqui, Mensaje(ex)); }
                }
            }
        }

        private static void LlenarComposicion(object comp, List<Dictionary<string, object>> elementos, Contexto c, string ruta)
        {
            List<object> existentes = Items(comp).ToList();
            HashSet<object> usados = new HashSet<object>();
            int i = 0;
            foreach (Dictionary<string, object> e in elementos)
            {
                string tipo = Cad(e, "$tipo");
                string nombre = Cad(e, "$nombre");
                string aqui = ruta + "[" + (nombre ?? tipo + "#" + i) + "]";
                try
                {
                    object destino = existentes.FirstOrDefault(x => !usados.Contains(x) && Coincide(x, e));
                    if (destino == null && nombre == null && !e.ContainsKey("PropertyName") && !e.ContainsKey("EventType") &&
                        i < existentes.Count && !usados.Contains(existentes[i]) && existentes[i].GetType().Name == tipo)
                        destino = existentes[i]; // elementos sin nombre que el objeto ya trae (columnas, ejes…): por posición
                    if (destino == null) destino = Crear(comp, e, c);
                    usados.Add(destino);
                    Rellenar(destino, e, c, aqui);
                }
                catch (Exception ex) { c.Aviso(aqui, Mensaje(ex)); }
                i++;
            }
        }

        /// <summary>¿El elemento existente es el que describe el JSON? (por nombre, o por PropertyName / EventType).</summary>
        private static bool Coincide(object x, Dictionary<string, object> e)
        {
            string tipo = Cad(e, "$tipo");
            if (tipo != null && x.GetType().Name != tipo) return false;
            bool alguna = false;
            foreach (string k in new[] { "$nombre", "PropertyName", "EventType" })
            {
                if (!e.ContainsKey(k)) continue;
                alguna = true;
                string actual = Convert.ToString(Prop(x, k == "$nombre" ? "Name" : k), CultureInfo.InvariantCulture);
                if (!string.Equals(actual, Convert.ToString(e[k], CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)) return false;
            }
            return alguna;
        }

        /// <summary>
        /// Crea un elemento en una composición: prueba los Create(...) públicos (genéricos o no) y elige el que
        /// pueda rellenar entero con datos del JSON, el de más parámetros.
        /// </summary>
        private static object Crear(object comp, Dictionary<string, object> e, Contexto c)
        {
            string tipo = Cad(e, "$tipo");
            Type t = tipo == null ? null : Tipo(tipo, comp, c);
            var candidatos = new List<(MethodInfo M, object[] Args)>();
            foreach (MethodInfo m0 in comp.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "Create"))
            {
                MethodInfo m = m0;
                if (m.IsGenericMethodDefinition)
                {
                    if (t == null || m.GetGenericArguments().Length != 1) continue;
                    try { m = m.MakeGenericMethod(t); } catch { continue; }
                }
                ParameterInfo[] ps = m.GetParameters();
                object[] args = new object[ps.Length];
                bool vale = true;
                for (int k = 0; k < ps.Length && vale; k++) vale = Argumento(ps[k], e, out args[k]);
                if (vale) candidatos.Add((m, args));
            }
            if (candidatos.Count == 0)
                throw new Exception("no se puede crear " + (tipo ?? "el elemento") + " en " + comp.GetType().Name +
                                    (t == null && tipo != null ? " (tipo desconocido)" : " (ningún Create admite los datos del JSON)"));
            var elegido = candidatos.OrderByDescending(x => x.Args.Length).ThenByDescending(x => x.M.IsGenericMethod).First();
            try { return elegido.M.Invoke(comp, elegido.Args); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        /// <summary>Valor para un parámetro de Create(...), sacado del JSON del elemento.</summary>
        private static bool Argumento(ParameterInfo p, Dictionary<string, object> e, out object valor)
        {
            valor = null;
            Type t = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
            string n = p.Name ?? "";
            // 1) propiedad del JSON con el mismo nombre que el parámetro
            string clave = e.Keys.FirstOrDefault(k => !k.StartsWith("$") && string.Equals(k, n, StringComparison.OrdinalIgnoreCase));
            if (clave != null && !(e[clave] is Dictionary<string, object>) && !(e[clave] is List<object>))
            {
                try { valor = Convertir(e[clave], t); return true; } catch { return false; }
            }
            if (t == typeof(string))
            {
                if (n.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0 && e.ContainsKey("PropertyName")) { valor = Cad(e, "PropertyName"); return true; }
                if (n.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 && e.ContainsKey("$nombre")) { valor = Cad(e, "$nombre"); return true; }
                if (n.IndexOf("type", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!e.ContainsKey("ContainedType")) return false;
                    valor = Cad(e, "ContainedType");
                    return true;
                }
                if (e.ContainsKey("PropertyName")) { valor = Cad(e, "PropertyName"); return true; }
                return false;
            }
            if (t.IsEnum)
            {
                // una propiedad del JSON acabada en "Type" cuyo valor es un nombre de ese enum (BitDynamizationType…)
                foreach (var kv in e.Where(x => !x.Key.StartsWith("$") && x.Key.EndsWith("Type", StringComparison.Ordinal) && x.Value is string))
                {
                    string v = (string)kv.Value;
                    if (Enum.GetNames(t).Any(nm => string.Equals(nm, v, StringComparison.OrdinalIgnoreCase))) { valor = Enum.Parse(t, v, true); return true; }
                }
            }
            return false;
        }

        private static void Vaciar(object pantalla)
        {
            foreach (string comp in new[] { "ScreenItems", "EventHandlers", "PropertyEventHandlers", "Dynamizations" })
                foreach (object x in Items(Prop(pantalla, comp)).ToList())
                    Llamar(x, "Delete");
        }

        // ---------- Textos multilingües ----------

        private static bool EsMultilingue(object v) => v.GetType().Name.IndexOf("MultilingualText", StringComparison.Ordinal) >= 0 && Prop(v, "Items") != null;

        private static Dictionary<string, object> LeerTextos(object ml)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            foreach (object item in Items(Prop(ml, "Items")))
            {
                string cultura = Cultura(item);
                string texto = Texto(item, "Text");
                if (cultura.Length > 0 && texto.Length > 0) d[cultura] = texto;
            }
            return d;
        }

        private static void EscribirTextos(object ml, Dictionary<string, object> textos, Contexto c, string ruta)
        {
            if (ml == null || textos == null) return;
            List<object> items = Items(Prop(ml, "Items")).ToList();
            foreach (var kv in textos)
            {
                object item = items.FirstOrDefault(i => string.Equals(Cultura(i), kv.Key, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    if (c.Idiomas.Count > 0 && !c.Idiomas.Contains(kv.Key)) c.IdiomasQuitados.Add(kv.Key);
                    else c.Aviso(ruta, "sin texto en " + kv.Key);
                    continue;
                }
                Asignar(item, Propiedad(item.GetType(), "Text"), Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
            }
        }

        private static string Cultura(object item)
        {
            object lang = Prop(item, "Language");
            object cult = Prop(lang, "Culture");
            return (cult as CultureInfo)?.Name ?? Texto(lang, "Name");
        }

        // ---------- Valores ----------

        private static bool EsEscalar(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(Color) ||
                   t == typeof(TimeSpan) || t == typeof(DateTime) || t == typeof(Guid) || t == typeof(CultureInfo);
        }

        private static object Escalar(object v)
        {
            switch (v)
            {
                case Color col: return "#" + col.ToArgb().ToString("X8");
                case Enum en: return en.ToString();
                case TimeSpan ts: return ts.ToString("c", CultureInfo.InvariantCulture);
                case DateTime dt: return dt.ToString("o", CultureInfo.InvariantCulture);
                case CultureInfo ci: return ci.Name;
                case char ch: return ch.ToString();
                case bool _: case string _: case double _: case float _: case decimal _: return v;
                case IConvertible _ when v.GetType().IsPrimitive: return Convert.ToInt64(v, CultureInfo.InvariantCulture);
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }

        private static object Convertir(object v, Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (v == null) return null;
            if (t == typeof(object))
            {
                // propiedad de tipo object (Value de una tabla de valores…): un "#AARRGGBB" es un color
                if (v is string so && so.Length == 9 && so[0] == '#' &&
                    int.TryParse(so.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
                    return Color.FromArgb(argb);
                return v;
            }
            if (t == typeof(string)) return Convert.ToString(v, CultureInfo.InvariantCulture);
            if (t.IsEnum)
                return v is string s ? Enum.Parse(t, s, true) : Enum.ToObject(t, Convert.ToInt64(v, CultureInfo.InvariantCulture));
            if (t == typeof(Color))
            {
                if (v is string sc)
                {
                    sc = sc.Trim().TrimStart('#');
                    if (sc.Length == 6) sc = "FF" + sc;
                    return Color.FromArgb(int.Parse(sc, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                }
                return Color.FromArgb(Convert.ToInt32(v, CultureInfo.InvariantCulture));
            }
            if (t == typeof(TimeSpan)) return v is string st ? TimeSpan.Parse(st, CultureInfo.InvariantCulture) : TimeSpan.FromMilliseconds(Convert.ToDouble(v, CultureInfo.InvariantCulture));
            if (t == typeof(DateTime)) return DateTime.Parse(Convert.ToString(v, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (t == typeof(Guid)) return Guid.Parse(Convert.ToString(v, CultureInfo.InvariantCulture));
            if (t == typeof(CultureInfo)) return CultureInfo.GetCultureInfo(Convert.ToString(v, CultureInfo.InvariantCulture));
            if (t == typeof(bool) && v is string sb) return bool.Parse(sb);
            if (t.IsPrimitive || t == typeof(decimal))
            {
                if (v is string sh && sh.StartsWith("#")) // color guardado como número ARGB
                {
                    uint argb = uint.Parse(sh.TrimStart('#').Length == 6 ? "FF" + sh.TrimStart('#') : sh.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return t == typeof(int) ? (object)unchecked((int)argb) : Convert.ChangeType(argb, t, CultureInfo.InvariantCulture);
                }
                return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
            }
            return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
        }

        private static void EscribirLista(object o, PropertyInfo p, List<object> valores)
        {
            object actual = p.GetValue(o, null);
            if (actual != null && EsComposicion(actual))
            {
                if (valores.Count == 0) return; // composición vacía: nada que crear
                throw new Exception("una composición necesita objetos con \"$tipo\", no valores sueltos");
            }
            Type elem = p.PropertyType.IsArray ? p.PropertyType.GetElementType()
                      : p.PropertyType.IsGenericType ? p.PropertyType.GetGenericArguments()[0] : typeof(object);
            if (actual is IList lista && !lista.IsReadOnly && !lista.IsFixedSize)
            {
                lista.Clear();
                foreach (object v in valores) lista.Add(Convertir(v, elem));
                return;
            }
            if (!p.CanWrite) throw new Exception("lista de solo lectura");
            if (p.PropertyType.IsArray)
            {
                Array a = Array.CreateInstance(elem, valores.Count);
                for (int i = 0; i < valores.Count; i++) a.SetValue(Convertir(valores[i], elem), i);
                Asignar(o, p, a);
                return;
            }
            IList nueva = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elem));
            foreach (object v in valores) nueva.Add(Convertir(v, elem));
            Asignar(o, p, nueva);
        }

        private static void Asignar(object o, PropertyInfo p, object valor)
        {
            if (p == null) throw new Exception("propiedad desconocida");
            try { p.SetValue(o, valor, null); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        // ---------- Búsquedas ----------

        private static object BuscarPantalla(object sw, string nombre) =>
            Pantallas(sw).Select(x => x.Pantalla).FirstOrDefault(p => string.Equals(Texto(p, "Name"), nombre, StringComparison.OrdinalIgnoreCase));

        private static string GrupoDe(object sw, object pantalla) =>
            Pantallas(sw).Where(x => Equals(x.Pantalla, pantalla) || Texto(x.Pantalla, "Name") == Texto(pantalla, "Name")).Select(x => x.Grupo).FirstOrDefault() ?? "";

        /// <summary>El grupo de pantallas de la ruta "A/B" (lo crea si no existe). "" = raíz del panel.</summary>
        private static object Grupo(object sw, string ruta)
        {
            object actual = sw;
            bool raiz = true;
            foreach (string parte in Partes(ruta))
            {
                object grupos = Prop(actual, raiz ? "ScreenGroups" : "Groups");
                object g = Items(grupos).FirstOrDefault(x => string.Equals(Texto(x, "Name"), parte, StringComparison.OrdinalIgnoreCase))
                           ?? Llamar(grupos, "Create", parte);
                actual = g;
                raiz = false;
            }
            return actual;
        }

        /// <summary>Objeto con ese nombre y tipo en el panel (pantallas, variables, listas, conexiones…).</summary>
        private static object Resolver(object sw, string nombre, string tipo)
        {
            if (tipo == "HmiScreen") return BuscarPantalla(sw, nombre);
            foreach (PropertyInfo p in sw.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object comp;
                try { comp = p.GetValue(sw, null); } catch { continue; }
                if (comp == null || !EsComposicion(comp)) continue;
                foreach (object x in Items(comp))
                    if ((tipo == null || x.GetType().Name == tipo) && string.Equals(Texto(x, "Name"), nombre, StringComparison.OrdinalIgnoreCase))
                        return x;
            }
            return null;
        }

        /// <summary>Tipo de la API por su nombre corto (HmiButton, TagDynamization…).</summary>
        private static Type Tipo(string nombre, object referencia, Contexto c)
        {
            if (c.Tipos.TryGetValue(nombre, out Type t)) return t;
            IEnumerable<Assembly> ensamblados = new[] { referencia.GetType().Assembly, c.Sw.GetType().Assembly }
                .Concat(AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("Siemens.Engineering", StringComparison.Ordinal)))
                .Distinct();
            foreach (Assembly a in ensamblados)
            {
                Type[] tipos;
                try { tipos = a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { tipos = ex.Types.Where(x => x != null).ToArray(); }
                catch { continue; }
                t = tipos.FirstOrDefault(x => x.Name == nombre && (x.Namespace ?? "").StartsWith("Siemens.Engineering.HmiUnified", StringComparison.Ordinal))
                    ?? tipos.FirstOrDefault(x => x.Name == nombre && (x.Namespace ?? "").StartsWith("Siemens.Engineering", StringComparison.Ordinal));
                if (t != null) break;
            }
            c.Tipos[nombre] = t;
            return t;
        }

        // ---------- Reflexión ----------

        private static IEnumerable<PropertyInfo> Propiedades(Type t) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && !Ignoradas.Contains(p.Name))
             .GroupBy(p => p.Name).Select(g => g.First());

        private static PropertyInfo Propiedad(Type t, string nombre) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .FirstOrDefault(p => p.Name == nombre && p.GetIndexParameters().Length == 0);

        private static bool EsComposicion(object v) =>
            v is IEnumerable && !(v is string) && v.GetType().Name.EndsWith("Composition", StringComparison.Ordinal);

        /// <summary>Objeto anidado propio (Font, Padding, Script, MappingTable…), no una referencia a otro sitio del proyecto.</summary>
        private static bool EsParte(object duenio, object v)
        {
            string ns = v.GetType().Namespace ?? "";
            if (v.GetType().Name == "HmiSoftware" || !ns.StartsWith("Siemens.Engineering.HmiUnified", StringComparison.Ordinal)) return false;
            try
            {
                object padre = Hmi.Padre(v as IEngineeringObject);
                if (padre != null && (ReferenceEquals(padre, duenio) || padre.Equals(duenio))) return true;
            }
            catch { }
            return ns.IndexOf(".UI", StringComparison.Ordinal) >= 0 && !(v.GetType().Name.StartsWith("HmiScreen") && Prop(v, "ScreenItems") != null);
        }

        private static bool EsComfort(string panel) =>
            panel.IndexOf("MTP", StringComparison.OrdinalIgnoreCase) >= 0 || panel.IndexOf("Comfort", StringComparison.OrdinalIgnoreCase) >= 0 ||
            panel.Replace(" ", "").IndexOf("6AV2128", StringComparison.OrdinalIgnoreCase) >= 0; // Unified Comfort Panel (MTP…)

        private static void Mensajes(CompilerResultMessage m, StringBuilder log, int nivel)
        {
            if (m.State == CompilerResultState.Error || m.State == CompilerResultState.Warning)
                if (!string.IsNullOrEmpty(m.Description)) log.AppendLine(new string(' ', nivel * 3) + m.Path + ": " + m.Description);
            if (m.Messages != null) foreach (CompilerResultMessage h in m.Messages) Mensajes(h, log, nivel + 1);
        }

        private static object Prop(object o, string nombre)
        {
            if (o == null || nombre == null) return null;
            try { return Propiedad(o.GetType(), nombre)?.GetValue(o, null); }
            catch { return null; }
        }

        private static IEnumerable<object> Items(object coleccion) =>
            coleccion is IEnumerable e && !(coleccion is string) ? e.Cast<object>().ToList() : new List<object>();

        private static string Texto(object o, string nombre) => Convert.ToString(Prop(o, nombre), CultureInfo.InvariantCulture) ?? "";

        private static string Cad(Dictionary<string, object> d, string k) =>
            d != null && d.TryGetValue(k, out object v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

        private static IEnumerable<string> Partes(string grupo) =>
            (grupo ?? "").Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0);

        private static object Llamar(object o, string metodo, params object[] args)
        {
            if (o == null) throw new Exception("objeto inexistente al llamar a " + metodo);
            MethodInfo m = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(x => x.Name == metodo && !x.IsGenericMethodDefinition && x.GetParameters().Length == args.Length &&
                                                 x.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(args[i])).All(b => b));
            if (m == null) throw new Exception(o.GetType().Name + " no tiene " + metodo + "(" + string.Join(", ", args.Select(a => a.GetType().Name)) + ")");
            try { return m.Invoke(o, args); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        private static string Mensaje(Exception ex) => (ex.Message ?? "").Replace(Environment.NewLine, " ");

        private static string Limpio(string n)
        {
            foreach (char ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_');
            return n.Length == 0 ? "_" : n;
        }
    }
}
