using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;

namespace ExportarXml
{
    /// <summary>
    /// Qué CPU tiene un PLC (referencia y firmware) y el fichero _info.txt de un paquete exportado.
    /// Sirve para decidir al importar si se usa el XML (misma CPU: copia exacta, con textos)
    /// o el SIMATIC SD (otra CPU: TIA pone la versión de cada instrucción).
    /// </summary>
    internal sealed class Cpu
    {
        public string Plc = "";
        public string Modelo = "";     // p. ej. CPU 1518-3 PN
        public string Referencia = ""; // p. ej. 6ES7 518-3AP00-0AB0
        public string Firmware = "";   // p. ej. V3.1

        public bool Conocida => Referencia.Length > 0;

        public bool Igual(Cpu otra)
        {
            return otra != null && Conocida && otra.Conocida &&
                   string.Equals(Referencia, otra.Referencia, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Firmware, otra.Firmware, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            if (!Conocida) return Plc + " (CPU desconocida)";
            return Plc + ": " + (Modelo.Length > 0 ? Modelo + ", " : "") + Referencia + " " + Firmware;
        }

        /// <summary>CPU del PLC: sube desde el software hasta el DeviceItem de la CPU.</summary>
        public static Cpu De(PlcSoftware plc)
        {
            Cpu c = new Cpu { Plc = plc?.Name ?? "" };
            if (plc == null) return c;
            try
            {
                for (IEngineeringObject x = plc.Parent; x != null; x = x.Parent)
                {
                    if (!(x is DeviceItem di)) continue;

                    // TypeIdentifier: "OrderNumber:6ES7 518-3AP00-0AB0/V3.1"
                    string tipo = di.TypeIdentifier ?? "";
                    if (tipo.StartsWith("OrderNumber:", StringComparison.OrdinalIgnoreCase))
                    {
                        string resto = tipo.Substring("OrderNumber:".Length);
                        int barra = resto.LastIndexOf('/');
                        c.Referencia = (barra >= 0 ? resto.Substring(0, barra) : resto).Trim();
                        c.Firmware = barra >= 0 ? resto.Substring(barra + 1).Trim() : "";
                    }
                    c.Modelo = Atributo(di, "TypeName");
                    if (c.Referencia.Length == 0) c.Referencia = Atributo(di, "OrderNumber");
                    if (c.Firmware.Length == 0) c.Firmware = Atributo(di, "FirmwareVersion");
                    if (c.Conocida) break;
                }
            }
            catch { /* sin datos de CPU: se trata como desconocida */ }
            return c;
        }

        private static string Atributo(IEngineeringObject o, string nombre)
        {
            try { return Convert.ToString(o.GetAttribute(nombre)) ?? ""; }
            catch { return ""; }
        }

        // ---------- _info.txt: una línea por carpeta exportada ----------
        // CARPETA=<primera carpeta bajo XML\ o SD\, vacío si el fichero va suelto>|<PLC>|<modelo>|<referencia>|<firmware>

        public const string FicheroInfo = "_info.txt";

        public static string LineaInfo(string carpeta, Cpu c)
        {
            return "CARPETA=" + carpeta + "|" + c.Plc + "|" + c.Modelo + "|" + c.Referencia + "|" + c.Firmware;
        }

        /// <summary>
        /// CPU de origen de un fichero de un paquete exportado (busca _info.txt hacia arriba).
        /// null si el fichero no viene de un paquete o no se sabe de qué PLC es.
        /// </summary>
        public static Cpu Origen(string fichero)
        {
            try
            {
                DirectoryInfo d = new FileInfo(fichero).Directory;
                for (int i = 0; d != null && i < 10; i++, d = d.Parent)
                {
                    string info = Path.Combine(d.FullName, FicheroInfo);
                    if (!File.Exists(info)) continue;

                    Dictionary<string, Cpu> mapa = new Dictionary<string, Cpu>(StringComparer.OrdinalIgnoreCase);
                    foreach (string l in File.ReadAllLines(info, Encoding.UTF8).Where(l => l.StartsWith("CARPETA=")))
                    {
                        string[] p = l.Substring(8).Split('|');
                        if (p.Length < 5) continue;
                        mapa[p[0]] = new Cpu { Plc = p[1], Modelo = p[2], Referencia = p[3], Firmware = p[4] };
                    }
                    if (mapa.Count == 0) return null;
                    if (mapa.Count == 1) return mapa.Values.First();

                    // Ruta relativa: XML\<carpeta>\... o SD\<carpeta>\...
                    string rel = fichero.Substring(d.FullName.Length).TrimStart('\\', '/');
                    string[] partes = rel.Split('\\', '/');
                    string clave = partes.Length > 2 ? partes[1] : "";
                    return mapa.TryGetValue(clave, out Cpu c) ? c : null;
                }
            }
            catch { }
            return null;
        }
    }
}
