using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;

namespace ExportarXml
{
    /// <summary>
    /// Deshacer propio, sin depender del Ctrl+Z de TIA:
    /// - Antes de importar, cada objeto que se va a sobrescribir se exporta a XML y se apunta qué
    ///   objetos son nuevos. Todo va a Import_XML\deshacer_<fecha>\ con un manifiesto.
    /// - "XML > Deshacer última importación" borra los nuevos y restaura las copias en su carpeta.
    /// Funciona aunque se haya compilado, guardado o cerrado TIA.
    /// </summary>
    internal static class Deshacer
    {
        private const string Titulo = "Deshacer última importación";
        private const string Manifiesto = "_deshacer.txt";
        private const string Puntero = "ultima_importacion.txt";

        // ---------- Antes de importar ----------

        /// <summary>Copia lo que se va a sobrescribir y apunta lo nuevo. Devuelve la carpeta o null.</summary>
        public static string Preparar(TiaPortal tia, PlcSoftware plc, IEnumerable<string> objetos, StringBuilder log)
        {
            Project proyecto = tia.Projects.FirstOrDefault();
            if (proyecto == null) return null;

            string baseDir = Path.Combine(proyecto.Path.DirectoryName, "Import_XML");
            string carpeta = Path.Combine(baseDir, "deshacer_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(carpeta);

            List<string> lineas = new List<string> { "PLC=" + plc.Name, "FECHA=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            int copias = 0, nuevos = 0;
            foreach (string nombre in objetos.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string fichero = Limpio(nombre) + ".xml";
                    FileInfo f = new FileInfo(Path.Combine(carpeta, fichero));

                    PlcBlock b = Importador.BuscarBloque(plc.BlockGroup, nombre);
                    if (b != null)
                    {
                        if (!b.IsConsistent) { try { b.GetService<ICompilable>()?.Compile(); } catch { } }
                        b.Export(f, ExportOptions.WithDefaults);
                        lineas.Add("RESTAURAR|B|" + nombre + "|" + fichero + "|" + Ruta(b));
                        copias++;
                        continue;
                    }
                    PlcType t = Importador.BuscarTipo(plc.TypeGroup, nombre);
                    if (t != null)
                    {
                        t.Export(f, ExportOptions.WithDefaults);
                        lineas.Add("RESTAURAR|T|" + nombre + "|" + fichero + "|" + Ruta(t));
                        copias++;
                        continue;
                    }
                    PlcTagTable v = Importador.BuscarTabla(plc.TagTableGroup, nombre);
                    if (v != null)
                    {
                        v.Export(f, ExportOptions.WithDefaults);
                        lineas.Add("RESTAURAR|V|" + nombre + "|" + fichero + "|" + Ruta(v));
                        copias++;
                        continue;
                    }
                    lineas.Add("BORRAR|" + nombre);
                    nuevos++;
                }
                catch (Exception ex)
                {
                    log.AppendLine("Aviso: no se pudo copiar " + nombre + " para poder deshacer (" +
                                   ex.Message.Replace(Environment.NewLine, " ") + "); ese objeto no se podrá restaurar");
                }
            }

            File.WriteAllLines(Path.Combine(carpeta, Manifiesto), lineas, Encoding.UTF8);
            File.WriteAllText(Path.Combine(baseDir, Puntero), carpeta, Encoding.UTF8);
            log.AppendLine("Deshacer preparado: " + copias + " copia(s) y " + nuevos + " objeto(s) nuevo(s) en " + carpeta);
            return carpeta;
        }

        // ---------- Menú: deshacer ----------

        public static void Ejecutar(TiaPortal tia, PlcSoftware plc)
        {
            Project proyecto = tia.Projects.FirstOrDefault();
            if (proyecto == null || plc == null) return;

            string baseDir = Path.Combine(proyecto.Path.DirectoryName, "Import_XML");
            string puntero = Path.Combine(baseDir, Puntero);
            string carpeta = File.Exists(puntero) ? File.ReadAllText(puntero, Encoding.UTF8).Trim() : null;
            string manifiesto = carpeta != null ? Path.Combine(carpeta, Manifiesto) : null;
            if (manifiesto == null || !File.Exists(manifiesto))
            {
                Dialogos.Informar("No hay ninguna importación que deshacer.", Titulo, false);
                return;
            }

            string[] lineas = File.ReadAllLines(manifiesto, Encoding.UTF8);
            string plcOrigen = lineas.FirstOrDefault(l => l.StartsWith("PLC="))?.Substring(4) ?? "";
            string fecha = lineas.FirstOrDefault(l => l.StartsWith("FECHA="))?.Substring(6) ?? "";
            List<string> borrar = lineas.Where(l => l.StartsWith("BORRAR|")).Select(l => l.Substring(7)).ToList();
            List<string[]> restaurar = lineas.Where(l => l.StartsWith("RESTAURAR|")).Select(l => l.Split('|')).Where(p => p.Length >= 5).ToList();

            if (!string.Equals(plcOrigen, plc.Name, StringComparison.OrdinalIgnoreCase))
            {
                Dialogos.Informar("La última importación fue en el PLC \"" + plcOrigen + "\", no en \"" + plc.Name +
                                  "\". Selecciona ese PLC para deshacerla.", Titulo, true);
                return;
            }

            Respuesta r = Dialogos.Preguntar(
                "Deshacer la importación del " + fecha + " en \"" + plc.Name + "\":" + Environment.NewLine + Environment.NewLine +
                "Borrar " + borrar.Count + " objeto(s) nuevo(s): " + Resumen(borrar) + Environment.NewLine +
                "Restaurar " + restaurar.Count + " objeto(s) sobrescrito(s): " + Resumen(restaurar.Select(p => p[2])) + Environment.NewLine + Environment.NewLine +
                "Sí / Yes  →  deshacer" + Environment.NewLine + "No / Cancelar  →  dejarlo como está",
                Titulo);
            if (r != Respuesta.Si) return;

            StringBuilder log = new StringBuilder();
            string compilacion = "no realizada";
            int borrados = 0, restaurados = 0, errores = 0;

            using (ExclusiveAccess ea = tia.ExclusiveAccess("Deshaciendo la última importación..."))
            {
                // 1) Borrar lo nuevo: DB primero, luego el resto de bloques, tipos y tablas
                IEnumerable<string> orden = borrar.OrderBy(n => EsDb(plc, n) ? 0 : 1);
                foreach (string n in orden)
                {
                    try
                    {
                        PlcBlock b = Importador.BuscarBloque(plc.BlockGroup, n);
                        if (b != null) { b.Delete(); borrados++; log.AppendLine("BORRADO    " + n); continue; }
                        PlcType t = Importador.BuscarTipo(plc.TypeGroup, n);
                        if (t != null) { t.Delete(); borrados++; log.AppendLine("BORRADO    " + n); continue; }
                        PlcTagTable v = Importador.BuscarTabla(plc.TagTableGroup, n);
                        if (v != null) { v.Delete(); borrados++; log.AppendLine("BORRADO    " + n); continue; }
                        log.AppendLine("YA NO EXISTE " + n);
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        log.AppendLine("ERROR      " + n + " -> " + ex.Message.Replace(Environment.NewLine, " "));
                    }
                }

                // 2) Restaurar copias: tipos, tablas y bloques, con reintentos por dependencias
                List<string[]> pendientes = restaurar.OrderBy(p => p[1] == "T" ? 0 : p[1] == "V" ? 1 : 2).ToList();
                Dictionary<string, string> ultimoError = new Dictionary<string, string>();
                bool progreso = true;
                while (pendientes.Count > 0 && progreso)
                {
                    progreso = false;
                    foreach (string[] p in pendientes.ToList())
                    {
                        try
                        {
                            Restaurar(plc, p, carpeta);
                            pendientes.Remove(p);
                            restaurados++;
                            progreso = true;
                            log.AppendLine("RESTAURADO " + p[2]);
                        }
                        catch (Exception ex)
                        {
                            ultimoError[p[2]] = ex.Message.Replace(Environment.NewLine, " ");
                        }
                    }
                }
                foreach (string[] p in pendientes)
                {
                    errores++;
                    log.AppendLine("ERROR      " + p[2] + " -> " + ultimoError[p[2]] + "  (copia: " + Path.Combine(carpeta, p[3]) + ")");
                }

                ea.Text = "Compilando " + plc.Name + "...";
                compilacion = Importador.Compilar(plc, log);
            }

            // No se puede deshacer dos veces la misma importación
            try { File.Move(puntero, puntero + ".usado_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")); } catch { }

            string resumen = "Borrados: " + borrados + "   Restaurados: " + restaurados + "   Errores: " + errores + Environment.NewLine +
                             "Compilación: " + compilacion;
            string ruta = Path.Combine(carpeta, "_resultado.txt");
            File.WriteAllText(ruta, resumen + Environment.NewLine + Environment.NewLine + log, Encoding.UTF8);
            Dialogos.Informar(resumen + Environment.NewLine + Environment.NewLine + "Detalle en:" + Environment.NewLine + ruta,
                              Titulo, errores > 0);
        }

        private static void Restaurar(PlcSoftware plc, string[] p, string carpeta)
        {
            FileInfo f = new FileInfo(Path.Combine(carpeta, p[3]));
            string ruta = p[4];
            switch (p[1])
            {
                case "B":
                {
                    PlcBlock actual = Importador.BuscarBloque(plc.BlockGroup, p[2]);
                    PlcBlockGroup g = (actual?.Parent as PlcBlockGroup) ?? GrupoBloques(plc.BlockGroup, ruta);
                    g.Blocks.Import(f, ImportOptions.Override);
                    break;
                }
                case "T":
                {
                    PlcType actual = Importador.BuscarTipo(plc.TypeGroup, p[2]);
                    PlcTypeGroup g = (actual?.Parent as PlcTypeGroup) ?? GrupoTipos(plc.TypeGroup, ruta);
                    g.Types.Import(f, ImportOptions.Override);
                    break;
                }
                case "V":
                {
                    PlcTagTable actual = Importador.BuscarTabla(plc.TagTableGroup, p[2]);
                    PlcTagTableGroup g = (actual?.Parent as PlcTagTableGroup) ?? GrupoTablas(plc.TagTableGroup, ruta);
                    g.TagTables.Import(f, ImportOptions.Override);
                    break;
                }
            }
        }

        // Ruta de carpetas desde la raíz ("Funciones/Conversiones"), para restaurar en el mismo sitio
        private static string Ruta(IEngineeringObject o)
        {
            List<string> partes = new List<string>();
            for (IEngineeringObject x = o.Parent; x != null; x = x.Parent)
            {
                if (x is PlcBlockUserGroup gb) partes.Insert(0, gb.Name);
                else if (x is PlcTypeUserGroup gt) partes.Insert(0, gt.Name);
                else if (x is PlcTagTableUserGroup gv) partes.Insert(0, gv.Name);
                else break;
            }
            return string.Join("/", partes);
        }

        private static PlcBlockGroup GrupoBloques(PlcBlockGroup raiz, string ruta)
        {
            PlcBlockGroup g = raiz;
            foreach (string n in Partes(ruta))
            {
                PlcBlockGroup sig = g.Groups.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
                if (sig == null) break;
                g = sig;
            }
            return g;
        }

        private static PlcTypeGroup GrupoTipos(PlcTypeGroup raiz, string ruta)
        {
            PlcTypeGroup g = raiz;
            foreach (string n in Partes(ruta))
            {
                PlcTypeGroup sig = g.Groups.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
                if (sig == null) break;
                g = sig;
            }
            return g;
        }

        private static PlcTagTableGroup GrupoTablas(PlcTagTableGroup raiz, string ruta)
        {
            PlcTagTableGroup g = raiz;
            foreach (string n in Partes(ruta))
            {
                PlcTagTableGroup sig = g.Groups.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
                if (sig == null) break;
                g = sig;
            }
            return g;
        }

        private static IEnumerable<string> Partes(string ruta)
        {
            return (ruta ?? "").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool EsDb(PlcSoftware plc, string nombre)
        {
            PlcBlock b = Importador.BuscarBloque(plc.BlockGroup, nombre);
            return b != null && b.GetType().Name.EndsWith("DB", StringComparison.OrdinalIgnoreCase);
        }

        private static string Resumen(IEnumerable<string> nombres)
        {
            List<string> l = nombres.ToList();
            if (l.Count == 0) return "ninguno";
            return string.Join(", ", l.Take(8)) + (l.Count > 8 ? ", ..." : "");
        }

        private static string Limpio(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n;
        }
    }
}
