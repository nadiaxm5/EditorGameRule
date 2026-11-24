using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace GameRuleEditor.Core
{
    public static class GameRuleParser
    {
        public static (string Name, List<string> Params) ParseFunction(string input)
        {
            if (string.IsNullOrEmpty(input)) return (null, null);

            var match = Regex.Match(input.Trim(), @"^!?([a-zA-Z0-9_]+)\s*\((.*)\)\s*$", RegexOptions.Singleline);

            if (!match.Success) return (input.Trim(), new List<string>());

            string name = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            List<string> parameters = SplitParameters(content);

            return (name, parameters);
        }

        public static List<string> SplitParameters(string content)
        {
            var result = new List<string>();
            // An empty slot is still a parameter in existing descriptors (e.g. Check()).
            if (string.IsNullOrWhiteSpace(content)) { result.Add(string.Empty); return result; }
            int depth = 0, start = 0;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (IsQuoteStart(content, i)) { i = QuotedEnd(content, i); continue; }
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(CleanParam(content.Substring(start, i - start)));
                    start = i + 1;
                }
            }
            result.Add(CleanParam(content.Substring(start)));
            return result;
        }

        private static string CleanParam(string p)
        {
            p = p.Trim();
            if (p.Length < 2 || p[0] != '"' || QuotedEnd(p, 0) != p.Length - 1 || p[p.Length - 1] != '"') return p;
            var result = new StringBuilder();
            for (int i = 1; i < p.Length - 1; i++)
            {
                if (p[i] != '\\' || i + 1 >= p.Length - 1) { result.Append(p[i]); continue; }
                char escaped = p[++i];
                switch (escaped)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'u':
                        if (i + 4 < p.Length - 1 && ushort.TryParse(p.Substring(i + 1, 4),
                            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
                        { result.Append((char)code); i += 4; }
                        else result.Append("\\u");
                        break;
                    default: result.Append('\\').Append(escaped); break;
                }
            }
            return result.ToString();
        }

        internal static int QuotedEnd(string text, int start)
        {
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (text[i] == text[start]) return i;
            }
            return text.Length - 1;
        }

        private static bool IsQuoteStart(string text, int index) => text[index] == '"' ||
            (text[index] == '\'' && (index == 0 || !char.IsLetterOrDigit(text[index - 1]) && text[index - 1] != '_'));

        // Shared escaping for JSON text and generated C# string literals.
        public static string QuoteString(string value)
        {
            var result = new StringBuilder("\"");
            foreach (char c in value ?? string.Empty)
                switch (c)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (c < ' ' || c == '\u2028' || c == '\u2029')
                            result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else result.Append(c);
                        break;
                }
            return result.Append('"').ToString();
        }

        public static string FormatFunction(string name, IEnumerable<string> parameters)
        {
            var values = new List<string>();
            foreach (string parameter in parameters)
            {
                string value = parameter ?? string.Empty;
                int depth = 0;
                bool quote = value != value.Trim();
                foreach (char c in value)
                {
                    if (c == '"' || c == '\'' || c == '\\' || char.IsControl(c)) quote = true;
                    else if (c == '(') depth++;
                    else if (c == ')') { depth--; if (depth < 0) quote = true; }
                    else if (c == ',' && depth == 0) quote = true;
                }
                values.Add(quote || depth != 0 ? QuoteString(value) : value);
            }
            return name + "(" + string.Join(",", values) + ")";
        }

        /// <summary>
        /// Splits a full condition string into tokens (Conditions and Operators).
        /// </summary>
        public static List<string> TokenizeCondition(string fullCondition)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(fullCondition)) return tokens;

            int depth = 0, start = 0;
            for (int i = 0; i < fullCondition.Length; i++)
            {
                char c = fullCondition[i];
                if (IsQuoteStart(fullCondition, i)) { i = QuotedEnd(fullCondition, i); continue; }
                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth != 0) continue;
                string operation = null;
                if (c == '!' && (i + 1 >= fullCondition.Length || fullCondition[i + 1] != '=')) operation = "!";
                else foreach (string word in new[] { "AND", "OR", "NOT" })
                {
                    if (i + word.Length <= fullCondition.Length &&
                        string.CompareOrdinal(fullCondition, i, word, 0, word.Length) == 0 &&
                        (i == 0 || !IsReferenceCharacter(fullCondition[i - 1])) &&
                        (i + word.Length == fullCondition.Length || !IsReferenceCharacter(fullCondition[i + word.Length])))
                    { operation = word; break; }
                }
                if (operation == null) continue;
                string condition = fullCondition.Substring(start, i - start).Trim();
                if (condition.Length > 0) tokens.Add(condition);
                tokens.Add(operation == "!" ? "NOT" : operation);
                i += operation.Length - 1;
                start = i + 1;
            }
            string tail = fullCondition.Substring(start).Trim();
            if (tail.Length > 0) tokens.Add(tail);
            return tokens;
        }

        private static bool IsReferenceCharacter(char c) =>
            char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '#';
    }
}
