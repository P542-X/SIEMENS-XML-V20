using System;
using System.IO;
using System.Linq;
using System.Text;
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
    /// Exportar:
    /// Admite: PLC (dispositivo), Bloques de programa, carpetas, bloques (OB/FB/FC/DB),
    /// Tipos de datos PLC (UDT) y tablas de variables.
    /// Salida: <carpeta del proyecto>\Export_XML\<fecha_hora>\... + _resumen.txt
    /// Importar: ver Importador.cs
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
            raizMenu.Items.AddActionItem<IEngineeringObject>("Exportar selección a XML", OnExportar, OnEstado);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Importar ficheros XML / fuentes...", OnImportarFicheros, OnEstadoImportar);
            raizMenu.Items.AddActionItem<IEngineeringObject>("Importar carpeta XML / fuentes (con subcarpetas)...", OnImportarCarpeta, OnEstadoImportar);
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
                    Dialogos.Informar("No hay ficheros .xml, .scl, .udt, .db ni .awl en esa carpeta.", "Importar XML", true);
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
            Project proyecto = _tia.Projects.FirstOrDefault();
            if (proyecto == null) return;

            string raiz = Path.Combine(
                proyecto.Path.DirectoryName,
                "Export_XML",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(raiz);

            Resultado res = new Resultado();

            using (ExclusiveAccess ea = _tia.ExclusiveAccess("Exportando a XML..."))
            {
                foreach (IEngineeringObject obj in seleccion.GetSelection().OfType<IEngineeringObject>())
                {
                    if (ea.IsCancellationRequested) break;
                    Procesar(obj, raiz, ea, res);
                }
            }

            string cabecera =
                "Exportados: " + res.Ok + "   Errores: " + res.Errores + Environment.NewLine +
                "Carpeta: " + raiz + Environment.NewLine + Environment.NewLine;
            File.WriteAllText(Path.Combine(raiz, "_resumen.txt"), cabecera + res.Log, Encoding.UTF8);

            Dialogos.Informar(
                "Exportados: " + res.Ok + "   Errores: " + res.Errores + Environment.NewLine + Environment.NewLine +
                "Carpeta:" + Environment.NewLine + raiz,
                "Exportar XML",
                res.Errores > 0);
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
