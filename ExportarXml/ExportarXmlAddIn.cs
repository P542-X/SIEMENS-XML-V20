using System;
using System.IO;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Siemens.Engineering;
using Siemens.Engineering.AddIn.Menu;
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
    /// Clic derecho en el árbol del proyecto > "XML" > Exportar / Importar.
    /// Exportar selección: PLC (dispositivo), Bloques de programa, carpetas, bloques (OB/FB/FC/DB),
    /// Tipos de datos PLC (UDT) y tablas de variables. Deja un paquete:
    ///   <carpeta del proyecto>\Export\<fecha_hora>\
    ///       XML\...      todo en SimaticML (copia exacta, con textos)
    ///       SD\...       los bloques en SIMATIC SD (sin versiones: vale para cualquier CPU)
    ///       _info.md     proyecto, versión de TIA, CPU y firmware, objetos e instrucciones usadas
    ///       _info.txt    lo mismo para el importador (CPU de origen de cada carpeta)
    ///       _resumen.txt detalle de la exportación
    ///   <carpeta del proyecto>\Export\<proyecto>_<fecha_hora>.zip   el paquete en un solo fichero
    /// Importar: ver Importador.cs (admite también el .zip del paquete)
    ///
    /// Nota V20: los Add-Ins no se recargan entre ejecuciones, así que no se guardan
    /// objetos de Engineering (ExclusiveAccess, etc.) como campos: todo va en local
    /// y se pasa por parámetro dentro de un contexto que se crea en cada ejecución.
    /// </summary>
    public class ExportarXmlAddIn : ContextMenuAddIn
    {
        private readonly TiaPortal _tia;

        public ExportarXmlAddIn(TiaPortal tia) : base("XML")
        {
            _tia = tia;
        }

        protected override void BuildContextMenuItems(ContextMenuAddInRoot raizMenu)
        {
            raizMenu.Items.AddActionItem<IEngineeringObject>("Exportar selección", OnExportar, OnEstado);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Importar ficheros (xml, sd, fuentes, zip)...", OnImportarFicheros, OnEstadoImportar);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Importar carpeta...", OnImportarCarpeta, OnEstadoImportar);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Deshacer última importación", OnDeshacer, OnEstadoImportar);
        }

        // ---------- Importación ----------

        private static IEngineeringObject DestinoImportacion(MenuSelectionProvider seleccion)
        {
            return seleccion.GetSelection().OfType<IEngineeringObject>().FirstOrDefault(Importador.EsDestino);
        }

        private MenuStatus OnEstadoImportar(MenuSelectionProvider seleccion)
        {
            return DestinoImportacion(seleccion) != null ? MenuStatus.Enabled : MenuStatus.Hidden;
        }

        private void OnImportarFicheros(MenuSelectionProvider seleccion)
        {
            try
            {
                IEngineeringObject destino = DestinoImportacion(seleccion);
                if (destino == null) return;
                string[] ficheros = Dialogos.ElegirFicheros();
                if (ficheros == null || ficheros.Length == 0) return;
                Importador.Ejecutar(_tia, destino, ficheros);
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al importar:" + Environment.NewLine + ex.Message, "Importar XML", true);
            }
        }

        private void OnDeshacer(MenuSelectionProvider seleccion)
        {
            try
            {
                IEngineeringObject destino = DestinoImportacion(seleccion);
                if (destino == null) return;
                Deshacer.Ejecutar(_tia, Importador.ObtenerPlc(destino));
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al deshacer:" + Environment.NewLine + ex.Message, "Deshacer", true);
            }
        }

        private void OnImportarCarpeta(MenuSelectionProvider seleccion)
        {
            try
            {
                IEngineeringObject destino = DestinoImportacion(seleccion);
                if (destino == null) return;
                string carpeta = Dialogos.ElegirCarpeta();
                if (string.IsNullOrEmpty(carpeta)) return;
                string[] ficheros = Directory.GetFiles(carpeta, "*.*", SearchOption.AllDirectories)
                    .Where(f => Importador.ExtensionesImportables.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();
                if (ficheros.Length == 0)
                {
                    Dialogos.Informar("No hay ficheros .xml, fuentes (.scl, .udt, .db, .awl) ni documentos SIMATIC SD (.s7dcl, .s7dcf, .s7dcs, .s7dbd) en esa carpeta.", "Importar XML", true);
                    return;
                }
                Importador.Ejecutar(_tia, destino, ficheros);
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al importar:" + Environment.NewLine + ex.Message, "Importar XML", true);
            }
        }

        // ---------- Exportación ----------

        private MenuStatus OnEstado(MenuSelectionProvider seleccion)
        {
            return seleccion.GetSelection().OfType<IEngineeringObject>().Any(EsExportable)
                ? MenuStatus.Enabled
                : MenuStatus.Hidden;
        }

        private static bool EsExportable(IEngineeringObject o)
        {
            return o is PlcBlock || o is PlcBlockGroup
                || o is PlcType || o is PlcTypeGroup
                || o is PlcTagTable || o is PlcTagTableGroup
                || o is PlcSoftware || o is Device || o is DeviceItem;
        }

        private void OnExportar(MenuSelectionProvider seleccion)
        {
            try
            {
                Exportar(seleccion.GetSelection().OfType<IEngineeringObject>().ToArray());
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al exportar:" + Environment.NewLine + ex.Message, "Exportar", true);
            }
        }

        private void Exportar(IEngineeringObject[] objetos)
        {
            Project proyecto = _tia.Projects.FirstOrDefault();
            if (proyecto == null) return;

            string fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string export = Path.Combine(proyecto.Path.DirectoryName, "Export");
            string raiz = Path.Combine(export, fecha);
            string carpetaXml = Path.Combine(raiz, "XML");
            string carpetaSd = Path.Combine(raiz, "SD");
            string carpetaFuentes = Path.Combine(raiz, "FUENTES");
            Directory.CreateDirectory(raiz);

            Resultado res = new Resultado();
            ExportadorSd sd = new ExportadorSd { RaizSd = carpetaSd, RaizFuentes = carpetaFuentes };
            List<string> info = new List<string>();
            List<Cpu> cpus = new List<Cpu>();

            using (ExclusiveAccess ea = _tia.ExclusiveAccess("Exportando..."))
            {
                foreach (IEngineeringObject obj in objetos.Where(EsExportable))
                {
                    if (ea.IsCancellationRequested) break;

                    // CPU de origen de lo que se exporta (para el importador y para _info.md)
                    PlcSoftware plc = Importador.ObtenerPlc(obj);
                    Cpu cpu = Cpu.De(plc);
                    info.Add(Cpu.LineaInfo(CarpetaRaiz(obj), cpu));
                    if (plc != null && !cpus.Any(c => c.Plc == cpu.Plc)) cpus.Add(cpu);

                    Procesar(obj, carpetaXml, ea, res);
                    if (ExportadorSd.EsExportable(obj))
                        sd.Procesar(obj, carpetaSd, ea);
                }
            }

            File.WriteAllLines(Path.Combine(raiz, Cpu.FicheroInfo), info.Distinct(), Encoding.UTF8);
            EscribirInstruccionesIa(raiz);
            File.WriteAllText(Path.Combine(raiz, "_info.md"), InfoMd(proyecto, cpus, carpetaXml, carpetaSd, carpetaFuentes, sd), Encoding.UTF8);
            File.WriteAllText(Path.Combine(raiz, "_resumen.txt"),
                "XML     exportados: " + res.Ok + "   errores: " + res.Errores + Environment.NewLine +
                "SD      exportados: " + sd.Ok + Environment.NewLine +
                "FUENTES exportadas: " + sd.Fuentes + " (bloques SCL que SD no admite)" + Environment.NewLine +
                "Solo XML: " + sd.Errores + " bloques (ver _info.md, columna Texto)" + Environment.NewLine +
                "Carpeta: " + raiz + Environment.NewLine + Environment.NewLine +
                "---- XML ----" + Environment.NewLine + res.Log + Environment.NewLine +
                "---- SD / FUENTES ----" + Environment.NewLine + sd.Log, Encoding.UTF8);

            // El paquete en un solo fichero
            string zip = Path.Combine(export, Limpiar(proyecto.Name) + "_" + fecha + ".zip");
            string avisoZip = "";
            try
            {
                CrearZip(raiz, zip);
            }
            catch (Exception ex)
            {
                zip = null;
                avisoZip = Environment.NewLine + "No se pudo crear el .zip: " + ex.Message;
            }

            Dialogos.Informar(
                "XML: " + res.Ok + " (errores " + res.Errores + ")    SD: " + sd.Ok + "    Fuentes SCL: " + sd.Fuentes +
                "    Solo XML: " + sd.Errores + Environment.NewLine +
                string.Join(Environment.NewLine, cpus.Select(c => "CPU " + c)) + Environment.NewLine + Environment.NewLine +
                (zip != null ? "Paquete:" + Environment.NewLine + zip : "Carpeta:" + Environment.NewLine + raiz) + avisoZip +
                Environment.NewLine + Environment.NewLine +
                "Para trabajar con una IA: pásale el .zip y dile que lea primero INSTRUCCIONES_IA.md.",
                "Exportar",
                res.Errores > 0);
        }

        /// <summary>
        /// Zip con rutas separadas por '/' (ZipFile.CreateFromDirectory de .NET Framework pone '\',
        /// y muchas herramientas fuera de Windows no lo entienden).
        /// </summary>
        private static void CrearZip(string carpeta, string zip)
        {
            if (File.Exists(zip)) File.Delete(zip);
            using (FileStream fs = File.Create(zip))
            using (ZipArchive z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (string f in Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories))
                {
                    string nombre = f.Substring(carpeta.Length).TrimStart('\\', '/').Replace('\\', '/');
                    z.CreateEntryFromFile(f, nombre, CompressionLevel.Optimal);
                }
            }
        }

        /// <summary>
        /// Copia al paquete las instrucciones para la IA (van dentro de la DLL como recurso),
        /// para que cualquier IA que reciba el .zip sepa leerlo y qué entregar para importar.
        /// </summary>
        private static void EscribirInstruccionesIa(string raiz)
        {
            try
            {
                using (Stream s = typeof(ExportarXmlAddIn).Assembly.GetManifestResourceStream("INSTRUCCIONES_IA.md"))
                {
                    if (s == null) return;
                    using (FileStream f = File.Create(Path.Combine(raiz, "INSTRUCCIONES_IA.md")))
                        s.CopyTo(f);
                }
            }
            catch { /* sin instrucciones el paquete sigue siendo válido */ }
        }

        /// <summary>Primera carpeta que crea el objeto bajo XML\ (vacío si sus ficheros van sueltos).</summary>
        private static string CarpetaRaiz(IEngineeringObject obj)
        {
            if (obj is PlcBlockGroup g) return Limpiar(g.Name);
            if (obj is PlcTypeGroup t) return Limpiar(t.Name);
            if (obj is PlcTagTableGroup v) return Limpiar(v.Name);
            if (obj is PlcSoftware || obj is Device || obj is DeviceItem)
                return Limpiar(Importador.ObtenerPlc(obj)?.Name ?? "");
            return "";
        }

        /// <summary>Resumen legible del paquete: dónde está todo, qué CPU y qué instrucciones usa.</summary>
        private string InfoMd(Project proyecto, List<Cpu> cpus, string carpetaXml, string carpetaSd, string carpetaFuentes, ExportadorSd sd)
        {
            StringBuilder md = new StringBuilder();
            md.AppendLine("# Exportación de TIA Portal");
            md.AppendLine();
            md.AppendLine("> Para una IA: lee primero `INSTRUCCIONES_IA.md`.");
            md.AppendLine();
            md.AppendLine("| | |");
            md.AppendLine("|---|---|");
            md.AppendLine("| Proyecto | " + proyecto.Name + " |");
            md.AppendLine("| Fecha | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " |");
            md.AppendLine("| TIA Portal (API) | " + typeof(TiaPortal).Assembly.GetName().Version + " |");
            foreach (Cpu c in cpus)
                md.AppendLine("| PLC " + c.Plc + " | " + (c.Conocida ? (c.Modelo + " · " + c.Referencia + " · " + c.Firmware).Trim(' ', '·') : "CPU desconocida") + " |");
            md.AppendLine();
            md.AppendLine("- `XML/`: SimaticML, copia exacta (con textos). Las cajas KOP llevan la versión de esta CPU.");
            md.AppendLine("- `SD/`: SIMATIC SD de los bloques. Sin versiones: se puede importar en cualquier CPU.");
            md.AppendLine("- `FUENTES/`: bloques SCL que SD no admite, como fuente `.scl`.");
            md.AppendLine("- Columna **Texto**: dónde está el bloque en texto (SD o FUENTE). Vacía en UDT y tablas (van en XML, que para ellos no depende de la CPU). Si dice *solo XML*, el motivo va al lado.");
            md.AppendLine();

            List<string[]> objetos = new List<string[]>();
            SortedDictionary<string, SortedSet<string>> instrucciones = new SortedDictionary<string, SortedSet<string>>();
            if (Directory.Exists(carpetaXml))
            {
                foreach (string f in Directory.GetFiles(carpetaXml, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        string texto = File.ReadAllText(f);
                        XElement raiz = XDocument.Parse(texto).Root;
                        XElement obj = raiz?.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW."));
                        if (obj == null) continue;
                        XElement attrs = obj.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
                        Func<string, string> V = n => attrs?.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value ?? "";
                        string tipo = obj.Name.LocalName.Replace("SW.Blocks.", "").Replace("SW.Types.PlcStruct", "UDT")
                                                       .Replace("SW.Tags.PlcTagTable", "Tabla");
                        string rel = f.Substring(carpetaXml.Length).TrimStart('\\', '/');
                        string sinExt = Path.ChangeExtension(rel, null);
                        string rutaSd = Path.Combine(carpetaSd, sinExt);
                        bool haySd = Directory.Exists(Path.GetDirectoryName(rutaSd)) &&
                                     Directory.GetFiles(Path.GetDirectoryName(rutaSd), Path.GetFileName(rutaSd) + ".s7*").Length > 0;
                        bool hayFuente = File.Exists(Path.Combine(carpetaFuentes, sinExt + ".scl"));
                        string texto2 = haySd ? "SD" : hayFuente ? "FUENTE" :
                                        tipo == "UDT" || tipo == "Tabla" ? "" :
                                        "solo XML" + (sd.Motivos.TryGetValue(V("Name"), out string mot) ? ": " + mot : "");
                        objetos.Add(new[] { tipo, V("Name"), V("Number"), V("ProgrammingLanguage"), rel.Replace('\\', '/'), texto2 });

                        // Instrucciones con versión: cajas KOP/FUP y multiinstancias
                        foreach (Match m in Regex.Matches(texto, @"<Part Name=""([^""]+)""[^>]*?Version=""([^""]+)""|<Part Version=""([^""]+)""[^>]*?Name=""([^""]+)""|Datatype=""([^""&]+)""[^>]*?Version=""([^""]+)"""))
                        {
                            string n = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[4].Success ? m.Groups[4].Value : m.Groups[5].Value;
                            string ver = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[6].Value;
                            n = Regex.Replace(n, @"^Array\[[^\]]*\] of ", "");   // Array[..] of TCON -> TCON
                            if (!instrucciones.ContainsKey(n)) instrucciones[n] = new SortedSet<string>();
                            instrucciones[n].Add(ver);
                        }
                    }
                    catch { }
                }
            }

            md.AppendLine("## Objetos (" + objetos.Count + ")");
            md.AppendLine();
            md.AppendLine("| Tipo | Nombre | Nº | Lenguaje | Fichero XML | Texto |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (string[] o in objetos.OrderBy(o => o[0]).ThenBy(o => o[1]))
                md.AppendLine("| " + string.Join(" | ", o) + " |");
            md.AppendLine();
            md.AppendLine("## Instrucciones con versión usadas en esta CPU");
            md.AppendLine();
            if (instrucciones.Count == 0) md.AppendLine("(ninguna)");
            foreach (KeyValuePair<string, SortedSet<string>> i in instrucciones)
                md.AppendLine("- " + i.Key + " " + string.Join(", ", i.Value));
            return md.ToString();
        }

        // ---------- Despacho según el tipo de objeto seleccionado ----------

        private static void Procesar(IEngineeringObject obj, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            if (obj is PlcBlock bloque)
                ExportarBloque(bloque, carpeta, ea, res);
            else if (obj is PlcBlockGroup grupoBloques)
                ExportarGrupoBloques(grupoBloques, Sub(carpeta, grupoBloques.Name), ea, res);
            else if (obj is PlcType udt)
                Exportar(udt.Name, carpeta, (f, o) => udt.Export(f, o), ea, res);
            else if (obj is PlcTypeGroup grupoTipos)
                ExportarGrupoTipos(grupoTipos, Sub(carpeta, grupoTipos.Name), ea, res);
            else if (obj is PlcTagTable tabla)
                Exportar(tabla.Name, carpeta, (f, o) => tabla.Export(f, o), ea, res);
            else if (obj is PlcTagTableGroup grupoTablas)
                ExportarGrupoTablas(grupoTablas, Sub(carpeta, grupoTablas.Name), ea, res);
            else if (obj is PlcSoftware plc)
                ExportarPlc(plc, Sub(carpeta, plc.Name), ea, res);
            else if (obj is Device dispositivo)
                foreach (DeviceItem di in dispositivo.DeviceItems) ExportarDeviceItem(di, carpeta, ea, res);
            else if (obj is DeviceItem item)
                ExportarDeviceItem(item, carpeta, ea, res);
        }

        private static void ExportarDeviceItem(DeviceItem item, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            SoftwareContainer contenedor = item.GetService<SoftwareContainer>();
            if (contenedor?.Software is PlcSoftware plc)
            {
                ExportarPlc(plc, Sub(carpeta, plc.Name), ea, res);
                return;
            }
            // En algunos equipos la CPU cuelga de un rack: bajamos un nivel
            foreach (DeviceItem hijo in item.DeviceItems) ExportarDeviceItem(hijo, carpeta, ea, res);
        }

        private static void ExportarPlc(PlcSoftware plc, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            ExportarGrupoBloques(plc.BlockGroup, Sub(carpeta, "Bloques"), ea, res);
            ExportarGrupoTipos(plc.TypeGroup, Sub(carpeta, "Tipos_PLC"), ea, res);
            ExportarGrupoTablas(plc.TagTableGroup, Sub(carpeta, "Variables_PLC"), ea, res);
        }

        // ---------- Recorrido recursivo de carpetas ----------

        private static void ExportarGrupoBloques(PlcBlockGroup grupo, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            foreach (PlcBlock b in grupo.Blocks) ExportarBloque(b, carpeta, ea, res);
            foreach (PlcBlockUserGroup sub in grupo.Groups) ExportarGrupoBloques(sub, Sub(carpeta, sub.Name), ea, res);
        }

        private static void ExportarGrupoTipos(PlcTypeGroup grupo, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            foreach (PlcType t in grupo.Types) Exportar(t.Name, carpeta, (f, o) => t.Export(f, o), ea, res);
            foreach (PlcTypeUserGroup sub in grupo.Groups) ExportarGrupoTipos(sub, Sub(carpeta, sub.Name), ea, res);
        }

        private static void ExportarGrupoTablas(PlcTagTableGroup grupo, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            foreach (PlcTagTable t in grupo.TagTables) Exportar(t.Name, carpeta, (f, o) => t.Export(f, o), ea, res);
            foreach (PlcTagTableUserGroup sub in grupo.Groups) ExportarGrupoTablas(sub, Sub(carpeta, sub.Name), ea, res);
        }

        // ---------- Exportación ----------

        private static void ExportarBloque(PlcBlock bloque, string carpeta, ExclusiveAccess ea, Resultado res)
        {
            // Un bloque inconsistente no se puede exportar: intentamos compilarlo antes
            if (!bloque.IsConsistent)
            {
                try { bloque.GetService<ICompilable>()?.Compile(); }
                catch { /* si falla, el error saldrá al exportar */ }
            }
            Exportar(bloque.Name, carpeta, (f, o) => bloque.Export(f, o), ea, res);
        }

        private static void Exportar(string nombre, string carpeta, Action<FileInfo, ExportOptions> exportar,
                                     ExclusiveAccess ea, Resultado res)
        {
            if (ea.IsCancellationRequested) return;

            try
            {
                Directory.CreateDirectory(carpeta);
                FileInfo fichero = new FileInfo(Path.Combine(carpeta, Limpiar(nombre) + ".xml"));
                if (fichero.Exists) fichero.Delete();

                ea.Text = "Exportando " + nombre + "...";
                exportar(fichero, ExportOptions.WithDefaults);

                res.Ok++;
                res.Log.AppendLine("OK     " + fichero.FullName);
            }
            catch (Exception ex)
            {
                res.Errores++;
                res.Log.AppendLine("ERROR  " + nombre + " -> " + ex.Message);
            }
        }

        private static string Sub(string carpeta, string nombre)
        {
            return Path.Combine(carpeta, Limpiar(nombre));
        }

        private static string Limpiar(string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre;
        }

        /// <summary>Contadores y log de una ejecución (se crea nuevo en cada clic).</summary>
        private sealed class Resultado
        {
            public readonly StringBuilder Log = new StringBuilder();
            public int Ok;
            public int Errores;
        }
    }
}
