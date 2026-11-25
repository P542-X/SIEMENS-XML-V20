using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExportarXml
{
    /// <summary>
    /// JSON mínimo, sin dependencias (el Add-In compila sin NuGet).
    /// Escribe con sangría para que una persona o una IA lo lea y lo edite a mano.
    /// Objetos → Dictionary&lt;string, object&gt; (orden de inserción), listas → List&lt;object&gt;,
    /// números → long o double, true/false → bool, null → null.
    /// </summary>
    internal static class Json
    {
        // ---------- Escribir ----------

        public static string Escribir(object valor)
        {
            StringBuilder sb = new StringBuilder();
            Valor(sb, valor, 0);
            sb.AppendLine();
            return sb.ToString();
        }

        private static void Valor(StringBuilder sb, object v, int nivel)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: Cadena(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : ((double)f).ToString("R", CultureInfo.InvariantCulture)); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> obj: Objeto(sb, obj, nivel); return;
                case IEnumerable lista: Lista(sb, lista, nivel); return;
            }
            if (v is IConvertible c && v.GetType().IsPrimitive) { sb.Append(c.ToString(CultureInfo.InvariantCulture)); return; }
            Cadena(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        private static void Objeto(StringBuilder sb, IDictionary<string, object> obj, int nivel)
        {
            if (obj.Count == 0) { sb.Append("{}"); return; }
            sb.Append('{');
            bool primero = true;
            foreach (var kv in obj)
            {
                if (!primero) sb.Append(',');
                primero = false;
                sb.AppendLine();
                Sangria(sb, nivel + 1);
                Cadena(sb, kv.Key);
                sb.Append(": ");
                Valor(sb, kv.Value, nivel + 1);
            }
            sb.AppendLine();
            Sangria(sb, nivel);
            sb.Append('}');
        }

        private static void Lista(StringBuilder sb, IEnumerable lista, int nivel)
        {
            List<object> items = new List<object>();
            foreach (object o in lista) items.Add(o);
            if (items.Count == 0) { sb.Append("[]"); return; }
            bool simples = items.TrueForAll(o => !(o is IDictionary<string, object>) && (o is string || !(o is IEnumerable)));
            if (simples)
            {
                sb.Append('[');
                for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(", "); Valor(sb, items[i], nivel + 1); }
                sb.Append(']');
                return;
            }
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.AppendLine();
                Sangria(sb, nivel + 1);
                Valor(sb, items[i], nivel + 1);
            }
            sb.AppendLine();
            Sangria(sb, nivel);
            sb.Append(']');
        }

        private static void Sangria(StringBuilder sb, int nivel) => sb.Append(' ', nivel * 2);

        private static void Cadena(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------- Leer ----------

        public static object Leer(string texto)
        {
            int i = 0;
            object v = LeerValor(texto, ref i);
            Blancos(texto, ref i);
            if (i < texto.Length) throw Error(texto, i, "sobra texto después del JSON");
            return v;
        }

        private static object LeerValor(string t, ref int i)
        {
            Blancos(t, ref i);
            if (i >= t.Length) throw Error(t, i, "fin inesperado");
            char c = t[i];
            if (c == '{') return LeerObjeto(t, ref i);
            if (c == '[') return LeerLista(t, ref i);
            if (c == '"') return LeerCadena(t, ref i);
            if (Palabra(t, ref i, "true")) return true;
            if (Palabra(t, ref i, "false")) return false;
            if (Palabra(t, ref i, "null")) return null;
            return LeerNumero(t, ref i);
        }

        private static Dictionary<string, object> LeerObjeto(string t, ref int i)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            i++; // {
            Blancos(t, ref i);
            if (i < t.Length && t[i] == '}') { i++; return d; }
            while (true)
            {
                Blancos(t, ref i);
                if (i >= t.Length || t[i] != '"') throw Error(t, i, "se esperaba el nombre de una propiedad entre comillas");
                string clave = LeerCadena(t, ref i);
                Blancos(t, ref i);
                if (i >= t.Length || t[i] != ':') throw Error(t, i, "se esperaba ':'");
                i++;
                d[clave] = LeerValor(t, ref i);
                Blancos(t, ref i);
                if (i < t.Length && t[i] == ',') { i++; continue; }
                if (i < t.Length && t[i] == '}') { i++; return d; }
                throw Error(t, i, "se esperaba ',' o '}'");
            }
        }

        private static List<object> LeerLista(string t, ref int i)
        {
            List<object> l = new List<object>();
            i++; // [
            Blancos(t, ref i);
            if (i < t.Length && t[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(LeerValor(t, ref i));
                Blancos(t, ref i);
                if (i < t.Length && t[i] == ',') { i++; continue; }
                if (i < t.Length && t[i] == ']') { i++; return l; }
                throw Error(t, i, "se esperaba ',' o ']'");
            }
        }

        private static string LeerCadena(string t, ref int i)
        {
            StringBuilder sb = new StringBuilder();
            i++; // "
            while (i < t.Length)
            {
                char c = t[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= t.Length) break;
                char e = t[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > t.Length) throw Error(t, i, "\\u incompleto");
                        sb.Append((char)int.Parse(t.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(t, i, "escape no válido \\" + e);
                }
            }
            throw Error(t, i, "cadena sin cerrar");
        }

        private static object LeerNumero(string t, ref int i)
        {
            int inicio = i;
            while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0) i++;
            string s = t.Substring(inicio, i - inicio);
            if (s.Length == 0) throw Error(t, inicio, "valor no válido");
            if (s.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            throw Error(t, inicio, "número no válido: " + s);
        }

        private static bool Palabra(string t, ref int i, string p)
        {
            if (string.CompareOrdinal(t, i, p, 0, p.Length) != 0) return false;
            i += p.Length;
            return true;
        }

        private static void Blancos(string t, ref int i)
        {
            while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
        }

        private static Exception Error(string t, int i, string que)
        {
            int linea = 1, col = 1;
            for (int k = 0; k < i && k < t.Length; k++) { if (t[k] == '\n') { linea++; col = 1; } else col++; }
            return new FormatException("JSON no válido (línea " + linea + ", columna " + col + "): " + que);
        }
    }
}
