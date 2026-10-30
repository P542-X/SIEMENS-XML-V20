using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ExportarXml
{
    /// <summary>
    /// Comprueba un XML SimaticML antes de importarlo, para no gastar una importación en un
    /// fichero que TIA va a rechazar:
    /// 1) Reglas aprendidas de errores reales de importación en V20 (ver SimaticML_V20_lecciones.md).
    ///    Si alguna falla, el fichero NO se importa.
    /// 2) Validación contra los XSD que instala TIA Openness (PublicAPI\V20\Schemas), si se encuentran.
    ///    Sus mensajes son AVISOS: se apuntan con línea y columna, pero el fichero se intenta importar
    ///    igual, para que un falso positivo del XSD no bloquee un fichero que TIA sí acepta.
    /// </summary>
    internal static class Validador
    {
        private const int MaxMensajes = 10;

        internal sealed class Resultado
        {
            public readonly List<string> Errores = new List<string>();
            public readonly List<string> Avisos = new List<string>();
        }

        public static Resultado Comprobar(string ruta)
        {
            Resultado res = new Resultado();

            XDocument doc;
            try
            {
                doc = XDocument.Load(ruta, LoadOptions.SetLineInfo);
            }
            catch (Exception ex)
            {
                res.Errores.Add("XML mal formado: " + ex.Message);
                return res;
            }

            Reglas(doc, res.Errores);
            Esquemas(ruta, res.Avisos);

            Recortar(res.Errores);
            Recortar(res.Avisos);
            return res;
        }

        private static void Recortar(List<string> lista)
        {
            if (lista.Count > MaxMensajes)
            {
                int resto = lista.Count - MaxMensajes;
                lista.RemoveRange(MaxMensajes, resto);
                lista.Add("... y " + resto + " más");
            }
        }

        /// <summary>Texto para el resumen: si se ha validado contra los XSD y desde dónde.</summary>
        public static string EstadoEsquemas()
        {
            CargarEsquemas();
            return _esquemas != null
                ? "XSD de TIA: " + _carpetaEsquemas + " (" + _ficherosEsquema + " ficheros)"
                : "XSD de TIA: no usados (" + _motivoSinEsquemas + "). Solo se aplican las reglas propias.";
        }

        // ---------- 1) Reglas propias ----------

        private static void Reglas(XDocument doc, List<string> errores)
        {
            XElement raiz = doc.Root;
            if (raiz == null || raiz.Name.LocalName != "Document") return;

            // Versión del fichero: un SimaticML de una versión mayor no se puede importar en V20
            XElement engineering = raiz.Elements().FirstOrDefault(x => x.Name.LocalName == "Engineering");
            string version = (string)engineering?.Attribute("version");
            Match m = version == null ? Match.Empty : Regex.Match(version, @"^V(\d+)");
            if (m.Success && int.Parse(m.Groups[1].Value) > 20)
                errores.Add("El fichero es de TIA " + version + ": TIA V20 solo importa SimaticML de V20 o anteriores.");

            // Los ID de ObjectList (en hexadecimal) no se pueden repetir dentro del documento
            foreach (IGrouping<string, XAttribute> g in doc.Descendants()
                         .Select(x => x.Attribute("ID")).Where(a => a != null)
                         .GroupBy(a => a.Value.ToUpperInvariant()).Where(g => g.Count() > 1))
            {
                errores.Add(Linea(g.Skip(1).First().Parent) + "ID=\"" + g.Key + "\" repetido.");
            }

            XElement bloque = raiz.Elements().FirstOrDefault(x => x.Name.LocalName.StartsWith("SW.Blocks."));
            if (bloque == null) return;

            List<XElement> unidades = bloque.Descendants()
                .Where(x => x.Name.LocalName == "SW.Blocks.CompileUnit").ToList();

            // Un bloque SCL solo admite una CompileUnit
            string lenguaje = bloque.Elements().FirstOrDefault(x => x.Name.LocalName == "AttributeList")?
                                    .Elements().FirstOrDefault(x => x.Name.LocalName == "ProgrammingLanguage")?.Value;
            if (lenguaje == "SCL" && unidades.Count > 1)
                errores.Add("Bloque SCL con " + unidades.Count + " CompileUnit: en SCL solo se admite una " +
                            "(\"The programming language of 'SCL' does not support the use of multiple compilation units\").");

            foreach (XElement cu in unidades)
            {
                // UId únicos dentro de cada red (IdentCon y NameCon no cuentan: son referencias a un UId)
                foreach (IGrouping<string, XAttribute> g in cu.Descendants()
                             .Where(x => x.Name.LocalName != "IdentCon" && x.Name.LocalName != "NameCon")
                             .Select(x => x.Attribute("UId")).Where(a => a != null)
                             .GroupBy(a => a.Value).Where(g => g.Count() > 1))
                {
                    errores.Add(Linea(g.Skip(1).First().Parent) + "UId=\"" + g.Key + "\" repetido en la misma red.");
                }

                // En SCL tokenizado el índice de un array va con tokens [ ], sin AccessModifier
                foreach (XElement st in cu.Descendants().Where(x => x.Name.LocalName == "StructuredText"))
                {
                    foreach (XElement c in st.Descendants().Where(x => x.Attribute("AccessModifier") != null))
                    {
                        errores.Add(Linea(c) + "AccessModifier en una red SCL (" + (string)c.Attribute("Name") +
                                    "): TIA no lo admite; el índice va solo con <Token Text=\"[\"/> y <Token Text=\"]\"/>.");
                    }
                }

                // En KOP/FUP cada IdentCon y NameCon tiene que apuntar a un UId que exista en Parts
                foreach (XElement flg in cu.Descendants().Where(x => x.Name.LocalName == "FlgNet"))
                {
                    HashSet<string> uids = new HashSet<string>(
                        flg.Elements().Where(x => x.Name.LocalName == "Parts").Elements()
                           .Select(x => (string)x.Attribute("UId")).Where(u => u != null));
                    foreach (XElement con in flg.Descendants()
                                 .Where(x => x.Name.LocalName == "IdentCon" || x.Name.LocalName == "NameCon"))
                    {
                        string u = (string)con.Attribute("UId");
                        if (u != null && !uids.Contains(u))
                            errores.Add(Linea(con) + con.Name.LocalName + " apunta a UId=\"" + u + "\", que no está en Parts.");
                    }
                }
            }
        }

        private static string Linea(XElement e)
        {
            IXmlLineInfo li = e;
            return li != null && li.HasLineInfo() ? "Línea " + li.LineNumber + ": " : "";
        }

        // ---------- 2) XSD de TIA Openness ----------

        private static bool _cargado;
        private static XmlSchemaSet _esquemas;
        private static string _carpetaEsquemas;
        private static int _ficherosEsquema;
        private static string _motivoSinEsquemas;

        private static void Esquemas(string ruta, List<string> avisos)
        {
            CargarEsquemas();
            if (_esquemas == null) return;

            XmlReaderSettings ajustes = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = _esquemas,
                DtdProcessing = DtdProcessing.Prohibit
            };
            // Solo errores: el sobre <Document> no tiene esquema y daría avisos de "sin información de esquema"
            ajustes.ValidationEventHandler += (s, a) =>
            {
                if (a.Severity == XmlSeverityType.Error && avisos.Count <= MaxMensajes)
                    avisos.Add("Línea " + a.Exception.LineNumber + ", pos. " + a.Exception.LinePosition +
                                " (XSD): " + a.Message);
            };

            try
            {
                using (XmlReader r = XmlReader.Create(ruta, ajustes))
                {
                    while (r.Read()) { }
                }
            }
            catch (XmlException ex)
            {
                avisos.Add("XSD: lectura interrumpida: " + ex.Message);
            }
        }

        private static void CargarEsquemas()
        {
            if (_cargado) return;
            _cargado = true;

            string carpeta = CarpetaEsquemas();
            if (carpeta == null)
            {
                _motivoSinEsquemas = "no se encuentra la carpeta PublicAPI\\V20\\Schemas";
                return;
            }

            try
            {
                XmlSchemaSet set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
                foreach (string xsd in Directory.GetFiles(carpeta, "*.xsd", SearchOption.AllDirectories))
                {
                    XmlSchema esquema = null;
                    try
                    {
                        using (XmlReader r = XmlReader.Create(xsd, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
                        {
                            esquema = XmlSchema.Read(r, null);
                        }
                        esquema.SourceUri = new Uri(xsd).AbsoluteUri; // para resolver include/import relativos
                        set.Add(esquema);
                        set.Compile();
                        _ficherosEsquema++;
                    }
                    catch
                    {
                        // Un XSD que choca con los ya cargados (incluido por otro, duplicado...) se descarta
                        // sin invalidar el resto
                        try
                        {
                            if (esquema != null && set.Contains(esquema)) set.Remove(esquema);
                            set.Compile();
                        }
                        catch { }
                    }
                }

                if (_ficherosEsquema == 0)
                {
                    _motivoSinEsquemas = "ningún .xsd de " + carpeta + " se pudo cargar";
                    return;
                }
                _esquemas = set;
                _carpetaEsquemas = carpeta;
            }
            catch (Exception ex)
            {
                _motivoSinEsquemas = "no se pudieron compilar los XSD: " + ex.Message;
                _esquemas = null;
            }
        }

        private static string CarpetaEsquemas()
        {
            List<string> candidatas = new List<string>();

            // Junto a la API que ha cargado el Add-In: ...\PublicAPI\V20.AddIn -> ...\PublicAPI\V20\Schemas
            try
            {
                string api = Path.GetDirectoryName(typeof(Siemens.Engineering.TiaPortal).Assembly.Location);
                if (!string.IsNullOrEmpty(api))
                {
                    string publicApi = Path.GetDirectoryName(api);
                    candidatas.Add(Path.Combine(publicApi, "V20", "Schemas"));
                    candidatas.Add(Path.Combine(api, "Schemas"));
                }
            }
            catch { }

            candidatas.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Siemens", "Automation", "Portal V20", "PublicAPI", "V20", "Schemas"));
            candidatas.Add(@"C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Schemas");

            return candidatas.FirstOrDefault(Directory.Exists);
        }
    }
}
