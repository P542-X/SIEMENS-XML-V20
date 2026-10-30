using System;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;

namespace ExportarXml
{
    /// <summary>
    /// Exporta bloques a SIMATIC SD (V20): .s7dcl (KOP), .s7dcf (FUP, desde V20 Update 4),
    /// .s7dcs (SCL), .s7dbd (DB), con un .s7res para los textos.
    /// En SD las instrucciones de KOP se escriben por nombre (Move(in := 42, out1 => #x)) y TIA
    /// resuelve la versión al importar: es la forma más segura de generar KOP con cualquier
    /// instrucción, y lo exportado sirve de catálogo legible de sintaxis.
    /// Salida: <carpeta del proyecto>\Export_SD\<fecha_hora>\... + _resumen.txt
    /// </summary>
    internal static class ExportadorSd
    {
        private const string Titulo = "Exportar SIMATIC SD";

        public static bool EsExportable(IEngineeringObject o)
        {
            return o is PlcBlock || o is PlcBlockGroup || o is PlcSoftware || o is Device || o is DeviceItem;
        }

        public static void Ejecutar(TiaPortal tia, IEngineeringObject[] seleccion)
        {
            Project proyecto = tia.Projects.FirstOrDefault();
            if (proyecto == null) return;

            string raiz = Path.Combine(proyecto.Path.DirectoryName, "Export_SD", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(raiz);

            StringBuilder log = new StringBuilder();
            int ok = 0, errores = 0;

            using (ExclusiveAccess ea = tia.ExclusiveAccess("Exportando a SIMATIC SD..."))
            {
                foreach (IEngineeringObject obj in seleccion)
                {
                    if (ea.IsCancellationRequested) break;
                    Procesar(obj, raiz, ea, log, ref ok, ref errores);
                }
            }

            string cabecera = "Exportados: " + ok + "   Errores: " + errores + Environment.NewLine +
                              "Carpeta: " + raiz + Environment.NewLine + Environment.NewLine;
            File.WriteAllText(Path.Combine(raiz, "_resumen.txt"), cabecera + log, Encoding.UTF8);

            Dialogos.Informar(
                "Exportados: " + ok + "   Errores: " + errores + Environment.NewLine + Environment.NewLine +
                "Carpeta:" + Environment.NewLine + raiz,
                Titulo, errores > 0);
        }

        private static void Procesar(IEngineeringObject obj, string carpeta, ExclusiveAccess ea,
                                     StringBuilder log, ref int ok, ref int errores)
        {
            if (obj is PlcBlock bloque)
                Exportar(bloque, carpeta, ea, log, ref ok, ref errores);
            else if (obj is PlcBlockGroup grupo)
                Grupo(grupo, Sub(carpeta, grupo.Name), ea, log, ref ok, ref errores);
            else if (obj is PlcSoftware plc)
                Grupo(plc.BlockGroup, Sub(Sub(carpeta, plc.Name), "Bloques"), ea, log, ref ok, ref errores);
            else if (obj is Device dispositivo)
                foreach (DeviceItem di in dispositivo.DeviceItems) Procesar(di, carpeta, ea, log, ref ok, ref errores);
            else if (obj is DeviceItem item)
            {
                SoftwareContainer contenedor = item.GetService<SoftwareContainer>();
                if (contenedor?.Software is PlcSoftware sw)
                    Procesar(sw, carpeta, ea, log, ref ok, ref errores);
                else
                    foreach (DeviceItem hijo in item.DeviceItems) Procesar(hijo, carpeta, ea, log, ref ok, ref errores);
            }
        }

        private static void Grupo(PlcBlockGroup grupo, string carpeta, ExclusiveAccess ea,
                                  StringBuilder log, ref int ok, ref int errores)
        {
            foreach (PlcBlock b in grupo.Blocks)
            {
                if (ea.IsCancellationRequested) return;
                Exportar(b, carpeta, ea, log, ref ok, ref errores);
            }
            foreach (PlcBlockUserGroup sub in grupo.Groups)
                Grupo(sub, Sub(carpeta, sub.Name), ea, log, ref ok, ref errores);
        }

        private static void Exportar(PlcBlock bloque, string carpeta, ExclusiveAccess ea,
                                     StringBuilder log, ref int ok, ref int errores)
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                ea.Text = "Exportando " + bloque.Name + "...";

                // Si ya hay ficheros de ese bloque (otra exportación en la misma carpeta), fuera
                foreach (string f in Directory.GetFiles(carpeta, bloque.Name + ".s7*")) File.Delete(f);

                DocumentExportResult r = bloque.ExportAsDocuments(new DirectoryInfo(carpeta), bloque.Name);
                if (r != null && r.State != DocumentResultState.Success)
                    throw new Exception("estado " + r.State);

                ok++;
                log.AppendLine("OK     " + Path.Combine(carpeta, bloque.Name) + ".s7*");
            }
            catch (Exception ex)
            {
                // STL, GRAPH, bloques protegidos o tipos que SD no admite dan error aquí
                errores++;
                log.AppendLine("ERROR  " + bloque.Name + " -> " + ex.Message);
            }
        }

        private static string Sub(string carpeta, string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return Path.Combine(carpeta, nombre);
        }
    }
}
