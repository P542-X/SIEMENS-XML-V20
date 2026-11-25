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
        private readonly TiaPortal _tiaInicial;
        private TiaPortal _tiaProyecto; // se guarda en un campo para que no lo finalice el recolector de basura

        public ExportarXmlAddIn(TiaPortal tia) : base("XML")
        {
            _tiaInicial = tia;
        }

        /// <summary>
        /// TiaPortal para trabajar. El que llega al constructor puede estar liberado aunque la sesión siga viva
        /// ("Access to a disposed object of type TiaPortal" en TiaPortal.Projects); entonces se usa el del
        /// proyecto de lo seleccionado (Project.Parent), que se toma en Preparar() al principio de cada acción.
        /// </summary>
        private TiaPortal _tia => Vivo(_tiaInicial) ? _tiaInicial : (_tiaProyecto ?? _tiaInicial);

        private static bool Vivo(TiaPortal t)
        {
            if (t == null) return false;
            try { return t.Projects.Count >= 0; }
            catch { return false; }
        }

        /// <summary>El proyecto de lo seleccionado (subiendo por Parent hasta Project, sin pasar de él).</summary>
        private static Project ProyectoDe(IEnumerable<IEngineeringObject> objetos)
        {
            foreach (IEngineeringObject o in objetos)
            {
                try
                {
                    for (IEngineeringObject x = o; x != null; x = Hmi.Padre(x))
                        if (x is Project p) return p;
                }
                catch { }
            }
            return null;
        }

        private void Preparar(MenuSelectionProvider seleccion)
        {
            if (Vivo(_tiaInicial)) return;
            if (Vivo(_tiaProyecto)) return;
            try
            {
                Project p = ProyectoDe(seleccion.GetSelection().OfType<IEngineeringObject>());
                object padre = p == null ? null : ((IEngineeringObject)p).Parent;
                _tiaProyecto = padre as TiaPortal;
                Apuntar("TiaPortal", new Exception("el del constructor está liberado; del proyecto: " +
                        (_tiaProyecto == null ? "no encontrado" : Vivo(_tiaProyecto) ? "vivo" : "también liberado")));
            }
            catch (Exception ex) { Apuntar("TiaPortal del proyecto", ex); }
        }

        private static string Detalle(Exception ex)
        {
            Exception e = ex is System.Reflection.TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            string pila = string.Join(Environment.NewLine, (e.StackTrace ?? "").Split('\n').Take(8).Select(l => l.TrimEnd()));
            return e.Message + Environment.NewLine + Environment.NewLine + "(" + e.GetType().Name + ")" + Environment.NewLine + pila +
                   Environment.NewLine + Environment.NewLine + "Últimos pasos del menú:" + Environment.NewLine + UltimasTrazas();
        }

        protected override void BuildContextMenuItems(ContextMenuAddInRoot raizMenu)
        {
            raizMenu.Items.AddActionItem<IEngineeringObject>("Exportar selección", OnExportar, OnEstado);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Exportar pantallas Unified...", OnExportarPantallas, OnEstadoPantallas);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Importar ficheros (xml, sd, fuentes, zip)...", OnImportarFicheros, OnEstadoImportar);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Importar carpeta...", OnImportarCarpeta, OnEstadoImportar);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Deshacer última importación", OnDeshacer, OnEstadoImportar);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Diagnóstico de la selección", OnDiagnostico, OnEstadoDiagnostico);
        }

        // ---------- Importación ----------

        private IEngineeringObject DestinoImportacion(MenuSelectionProvider seleccion) => DestinoImportacionSegura(seleccion);

        // ---------- Diagnóstico ----------
        // Sale solo cuando no sale nada más (lo seleccionado no se puede exportar ni es destino de importación):
        // dice qué tipo de objeto da TIA y hasta dónde llega su Parent, para poder añadirlo.

        private MenuStatus OnEstadoDiagnostico(MenuSelectionProvider seleccion)
        {
            Traza("OnEstadoDiagnostico", seleccion);
            try
            {
                IEngineeringObject[] sel = seleccion.GetSelection().OfType<IEngineeringObject>().ToArray();
                if (sel.Length == 0) return MenuStatus.Hidden;
                return sel.Any(EsExportableSeguro) || DestinoImportacionSegura(seleccion) != null ? MenuStatus.Hidden : MenuStatus.Enabled;
            }
            catch (Exception ex) { Apuntar("Diagnóstico", ex); return MenuStatus.Enabled; }
        }

        /// <summary>Errores al decidir qué opciones del menú se ven (TIA las oculta si salta una excepción).</summary>
        // En Documentos: el Add-In corre en otro proceso y su %TEMP% puede no ser el del usuario.
        private static readonly string LogMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ExportarXml_menu.log");

        // Traza en memoria de lo que ha hecho el menú (sale en los mensajes de error) y en LogMenu.
        private static readonly List<string> _traza = new List<string>();

        private static void Traza(string texto)
        {
            string linea = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + texto;
            lock (_traza) { _traza.Add(linea); if (_traza.Count > 40) _traza.RemoveAt(0); }
            try { File.AppendAllText(LogMenu, linea + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        private void Traza(string donde, MenuSelectionProvider seleccion)
        {
            string tipos = "";
            try { tipos = string.Join(", ", seleccion.GetSelection().Select(o => o?.GetType().Name ?? "null").Distinct()); } catch (Exception ex) { tipos = "selección ilegible: " + ex.GetType().Name; }
            Traza(donde + " · " + tipos + " · TiaPortal " + (Vivo(_tiaInicial) ? "vivo" : "LIBERADO"));
        }

        private static string UltimasTrazas()
        {
            lock (_traza) return string.Join(Environment.NewLine, _traza.Skip(Math.Max(0, _traza.Count - 15)));
        }

        private static void Apuntar(string donde, Exception ex)
        {
            Traza("EXCEPCIÓN en " + donde + ": " + ex.GetType().Name + ": " + ex.Message);
        }

        private static bool EsExportableSeguro(IEngineeringObject o)
        {
            try { return EsExportable(o); } catch (Exception ex) { Apuntar("EsExportable " + o?.GetType().FullName, ex); return false; }
        }

        private IEngineeringObject DestinoImportacionSegura(MenuSelectionProvider seleccion)
        {
            foreach (IEngineeringObject o in seleccion.GetSelection().OfType<IEngineeringObject>())
            {
                // cada comprobación por separado: si una falla (p. ej. la del PLC en un panel), se prueba la siguiente
                try { if (Importador.EsDestino(o)) return o; }
                catch (Exception ex) { Apuntar("Destino " + o?.GetType().FullName, ex); }
                try { if (HmiUnified.Software(_tia, o) != null) return o; }
                catch (Exception ex) { Apuntar("Destino Unified " + o?.GetType().FullName, ex); }
            }
            return null;
        }

        private void OnDiagnostico(MenuSelectionProvider seleccion)
        {
            Traza("OnDiagnostico", seleccion);
            Preparar(seleccion);
            try
            {
                Dialogos.Informar("Lo seleccionado no se puede exportar ni importar con esta versión del Add-In." + Environment.NewLine +
                                  "Pasa este texto (Ctrl+C copia el mensaje) para añadirlo:" + Environment.NewLine + Environment.NewLine +
                                  HmiUnified.Diagnostico(_tia, seleccion.GetSelection().Cast<object>()) +
                                  (File.Exists(LogMenu) ? "Errores del menú (" + LogMenu + "):" + Environment.NewLine +
                                      string.Join(Environment.NewLine, File.ReadAllLines(LogMenu).Reverse().Take(30).Reverse()) : ""), "Diagnóstico", false);
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error en el diagnóstico:" + Environment.NewLine + Detalle(ex), "Diagnóstico", true);
            }
        }

        private MenuStatus OnEstadoImportar(MenuSelectionProvider seleccion)
        {
            Traza("OnEstadoImportar", seleccion);
            try { return DestinoImportacion(seleccion) != null ? MenuStatus.Enabled : MenuStatus.Hidden; }
            catch (Exception ex) { Apuntar("Importar", ex); return MenuStatus.Hidden; }
        }

        private void OnImportarFicheros(MenuSelectionProvider seleccion)
        {
            Traza("OnImportarFicheros", seleccion);
            Preparar(seleccion);
            try
            {
                IEngineeringObject destino = DestinoImportacion(seleccion);
                if (destino == null) return;
                string[] ficheros = Dialogos.ElegirFicheros();
                if (ficheros == null || ficheros.Length == 0) return;
                ImportarEn(destino, ficheros);
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al importar:" + Environment.NewLine + Detalle(ex), "Importar XML", true);
            }
        }

        /// <summary>PLC → importador de PLC; HMI clásico → importador de HMI (XML); WinCC Unified → pantallas en JSON.</summary>
        private void ImportarEn(IEngineeringObject destino, string[] ficheros)
        {
            if (Importador.ObtenerPlc(destino) != null) { Importador.Ejecutar(_tia, destino, ficheros); return; }
            object hmi = Hmi.ObtenerHmi(destino);
            if (hmi != null) { Hmi.Importar(_tia, hmi, ficheros); return; }
            object unified = HmiUnified.Software(_tia, destino);
            if (unified != null) HmiUnified.Importar(_tia, unified, ficheros);
        }

        private void OnDeshacer(MenuSelectionProvider seleccion)
        {
            Traza("OnDeshacer", seleccion);
            Preparar(seleccion);
            try
            {
                IEngineeringObject destino = DestinoImportacion(seleccion);
                if (destino == null) return;
                if (Importador.ObtenerPlc(destino) == null)
                {
                    Dialogos.Informar("El deshacer solo está disponible para el PLC. En el HMI, la copia de lo sobrescrito queda en Import_XML\\copia_hmi_<fecha>.", "Deshacer", false);
                    return;
                }
                Deshacer.Ejecutar(_tia, Importador.ObtenerPlc(destino));
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al deshacer:" + Environment.NewLine + Detalle(ex), "Deshacer", true);
            }
        }

        private void OnImportarCarpeta(MenuSelectionProvider seleccion)
        {
            Traza("OnImportarCarpeta", seleccion);
            Preparar(seleccion);
            try
            {
                IEngineeringObject destino = DestinoImportacion(seleccion);
                if (destino == null) return;
                string carpeta = Dialogos.ElegirCarpeta();
                if (string.IsNullOrEmpty(carpeta)) return;
                // Los .zip NO se cogen al importar una carpeta: en una carpeta con varios paquetes
                // (alternativas, pruebas por partes...) se importarían todos mezclados. Un .zip se
                // importa eligiéndolo con "Importar ficheros".
                string[] ficheros = Directory.GetFiles(carpeta, "*.*", SearchOption.AllDirectories)
                    .Where(f => Importador.ExtensionesImportables.Contains(Path.GetExtension(f).ToLowerInvariant()) &&
                                !string.Equals(Path.GetExtension(f), ".zip", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (ficheros.Length == 0)
                {
                    Dialogos.Informar("No hay ficheros .xml, fuentes (.scl, .udt, .db, .awl), documentos SIMATIC SD (.s7dcl, .s7dcf, .s7dcs, .s7dbd) ni pantallas Unified (.json) en esa carpeta.", "Importar XML", true);
                    return;
                }
                ImportarEn(destino, ficheros);
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al importar:" + Environment.NewLine + Detalle(ex), "Importar XML", true);
            }
        }

        // ---------- Exportación ----------

        private MenuStatus OnEstado(MenuSelectionProvider seleccion)
        {
            Traza("OnEstado", seleccion);
            try
            {
                return seleccion.GetSelection().OfType<IEngineeringObject>().Any(EsExportableSeguro)
                    ? MenuStatus.Enabled
                    : MenuStatus.Hidden;
            }
            catch (Exception ex) { Apuntar("Exportar", ex); return MenuStatus.Hidden; }
        }

        private static bool EsExportable(IEngineeringObject o)
        {
            return o is PlcBlock || o is PlcBlockGroup
                || o is PlcType || o is PlcTypeGroup
                || o is PlcTagTable || o is PlcTagTableGroup
                || o is PlcSoftware || o is Device || o is DeviceItem
                || HmiUnified.EsSeleccionable(o);
        }

        private void OnExportar(MenuSelectionProvider seleccion)
        {
            Traza("OnExportar", seleccion);
            Preparar(seleccion);
            try
            {
                Exportar(seleccion.GetSelection().OfType<IEngineeringObject>().ToArray());
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al exportar:" + Environment.NewLine + Detalle(ex), "Exportar", true);
            }
        }

        // ---------- Pantallas Unified elegidas ----------
        // TIA no pasa al Add-In las pantallas Unified seleccionadas en el árbol (el menú sale vacío),
        // así que se elige el panel y aquí se marcan las pantallas.

        private object PanelUnified(MenuSelectionProvider seleccion)
        {
            foreach (IEngineeringObject o in seleccion.GetSelection().OfType<IEngineeringObject>())
            {
                try { if (Hmi.ObtenerHmi(o) == null) { object sw = HmiUnified.Software(_tia, o); if (sw != null) return sw; } }
                catch (Exception ex) { Apuntar("Pantallas " + o?.GetType().FullName, ex); }
            }
            return null;
        }

        private MenuStatus OnEstadoPantallas(MenuSelectionProvider seleccion)
        {
            Traza("OnEstadoPantallas", seleccion);
            try { return PanelUnified(seleccion) != null ? MenuStatus.Enabled : MenuStatus.Hidden; }
            catch (Exception ex) { Apuntar("Pantallas", ex); return MenuStatus.Hidden; }
        }

        private void OnExportarPantallas(MenuSelectionProvider seleccion)
        {
            Traza("OnExportarPantallas", seleccion);
            Preparar(seleccion);
            try
            {
                object sw = PanelUnified(seleccion);
                if (sw == null) return;
                List<string> todas = HmiUnified.ListaPantallas(sw);
                if (todas.Count == 0) { Dialogos.Informar("El panel no tiene pantallas.", "Exportar pantallas Unified", false); return; }
                List<string> elegidas = Dialogos.ElegirVarios("Exportar pantallas Unified · " + HmiUnified.Nombre(sw),
                    "Marca las pantallas que quieres exportar (grupo/pantalla). Se puede filtrar escribiendo en Buscar.", todas);
                if (elegidas == null) return;
                if (elegidas.Count == 0) { Dialogos.Informar("No has marcado ninguna pantalla.", "Exportar pantallas Unified", false); return; }
                Exportar(new[] { (IEngineeringObject)seleccion.GetSelection().OfType<IEngineeringObject>().First() }, new HashSet<string>(elegidas, StringComparer.Ordinal));
            }
            catch (Exception ex)
            {
                Dialogos.Informar("Error inesperado al exportar:" + Environment.NewLine + Detalle(ex), "Exportar pantallas Unified", true);
            }
        }

        private void Exportar(IEngineeringObject[] objetos, ICollection<string> pantallasElegidas = null)
        {
            Project proyecto = ProyectoDe(objetos);
            Traza("Exportar: proyecto de la selección " + (proyecto == null ? "NO encontrado" : "encontrado") + " · TiaPortal " + (Vivo(_tiaInicial) ? "vivo" : "LIBERADO"));
            if (proyecto == null) proyecto = _tia.Projects.FirstOrDefault();
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
            List<string> paneles = new List<string>();
            List<string> panelesUnified = new List<string>();
            Dictionary<string, int> recuentoUnified = new Dictionary<string, int>();
            Dictionary<string, int> recuentoHmi = new Dictionary<string, int>();
            StringBuilder logHmi = new StringBuilder();
            int okHmi = 0, errHmi = 0;
            List<string> info = new List<string>();
            List<Cpu> cpus = new List<Cpu>();

            using (ExclusiveAccess ea = _tia.ExclusiveAccess("Exportando..."))
            {
                foreach (IEngineeringObject obj in objetos.Where(EsExportableSeguro))
                {
                    if (ea.IsCancellationRequested) break;
                    Traza("Exportar " + obj.GetType().Name + " · TiaPortal " + (Vivo(_tiaInicial) ? "vivo" : "LIBERADO"));

                    // CPU de origen de lo que se exporta (para el importador y para _info.md)
                    PlcSoftware plc = Importador.ObtenerPlc(obj);
                    Cpu cpu = Cpu.De(plc);
                    info.Add(Cpu.LineaInfo(CarpetaRaiz(obj), cpu));
                    if (plc != null && !cpus.Any(c => c.Plc == cpu.Plc)) cpus.Add(cpu);

                    Procesar(obj, carpetaXml, ea, res);
                    if (ExportadorSd.EsExportable(obj))
                        sd.Procesar(obj, carpetaSd, ea);

                    // HMI clásico del objeto seleccionado (dispositivo o panel)
                    object hmi = Hmi.ObtenerHmi(obj);
                    if (hmi != null && !paneles.Contains(Hmi.Panel(hmi)))
                    {
                        paneles.Add(Hmi.Panel(hmi));
                        Hmi.Exportar(hmi, Path.Combine(raiz, "HMI"), ea, logHmi, recuentoHmi, ref okHmi, ref errHmi);
                    }
                    // WinCC Unified: pantallas en JSON
                    // (dispositivo o software completo, o solo la pantalla / grupo de pantallas seleccionado)
                    object unified = hmi == null ? HmiUnified.Software(_tia, obj) : null;
                    object filtro = HmiUnified.EsPantallaOGrupo(obj) ? obj : null;
                    string clave = unified == null ? null : HmiUnified.Panel(_tia, unified) + (filtro != null ? " › " + HmiUnified.Nombre(filtro) : "");
                    if (unified != null && !panelesUnified.Contains(clave) && (filtro != null || !panelesUnified.Contains(HmiUnified.Panel(_tia, unified))))
                    {
                        panelesUnified.Add(clave);
                        HmiUnified.Exportar(unified, Path.Combine(raiz, "HMI_Unified"), ea, logHmi, recuentoUnified, ref okHmi, ref errHmi, filtro,
                                            filtro == null ? pantallasElegidas : null, _tia);
                    }
                    else if (unified == null && hmi == null && HmiUnified.EsObjetoUnified(obj))
                    {
                        errHmi++;
                        logHmi.AppendLine("ERROR  UNIF   " + HmiUnified.Nombre(obj) + ": no se encuentra su panel Unified. Exporta seleccionando el panel (HMI_RT_x).");
                    }
                }
            }

            File.WriteAllLines(Path.Combine(raiz, Cpu.FicheroInfo), info.Distinct(), Encoding.UTF8);
            EscribirInstruccionesIa(raiz);
            File.WriteAllText(Path.Combine(raiz, "_info.md"), InfoMd(proyecto, cpus, carpetaXml, carpetaSd, carpetaFuentes, sd) + InfoHmi(paneles, recuentoHmi) + InfoUnified(panelesUnified, recuentoUnified), Encoding.UTF8);
            File.WriteAllText(Path.Combine(raiz, "_resumen.txt"),
                "XML     exportados: " + res.Ok + "   errores: " + res.Errores + Environment.NewLine +
                "SD      exportados: " + sd.Ok + Environment.NewLine +
                "FUENTES exportadas: " + sd.Fuentes + " (bloques SCL que SD no admite)" + Environment.NewLine +
                "Solo XML: " + sd.Errores + " bloques (ver _info.md, columna Texto)" + Environment.NewLine +
                "Carpeta: " + raiz + Environment.NewLine + Environment.NewLine +
                "---- XML ----" + Environment.NewLine + res.Log + Environment.NewLine +
                "---- SD / FUENTES ----" + Environment.NewLine + sd.Log + Environment.NewLine +
                "---- HMI ----" + Environment.NewLine + "Exportados: " + okHmi + "   errores: " + errHmi + Environment.NewLine + logHmi, Encoding.UTF8);

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
                "    Solo XML: " + sd.Errores + (paneles.Count + panelesUnified.Count > 0 ? "    HMI: " + okHmi + " (errores " + errHmi + ")" : "") + Environment.NewLine +
                string.Join(Environment.NewLine, cpus.Select(c => "CPU " + c)) + Environment.NewLine + Environment.NewLine +
                (zip != null ? "Paquete:" + Environment.NewLine + zip : "Carpeta:" + Environment.NewLine + raiz) + avisoZip +
                Environment.NewLine + Environment.NewLine +
                "Para trabajar con una IA: pásale el .zip y dile que lea primero INSTRUCCIONES_IA.md.",
                "Exportar",
                res.Errores > 0);
        }

        private static string InfoHmi(List<string> paneles, Dictionary<string, int> recuento)
        {
            if (paneles.Count == 0) return "";
            StringBuilder md = new StringBuilder();
            md.AppendLine();
            md.AppendLine("## HMI clásico (" + paneles.Count + ")");
            md.AppendLine();
            foreach (string p in paneles) md.AppendLine("- " + p);
            md.AppendLine();
            md.AppendLine("En `HMI/<panel>/`: " + string.Join(", ", recuento.Select(k => k.Key + " " + k.Value)) + ".");
            md.AppendLine("Para importar algo en el HMI, se elige el panel (no el PLC) en *Importar ficheros*.");
            return md.ToString();
        }

        private static string InfoUnified(List<string> paneles, Dictionary<string, int> recuento)
        {
            if (paneles.Count == 0) return "";
            StringBuilder md = new StringBuilder();
            md.AppendLine();
            md.AppendLine("## HMI WinCC Unified (" + paneles.Count + ")");
            md.AppendLine();
            foreach (string p in paneles) md.AppendLine("- " + p);
            md.AppendLine();
            md.AppendLine("En `HMI_Unified/<panel>/`: " + string.Join(", ", recuento.Select(k => k.Key + " " + k.Value)) + ".");
            md.AppendLine("Pantallas en JSON (`Pantallas/<grupo>/<pantalla>.json`, formato en INSTRUCCIONES_IA.md 6.8). `Variables_referencia.json` es solo para consultar nombres.");
            md.AppendLine("Para importar pantallas, se elige el panel Unified (no el PLC) en *Importar ficheros*.");
            return md.ToString();
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
            try
            {
                md.AppendLine("| Idiomas del proyecto | " + string.Join(", ", proyecto.LanguageSettings.ActiveLanguages.Select(l => l.Culture.Name)) +
                              " (referencia " + proyecto.LanguageSettings.ReferenceLanguage?.Culture?.Name + ") |");
            }
            catch { }
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
