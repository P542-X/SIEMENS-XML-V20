using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;

namespace ExportarXml
{
    /// <summary>
    /// Parte de texto de la exportación:
    /// - SIMATIC SD (.s7dcl...) de cada bloque: sin versiones de instrucción, vale para cualquier CPU.
    /// - Si SD no puede (bloques SCL en V20, bloques con redes de varios lenguajes...), y el bloque es SCL,
    ///   se intenta sacar como fuente externa .scl (GenerateSource) en FUENTES\.
    /// Lo que no sale por ninguno de los dos queda solo en XML, con el motivo en Motivos.
    /// </summary>
    internal sealed class ExportadorSd
    {
        public string RaizSd;
        public string RaizFuentes;
        public readonly StringBuilder Log = new StringBuilder();
        public int Ok, Fuentes, Errores;
        /// <summary>Bloque -> motivo por el que no hay SD (para _info.md).</summary>
        public readonly Dictionary<string, string> Motivos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool EsExportable(IEngineeringObject o)
        {
            return o is PlcBlock || o is PlcBlockGroup || o is PlcSoftware || o is Device || o is DeviceItem;
        }

        internal void Procesar(IEngineeringObject obj, string carpeta, ExclusiveAccess ea)
        {
            if (obj is PlcBlock bloque)
                Exportar(bloque, carpeta, ea);
            else if (obj is PlcBlockGroup grupo)
                Grupo(grupo, Sub(carpeta, grupo.Name), ea);
            else if (obj is PlcSoftware plc)
                Grupo(plc.BlockGroup, Sub(Sub(carpeta, plc.Name), "Bloques"), ea);
            else if (obj is Device dispositivo)
                foreach (DeviceItem di in dispositivo.DeviceItems) Procesar(di, carpeta, ea);
            else if (obj is DeviceItem item)
            {
                SoftwareContainer contenedor = item.GetService<SoftwareContainer>();
                if (contenedor?.Software is PlcSoftware sw)
                    Procesar(sw, carpeta, ea);
                else
                    foreach (DeviceItem hijo in item.DeviceItems) Procesar(hijo, carpeta, ea);
            }
        }

        private void Grupo(PlcBlockGroup grupo, string carpeta, ExclusiveAccess ea)
        {
            foreach (PlcBlock b in grupo.Blocks)
            {
                if (ea.IsCancellationRequested) return;
                Exportar(b, carpeta, ea);
            }
            foreach (PlcBlockUserGroup sub in grupo.Groups)
                Grupo(sub, Sub(carpeta, sub.Name), ea);
        }

        private void Exportar(PlcBlock bloque, string carpeta, ExclusiveAccess ea)
        {
            ea.Text = "Exportando texto de " + bloque.Name + "...";
            try
            {
                Directory.CreateDirectory(carpeta);
                foreach (string f in Directory.GetFiles(carpeta, bloque.Name + ".s7*")) File.Delete(f);

                DocumentExportResult r = bloque.ExportAsDocuments(new DirectoryInfo(carpeta), bloque.Name);
                if (r != null && r.State != DocumentResultState.Success)
                    throw new Exception("estado " + r.State);

                Ok++;
                Log.AppendLine("OK     SD     " + Path.Combine(carpeta, bloque.Name) + ".s7*");
            }
            catch (Exception ex)
            {
                string motivo = Motivo(ex.Message);
                if (EsScl(bloque) && GenerarFuente(bloque, carpeta, motivo)) return;

                Errores++;
                Motivos[bloque.Name] = motivo;
                Log.AppendLine("SOLO XML " + bloque.Name + " (" + motivo + ") -> " + ex.Message.Replace(Environment.NewLine, " "));
            }
        }

        private static bool EsScl(PlcBlock b)
        {
            try { return string.Equals(b.ProgrammingLanguage.ToString(), "SCL", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>Saca el bloque SCL como fuente .scl en FUENTES\ (misma ruta de carpetas que en SD\).</summary>
        private bool GenerarFuente(PlcBlock bloque, string carpetaSd, string motivoSd)
        {
            try
            {
                PlcSoftware plc = Importador.ObtenerPlc(bloque);
                if (plc == null) return false;
                string rel = carpetaSd.Length > RaizSd.Length ? carpetaSd.Substring(RaizSd.Length).TrimStart('\\', '/') : "";
                string carpeta = Path.Combine(RaizFuentes, rel);
                Directory.CreateDirectory(carpeta);
                FileInfo f = new FileInfo(Path.Combine(carpeta, Sub("", bloque.Name) + ".scl"));
                if (f.Exists) f.Delete();
                plc.ExternalSourceGroup.GenerateSource(new[] { bloque }, f, GenerateOptions.None);

                Fuentes++;
                Motivos[bloque.Name] = "SD no disponible (" + motivoSd + "): va como fuente .scl";
                Log.AppendLine("OK     FUENTE " + f.FullName);
                return true;
            }
            catch (Exception ex)
            {
                Log.AppendLine("       (no se pudo sacar " + bloque.Name + " como fuente .scl: " + ex.Message.Replace(Environment.NewLine, " ") + ")");
                return false;
            }
        }

        private static string Motivo(string mensaje)
        {
            string m = mensaje ?? "";
            if (m.IndexOf("mixed programming languages", StringComparison.OrdinalIgnoreCase) >= 0) return "SD no admite este bloque (varios lenguajes o SCL)";
            if (m.IndexOf("know-how", StringComparison.OrdinalIgnoreCase) >= 0) return "protegido con know-how";
            if (m.IndexOf("GRAPH", StringComparison.OrdinalIgnoreCase) >= 0) return "GRAPH";
            return "error al exportar SD";
        }

        private static string Sub(string carpeta, string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return carpeta.Length == 0 ? nombre : Path.Combine(carpeta, nombre);
        }
    }
}
