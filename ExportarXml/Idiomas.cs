using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Siemens.Engineering;

namespace ExportarXml
{
    /// <summary>
    /// TIA rechaza un XML con textos en un idioma que el proyecto no tiene
    /// ("the specified culture does not exist within the current project").
    /// Antes de importar, se deja en el XML solo lo que el proyecto admite:
    /// - MultilingualTextItem (títulos, comentarios de bloque y red, textos de variables) y
    /// - MultiLanguageText Lang="…" (comentarios de miembros de la interfaz)
    /// en idiomas que no están en el proyecto se quitan. Si un texto se queda sin ningún idioma,
    /// se pasa al idioma de referencia del proyecto, para no perderlo.
    /// El fichero original no se toca: se importa una copia adaptada.
    /// </summary>
    internal static class Idiomas
    {
        private static HashSet<string> _activos;
        private static string _referencia;
        private static string _carpeta;

        /// <summary>Lee los idiomas del proyecto. Llamar una vez por importación.</summary>
        public static string Preparar(TiaPortal tia)
        {
            _activos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _referencia = null;
            _carpeta = null;
            Project p = tia.Projects.FirstOrDefault();
            if (p == null) return "Idiomas del proyecto: desconocidos";
            try
            {
                foreach (Language l in p.LanguageSettings.ActiveLanguages) _activos.Add(l.Culture.Name);
                _referencia = p.LanguageSettings.ReferenceLanguage?.Culture?.Name ?? _activos.FirstOrDefault();
                _carpeta = Path.Combine(p.Path.DirectoryName, "Import_XML", "adaptado_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            }
            catch { _activos.Clear(); }
            return _activos.Count == 0 ? "Idiomas del proyecto: no se pudieron leer (los XML se importan tal cual)"
                                       : "Idiomas del proyecto: " + string.Join(", ", _activos) + " (referencia " + _referencia + ")";
        }

        /// <summary>
        /// Devuelve el fichero a importar: el original si ya cuadra, o una copia sin los idiomas
        /// que el proyecto no tiene. En nota explica lo que se ha quitado (null si nada).
        /// </summary>
        public static FileInfo Adaptar(FileInfo xml, out string nota)
        {
            nota = null;
            if (_activos == null || _activos.Count == 0 || _carpeta == null) return xml;

            XDocument doc = XDocument.Load(xml.FullName, LoadOptions.PreserveWhitespace);
            HashSet<string> quitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool cambiado = false;

            // 1) MultilingualText > ObjectList > MultilingualTextItem > AttributeList > Culture
            foreach (XElement mt in doc.Descendants().Where(x => x.Name.LocalName == "MultilingualText").ToList())
            {
                List<XElement> items = mt.Descendants().Where(x => x.Name.LocalName == "MultilingualTextItem").ToList();
                List<XElement> malos = items.Where(i => !_activos.Contains(Cultura(i))).ToList();
                if (malos.Count == 0) continue;
                if (malos.Count == items.Count)
                {
                    // Ningún idioma válido: el primero pasa al de referencia y el resto fuera
                    XElement c = items[0].Descendants().First(x => x.Name.LocalName == "Culture");
                    quitados.Add(c.Value);
                    c.Value = _referencia;
                    malos.RemoveAt(0);
                }
                foreach (XElement m in malos) { quitados.Add(Cultura(m)); m.Remove(); }
                cambiado = true;
            }

            // 2) <Comment><MultiLanguageText Lang="es-ES">…</MultiLanguageText></Comment> de la interfaz
            foreach (XElement com in doc.Descendants().Where(x => x.Name.LocalName == "Comment" &&
                                                                 x.Elements().Any(e => e.Name.LocalName == "MultiLanguageText")).ToList())
            {
                List<XElement> textos = com.Elements().Where(e => e.Name.LocalName == "MultiLanguageText").ToList();
                List<XElement> malos = textos.Where(t => !_activos.Contains((string)t.Attribute("Lang") ?? "")).ToList();
                if (malos.Count == 0) continue;
                if (malos.Count == textos.Count)
                {
                    quitados.Add((string)textos[0].Attribute("Lang"));
                    textos[0].SetAttributeValue("Lang", _referencia);
                    malos.RemoveAt(0);
                }
                foreach (XElement m in malos) { quitados.Add((string)m.Attribute("Lang")); m.Remove(); }
                cambiado = true;
            }

            if (!cambiado) return xml;

            Directory.CreateDirectory(_carpeta);
            FileInfo copia = new FileInfo(Path.Combine(_carpeta, xml.Name));
            doc.Save(copia.FullName, SaveOptions.DisableFormatting);
            nota = "textos en " + string.Join(", ", quitados.Where(q => !string.IsNullOrEmpty(q))) +
                   " adaptados: el proyecto no tiene ese idioma (se importa la copia " + copia.FullName + ")";
            return copia;
        }

        private static string Cultura(XElement item)
        {
            return item.Descendants().FirstOrDefault(x => x.Name.LocalName == "Culture")?.Value ?? "";
        }
    }
}
