using System;
using System.IO;
using System.Linq;
using System.Text;
using Siemens.Engineering;

namespace ExportarXml
{
    /// <summary>
    /// Registro paso a paso de la importación, escrito en disco ANTES de cada operación.
    /// Si TIA Portal se cierra de golpe, la última línea de Import_XML\progreso.txt dice
    /// qué fichero o qué paso lo provocó (el resumen normal no llega a escribirse).
    /// </summary>
    internal static class Progreso
    {
        private static string _fichero;

        public static void Iniciar(TiaPortal tia)
        {
            _fichero = null;
            try
            {
                Project p = tia.Projects.FirstOrDefault();
                if (p == null) return;
                string dir = Path.Combine(p.Path.DirectoryName, "Import_XML");
                Directory.CreateDirectory(dir);
                _fichero = Path.Combine(dir, "progreso.txt");
                File.WriteAllText(_fichero, "Importación iniciada " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                                  "Si TIA se ha cerrado, el último IMPORTANDO sin OK ni ERROR es el que lo ha provocado." + Environment.NewLine,
                                  Encoding.UTF8);
            }
            catch { _fichero = null; }
        }

        public static void Paso(string texto)
        {
            if (_fichero == null) return;
            try { File.AppendAllText(_fichero, DateTime.Now.ToString("HH:mm:ss") + "  " + texto + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
