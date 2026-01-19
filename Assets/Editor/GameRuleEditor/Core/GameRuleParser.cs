using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace GameRuleEditor.Core
{
    public static class GameRuleParser
    {
        // Extracts "Move" and ["5", "0", "90", "0"] from "Move(5, 0, 90, 0)"
        public static (string Name, List<string> Params) ParseFunction(string input)
        {
            if (string.IsNullOrEmpty(input)) return (null, null);

            var match = Regex.Match(input.Trim(), @"^!?([a-zA-Z0-9_]+)\s*\((.*)\)\s*$");

            if (!match.Success) return (input.Trim(), new List<string>());

            string name = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            List<string> parameters = SplitParameters(content);

            return (name, parameters);
        }

        private static List<string> SplitParameters(string content)
        {
            List<string> result = new List<string>();
            int parenthesisLevel = 0;
            string current = "";

            foreach (char c in content)
            {
                if (c == ',' && parenthesisLevel == 0)
                {
                    result.Add(CleanParam(current));
                    current = "";
                }
                else
                {
                    if (c == '(') parenthesisLevel++;
                    if (c == ')') parenthesisLevel--;
                    current += c;
                }
            }
            result.Add(CleanParam(current));
            return result;
        }

        private static string CleanParam(string p)
        {
            p = p.Trim();
            if (p.StartsWith("\"") && p.EndsWith("\"") && p.Length > 1)
                return p.Substring(1, p.Length - 2);
            return p;
        }

        /// <summary>
        /// Splits a full condition string into tokens (Conditions and Operators).
        /// Example: "NOT Check(A) AND Compare(B)" -> ["NOT", "Check(A)", "AND", "Compare(B)"]
        /// </summary>
        public static List<string> TokenizeCondition(string fullCondition)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(fullCondition)) return tokens;

            // Normalize spaces around operators to ensure splitting works
            // We use a regex that looks for whole words AND, OR, NOT
            string pattern = @"\b(AND|OR|NOT)\b";

            // Split keeps delimiters because of parentheses in pattern
            string[] parts = Regex.Split(fullCondition, pattern);

            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    tokens.Add(trimmed);
                }
            }

            return tokens;
        }
    }
}