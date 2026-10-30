using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

namespace ExportarXml
{
    /// <summary>
    /// Importa XML (SimaticML) y fuentes externas (.scl, .udt, .db, .awl) en el PLC del objeto seleccionado.
    /// - Detecta solo qué es cada fichero (UDT, tabla de variables, FB, FC, OB, DB, fuente).
    /// - Antes de importar un XML lo comprueba con Validador: si incumple una regla conocida
    ///   no se importa; los avisos de los XSD de TIA solo se apuntan en el resumen.
    /// - Las fuentes se añaden a "Fuentes externas", se generan los bloques y se borra la fuente.
    ///   GenerateBlocksFromSource NO sobrescribe un bloque existente (deja el código viejo), así que
    ///   al sobrescribir se borran antes los objetos que declara la fuente.
    /// - Los documentos SIMATIC SD (.s7dcl KOP, .s7dcf FUP, .s7dcs SCL, .s7dbd DB; con su .s7res)
    ///   se importan con ImportFromDocuments. En SD las instrucciones van por nombre y TIA pone
    ///   la versión, así que sirven para cualquier instrucción sin tener que copiar su XML.
    /// - Orden: primero XML y documentos SD, después fuentes externas; dentro de cada grupo
    ///   UDT > tablas > FB/FC > OB/DB global > DB de instancia, con varias pasadas por si unos
    ///   dependen de otros. Así, si la misma cosa viene en un XML y en una fuente (fuentes de
    ///   respaldo), manda el XML y la fuente solo crea lo que falte.
    /// - Si el objeto ya existe, pregunta si sobrescribir o saltar; al sobrescribir
    ///   lo deja en la carpeta donde ya estaba.
    /// - Antes de importar copia a XML lo que se va a sobrescribir y apunta lo nuevo, para poder
    ///   deshacerlo con "XML > Deshacer última importación" (ver Deshacer.cs). El Ctrl+Z de TIA no
    ///   deshace lo que hace un Add-In.
    /// - Al final compila el software del PLC y deja un resumen.
    /// </summary>
    internal static class Importador
    {
        private const string Titulo = "Importar XML";

        private enum Tipo { Udt, TablaVariables, Bloque, Fuente, Documento }

        /// <summary>Extensiones de fuente externa que TIA sabe convertir en bloques.</summary>
        public static readonly string[] ExtensionesFuente = { ".scl", ".udt", ".db", ".awl" };

        /// <summary>Documentos SIMATIC SD de bloque (V20). El .s7res de textos va al lado y no se elige.</summary>
        public static readonly string[] ExtensionesDocumento = { ".s7dcl", ".s7dcf", ".s7dcs", ".s7dbd" };

        /// <summary>Todo lo que se puede importar: XML + fuentes + documentos SD + paquetes .zip.</summary>
        public static readonly string[] ExtensionesImportables =
            { ".xml", ".scl", ".udt", ".db", ".awl", ".s7dcl", ".s7dcf", ".s7dcs", ".s7dbd", ".zip" };

        private static bool EsFuente(string ruta)
        {
            return ExtensionesFuente.Contains(Path.GetExtension(ruta).ToLowerInvariant());
        }

        private static bool EsDocumento(string ruta)
        {
            return ExtensionesDocumento.Contains(Path.GetExtension(ruta).ToLowerInvariant());
        }

        private sealed class Elemento
        {
            public FileInfo Fichero;
            public string Nombre;
            public Tipo Tipo;
            public int Prioridad;
            public bool Existe;
            public string UltimoError;
            /// <summary>Solo fuentes: bloques y tipos que declara (para saber si ya existen).</summary>
            public List<string> Objetos = new List<string>();
            /// <summary>Solo XML: avisos de los XSD de TIA (no bloquean la importación).</summary>
            public List<string> Avisos = new List<string>();
            /// <summary>Lo que se ha hecho además de importar (copias de seguridad, borrados...).</summary>
            public List<string> Notas = new List<string>();
            /// <summary>El mismo bloque en el otro formato (XML <-> SD), por si este falla.</summary>
            public Elemento Alternativa;
        }

        // ---------- Destino de la importación ----------

        public static bool EsDestino(IEngineeringObject o)
        {
            return ObtenerPlc(o) != null;
        }

        internal static PlcSoftware ObtenerPlc(IEngineeringObject o)
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

            _carpetaCopia = CarpetaCopia(tia);

            Cpu cpuDestino = Cpu.De(plc);
            StringBuilder log = new StringBuilder();
            log.AppendLine("PLC destino: " + cpuDestino);
            log.AppendLine(Validador.EstadoEsquemas());
            log.AppendLine();
            int ignorados = 0;
            List<Elemento> elementos = new List<Elemento>();
            foreach (string ruta in AbrirZips(tia, rutas, log))
            {
                Elemento e = Leer(ruta, log);
                if (e != null) elementos.Add(e);
                else ignorados++;
            }

            if (elementos.Count == 0)
            {
                string rutaVacio = GuardarResumen(tia, "Ningún fichero se puede importar.", log);
                Dialogos.Informar(
                    "Ninguno de los ficheros se puede importar (ni XML SimaticML válido ni fuente externa)." +
                    (rutaVacio != null ? Environment.NewLine + Environment.NewLine + "Detalle en:" + Environment.NewLine + rutaVacio : ""),
                    Titulo, true);
                return;
            }

            // Un mismo bloque en XML y en SD (paquete exportado): se elige uno y el otro queda de reserva
            Emparejar(elementos, cpuDestino, log);

            // ¿Cuáles existen ya en el PLC?
            foreach (Elemento e in elementos) e.Existe = Existe(plc, e);
            List<Elemento> existentes = elementos.Where(e => e.Existe).ToList();

            bool sobrescribir = false;
            if (existentes.Count > 0)
            {
                string ejemplos = string.Join(", ", existentes.Take(6).Select(e => e.Tipo == Tipo.Fuente || e.Tipo == Tipo.Documento
                                                  ? e.Fichero.Name + " (" + string.Join(", ", e.Objetos.Where(o => ExisteObjeto(plc, o))) + ")"
                                                  : e.Nombre))
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
                .OrderBy(e => e.Tipo == Tipo.Fuente ? 1 : 0)
                .ThenBy(e => e.Prioridad)
                .ThenBy(e => e.Nombre)
                .ToList();

            // Objetos creados en esta importación: no se vuelven a importar ni se borran desde
            // otro fichero de la misma selección (p. ej. un XML y su fuente de respaldo)
            HashSet<string> creados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int omitidos = 0;

            int importados = 0;
            string compilacion = "no realizada";

            string deshacer = "";
            using (ExclusiveAccess ea = tia.ExclusiveAccess("Importando XML..."))
            {
                // Deshacer propio: copia de lo que se va a sobrescribir y lista de lo nuevo, ANTES de tocar nada.
                // (El Ctrl+Z de TIA no deshace lo que hace un Add-In.)
                ea.Text = "Preparando el deshacer...";
                List<string> objetos = new List<string>();
                foreach (Elemento e in pendientes)
                {
                    if (e.Tipo == Tipo.Fuente || e.Tipo == Tipo.Documento) objetos.AddRange(e.Objetos);
                    else objetos.Add(e.Nombre);
                }
                string carpetaDeshacer = Deshacer.Preparar(tia, plc, objetos, log);
                log.AppendLine();

                Pasadas(plc, destino, pendientes, sobrescribir, creados, ea, log, "", ref importados, ref omitidos);
                if (UsarAlternativas(pendientes, log))
                    Pasadas(plc, destino, pendientes, sobrescribir, creados, ea, log, "", ref importados, ref omitidos);

                foreach (Elemento e in pendientes)
                {
                    log.AppendLine("ERROR    " + e.Nombre + " -> " + (e.UltimoError ?? "cancelado"));
                    foreach (string n in e.Notas) log.AppendLine("         " + n);
                    foreach (string a in e.Avisos) log.AppendLine("         aviso XSD: " + a);
                }

                if (importados > 0 && !ea.IsCancellationRequested)
                {
                    ea.Text = "Compilando " + plc.Name + "...";
                    compilacion = Compilar(plc, log);
                }

                deshacer = carpetaDeshacer != null
                    ? "Deshacer: clic derecho en el PLC > XML > Deshacer última importación"
                    : "Deshacer: no disponible (proyecto sin guardar en disco)";
            }


            string resumen =
                "Importados: " + importados + "   Saltados: " + saltados +
                (omitidos > 0 ? "   Omitidos (repetidos): " + omitidos : "") +
                "   Errores: " + pendientes.Count + "   No válidos: " + ignorados + Environment.NewLine +
                "Compilación: " + compilacion + Environment.NewLine +
                deshacer;

            string rutaResumen = GuardarResumen(tia, resumen, log);

            Dialogos.Informar(
                resumen + Environment.NewLine + Environment.NewLine +
                (rutaResumen != null ? "Detalle en:" + Environment.NewLine + rutaResumen : ""),
                Titulo,
                pendientes.Count > 0 || compilacion.StartsWith("Error"));
        }

        /// <summary>
        /// Descomprime los .zip (paquetes exportados) en Import_XML\zip_<fecha>\ y devuelve
        /// todas las rutas importables, sin los .zip.
        /// </summary>
        private static IEnumerable<string> AbrirZips(TiaPortal tia, IEnumerable<string> rutas, StringBuilder log)
        {
            List<string> salida = new List<string>();
            foreach (string ruta in rutas)
            {
                if (!string.Equals(Path.GetExtension(ruta), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    salida.Add(ruta);
                    continue;
                }
                try
                {
                    Project proyecto = tia.Projects.FirstOrDefault();
                    string baseDir = proyecto != null ? Path.Combine(proyecto.Path.DirectoryName, "Import_XML") : Path.GetTempPath();
                    string dir = Path.Combine(baseDir, "zip_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), Path.GetFileNameWithoutExtension(ruta));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    ZipFile.ExtractToDirectory(ruta, dir);
                    string[] dentro = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                        .Where(f => ExtensionesImportables.Contains(Path.GetExtension(f).ToLowerInvariant()) &&
                                    !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    log.AppendLine("ZIP      " + ruta + " -> " + dentro.Length + " ficheros en " + dir);
                    salida.AddRange(dentro);
                }
                catch (Exception ex)
                {
                    log.AppendLine("IGNORADO " + ruta + " (no se puede abrir el .zip: " + ex.Message + ")");
                }
            }
            return salida;
        }

        /// <summary>
        /// Si un bloque viene en XML y en SD (paquete de "Exportar selección"), se importa uno:
        /// - el XML si la CPU de origen es la misma que la de destino (copia exacta, con textos);
        /// - el SD si la CPU es otra (TIA pone la versión de cada instrucción para esta CPU);
        /// - el XML si no se sabe la CPU de origen.
        /// El otro queda como alternativa por si el elegido falla.
        /// </summary>
        private static void Emparejar(List<Elemento> elementos, Cpu cpuDestino, StringBuilder log)
        {
            // SD o fuente de un solo objeto: el mismo bloque en texto
            foreach (Elemento sd in elementos.Where(e => e.Tipo == Tipo.Documento ||
                                                         (e.Tipo == Tipo.Fuente && e.Objetos.Count == 1)).ToList())
            {
                Elemento xml = elementos.FirstOrDefault(e => e.Tipo == Tipo.Bloque &&
                                                             string.Equals(e.Nombre, sd.Objetos[0], StringComparison.OrdinalIgnoreCase));
                if (xml == null) continue;

                Cpu origen = Cpu.Origen(xml.Fichero.FullName) ?? Cpu.Origen(sd.Fichero.FullName);
                bool usarSd = origen != null && origen.Conocida && cpuDestino.Conocida && !origen.Igual(cpuDestino);
                Elemento elegido = usarSd ? sd : xml, reserva = usarSd ? xml : sd;

                elegido.Alternativa = reserva;
                elementos.Remove(reserva);
                string texto = sd.Tipo == Tipo.Documento ? "SD" : "la fuente";
                elegido.Notas.Add(usarSd
                    ? "se usa " + texto + ": la CPU de origen (" + origen + ") no es la de destino"
                    : origen == null || !origen.Conocida
                        ? "se usa XML (CPU de origen desconocida); " + texto + " de reserva"
                        : "se usa XML: misma CPU que el origen; " + texto + " de reserva");
            }
        }

        /// <summary>Cambia por su alternativa (el otro formato) cada pendiente que la tenga. true si cambió alguno.</summary>
        private static bool UsarAlternativas(List<Elemento> pendientes, StringBuilder log)
        {
            bool alguno = false;
            for (int i = 0; i < pendientes.Count; i++)
            {
                Elemento e = pendientes[i];
                if (e.Alternativa == null) continue;
                Elemento alt = e.Alternativa;
                alt.Existe = e.Existe;
                alt.Notas.Add("se usa " + (alt.Tipo == Tipo.Documento ? "SD" : alt.Tipo == Tipo.Fuente ? "la fuente" : "XML") + " porque " + e.Fichero.Name +
                              " falló: " + (e.UltimoError ?? "?"));
                pendientes[i] = alt;
                alguno = true;
            }
            return alguno;
        }

        /// <summary>
        /// Varias pasadas sobre los pendientes: si algo falla por depender de otro objeto,
        /// se reintenta mientras alguna pasada consiga importar algo.
        /// </summary>
        private static void Pasadas(PlcSoftware plc, IEngineeringObject destino, List<Elemento> pendientes,
                                    bool sobrescribir, HashSet<string> creados, ExclusiveAccess ea,
                                    StringBuilder log, string sufijo, ref int importados, ref int omitidos,
                                    List<Elemento> exitos = null)
        {
            bool progreso = true;
            while (pendientes.Count > 0 && progreso && !ea.IsCancellationRequested)
            {
                progreso = false;
                foreach (Elemento e in pendientes.ToList())
                {
                    if (ea.IsCancellationRequested) break;
                    ea.Text = "Importando " + e.Nombre + "...";

                    // Ya lo ha creado otro fichero de esta misma importación
                    if (e.Tipo != Tipo.Fuente && creados.Contains(NombreObjeto(e)))
                    {
                        pendientes.Remove(e);
                        omitidos++;
                        progreso = true;
                        log.AppendLine("OMITIDO  " + e.Nombre + " (ya creado en esta importación desde otro fichero)  <-  " + e.Fichero.FullName);
                        continue;
                    }

                    try
                    {
                        Importar(plc, destino, e, sobrescribir, creados);
                        if (e.Tipo == Tipo.Fuente) foreach (string o in e.Objetos) creados.Add(o);
                        else creados.Add(NombreObjeto(e));
                        exitos?.Add(e);
                        pendientes.Remove(e);
                        importados++;
                        progreso = true;
                        log.AppendLine("OK       " + e.Nombre + "  <-  " + e.Fichero.FullName + sufijo);
                        foreach (string n in e.Notas) log.AppendLine("         " + n);
                        foreach (string a in e.Avisos) log.AppendLine("         aviso XSD: " + a);
                    }
                    catch (Exception ex)
                    {
                        e.UltimoError = ex.Message;
                    }
                }
            }
        }

        // ---------- Lectura y clasificación del XML ----------

        private static Elemento Leer(string ruta, StringBuilder log)
        {
            if (EsFuente(ruta)) return LeerFuente(ruta, log, Tipo.Fuente);
            if (EsDocumento(ruta)) return LeerFuente(ruta, log, Tipo.Documento);

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

                // Comprobación previa: si incumple una regla conocida, TIA lo va a rechazar
                Validador.Resultado v = Validador.Comprobar(ruta);
                if (v.Errores.Count > 0)
                {
                    log.AppendLine("NO VÁLIDO " + nombre + "  <-  " + ruta);
                    foreach (string err in v.Errores) log.AppendLine("         " + err);
                    foreach (string a in v.Avisos) log.AppendLine("         aviso XSD: " + a);
                    return null;
                }

                Elemento e = new Elemento { Fichero = new FileInfo(ruta), Nombre = nombre, Avisos = v.Avisos };
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

        // Cabeceras de una fuente: TYPE "x", FUNCTION_BLOCK "x", FUNCTION "x", DATA_BLOCK "x", ORGANIZATION_BLOCK "x"
        private static readonly Regex Cabecera = new Regex(
            @"^[ \t]*(TYPE|FUNCTION_BLOCK|FUNCTION|DATA_BLOCK|ORGANIZATION_BLOCK)[ \t]+(?:""([^""]+)""|([A-Za-z_][\w]*))",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static Elemento LeerFuente(string ruta, StringBuilder log, Tipo tipo)
        {
            try
            {
                string texto = File.ReadAllText(ruta); // detecta BOM; sin BOM lo lee como UTF-8
                List<Match> cabeceras = Cabecera.Matches(texto).Cast<Match>().ToList();
                if (cabeceras.Count == 0)
                {
                    log.AppendLine("IGNORADO " + ruta + " (sin TYPE / FUNCTION / FUNCTION_BLOCK / DATA_BLOCK / ORGANIZATION_BLOCK)");
                    return null;
                }
                if (tipo == Tipo.Documento && cabeceras.Count > 1)
                {
                    log.AppendLine("IGNORADO " + ruta + " (un documento SIMATIC SD tiene que contener un solo bloque)");
                    return null;
                }

                List<string> tipos = cabeceras.Select(c => c.Groups[1].Value.ToUpperInvariant()).ToList();
                Elemento e = new Elemento
                {
                    Fichero = new FileInfo(ruta),
                    Nombre = Path.GetFileName(ruta),
                    Tipo = tipo,
                    Objetos = cabeceras.Select(c => c.Groups[2].Success ? c.Groups[2].Value : c.Groups[3].Value).ToList()
                };

                // Mismo orden que el XML: tipos > FB/FC > OB/DB
                if (tipos.All(t => t == "TYPE")) e.Prioridad = 0;
                else if (tipos.All(t => t == "DATA_BLOCK" || t == "ORGANIZATION_BLOCK")) e.Prioridad = 3;
                else e.Prioridad = 2;

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
                case Tipo.Fuente:
                case Tipo.Documento:      return e.Objetos.Any(o => ExisteObjeto(plc, o));
            }
            return false;
        }

        private static bool ExisteObjeto(PlcSoftware plc, string nombre)
        {
            return BuscarBloque(plc.BlockGroup, nombre) != null || BuscarTipo(plc.TypeGroup, nombre) != null;
        }

        internal static PlcBlock BuscarBloque(PlcBlockGroup grupo, string nombre)
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

        internal static PlcType BuscarTipo(PlcTypeGroup grupo, string nombre)
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

        internal static PlcTagTable BuscarTabla(PlcTagTableGroup grupo, string nombre)
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

        /// <summary>Nombre del objeto de TIA que crea un elemento (en SD y XML, uno solo).</summary>
        private static string NombreObjeto(Elemento e)
        {
            return e.Tipo == Tipo.Documento ? e.Objetos[0] : e.Nombre;
        }

        private static void Importar(PlcSoftware plc, IEngineeringObject destino, Elemento e, bool sobrescribir,
                                     HashSet<string> creados)
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
                case Tipo.Fuente:
                    GenerarDesdeFuente(plc, e, sobrescribir, creados);
                    break;
                case Tipo.Documento:
                {
                    // Igual que el XML: si ya existe, en su carpeta; si no, en la seleccionada
                    PlcBlock existente = BuscarBloque(plc.BlockGroup, e.Objetos[0]);
                    PlcBlockGroup grupo = (existente?.Parent as PlcBlockGroup)
                                          ?? Ancestro<PlcBlockGroup>(destino)
                                          ?? plc.BlockGroup;
                    ImportarDocumento(grupo, e, sobrescribir);
                    break;
                }
            }
        }

        /// <summary>
        /// Importa un bloque desde SIMATIC SD. ImportFromDocuments busca en la carpeta los ficheros
        /// con ese nombre (el documento y su .s7res de textos, si lo hay).
        /// </summary>
        private static void ImportarDocumento(PlcBlockGroup grupo, Elemento e, bool sobrescribir)
        {
            DocumentImportResult r = grupo.Blocks.ImportFromDocuments(
                e.Fichero.Directory,
                Path.GetFileNameWithoutExtension(e.Fichero.Name),
                // TIA solo admite Override para bloques en ImportFromDocuments. No pisa nada sin permiso:
                // si el usuario dijo No a sobrescribir, los que ya existen ni llegan aquí (salen como SALTADO).
                ImportDocumentOptions.Override);

            if (r == null || r.State != DocumentResultState.Success)
                throw new Exception("ImportFromDocuments terminó con estado " + (r == null ? "null" : r.State.ToString()) +
                                    ". TIA no indica la línea: revisa la sintaxis (ramas, plantillas de las cajas) y que los MLC_ del .s7res coincidan. Si el bloque usa algo dudoso, pruébalo en un bloque aparte.");
        }

        /// <summary>
        /// Añade la fuente a "Fuentes externas", genera sus bloques y borra la fuente. Los bloques
        /// generados quedan en la raíz de "Bloques de programa" (Openness no deja elegir carpeta).
        /// </summary>
        private static void GenerarDesdeFuente(PlcSoftware plc, Elemento e, bool sobrescribir, HashSet<string> creados)
        {
            PlcExternalSourceComposition fuentes = plc.ExternalSourceGroup.ExternalSources;

            // GenerateBlocksFromSource no sobrescribe: deja el bloque existente con su código viejo.
            // Para sobrescribir de verdad, se borran antes los objetos que declara la fuente
            // (de atrás adelante: DB/OB antes que FB/FC, y estos antes que los tipos).
            // Antes de borrar se exporta cada objeto a XML, por si la generación falla y hay que recuperarlo.
            if (sobrescribir)
            {
                foreach (string nombre in Enumerable.Reverse(e.Objetos))
                {
                    // Lo acaba de importar otro fichero de esta selección: se respeta
                    if (creados.Contains(nombre))
                    {
                        e.Notas.Add(nombre + ": ya importado en esta ejecución desde otro fichero; no se regenera");
                        continue;
                    }

                    PlcBlock b = BuscarBloque(plc.BlockGroup, nombre);
                    if (b != null)
                    {
                        // Un bloque inconsistente no se puede exportar: se intenta compilar antes
                        if (!b.IsConsistent)
                        {
                            try { b.GetService<ICompilable>()?.Compile(); } catch { }
                        }
                        CopiaDeSeguridad(nombre, f => b.Export(f, ExportOptions.WithDefaults), e);
                        b.Delete();
                        continue;
                    }
                    PlcType t = BuscarTipo(plc.TypeGroup, nombre);
                    if (t != null)
                    {
                        CopiaDeSeguridad(nombre, f => t.Export(f, ExportOptions.WithDefaults), e);
                        t.Delete();
                    }
                }
            }

            // Si quedó una fuente con el mismo nombre de una ejecución anterior, fuera
            PlcExternalSource anterior = fuentes.Find(e.Fichero.Name);
            if (anterior != null) anterior.Delete();

            PlcExternalSource fuente = fuentes.CreateFromFile(e.Fichero.Name, e.Fichero.FullName);
            try
            {
                fuente.GenerateBlocksFromSource();
            }
            finally
            {
                try { fuente.Delete(); } catch { /* si no se puede borrar, se queda en Fuentes externas */ }
            }
        }

        // Carpeta de copias de esta ejecución (solo una ruta: no se guardan objetos de Engineering)
        private static string _carpetaCopia;

        private static string CarpetaCopia(TiaPortal tia)
        {
            Project proyecto = tia.Projects.FirstOrDefault();
            if (proyecto == null) return null;
            return Path.Combine(proyecto.Path.DirectoryName, "Import_XML", "copia_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        /// <summary>
        /// Exporta a XML el objeto que se va a borrar. Si no se puede hacer la copia, no se borra
        /// nada: se lanza la excepción y el fichero sale como ERROR en el resumen.
        /// </summary>
        private static void CopiaDeSeguridad(string nombre, Action<FileInfo> exportar, Elemento e)
        {
            if (_carpetaCopia == null) throw new Exception("No hay proyecto abierto para guardar la copia de " + nombre + ".");
            Directory.CreateDirectory(_carpetaCopia);
            string limpio = Path.GetInvalidFileNameChars().Aggregate(nombre, (n, c) => n.Replace(c, '_'));
            FileInfo f = new FileInfo(Path.Combine(_carpetaCopia, limpio + ".xml"));
            if (f.Exists) f.Delete();
            exportar(f);
            e.Notas.Add("borrado para regenerarlo; copia en " + f.FullName);
        }

        // ---------- Compilación y resumen ----------

        internal static string Compilar(PlcSoftware plc, StringBuilder log)
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
