using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameRuleEditor.Core
{
    public static class GameRuleParser
    {
        // Extracts "Move" and ["5", "0", "90", "0"] from "Move(5, 0, 90, 0)"
        public static (string Name, List<string> Params) ParseFunction(string input)
        {
            if (string.IsNullOrEmpty(input)) return (null, null);

            // Regex to capture Name and Content inside outermost parentheses
            var match = Regex.Match(input.Trim(), @"^!?([a-zA-Z0-9_]+)\s*\((.*)\)\s*$");

            if (!match.Success) return (input.Trim(), new List<string>());

            string name = match.Groups[1].Value;
            string content = match.Groups[2].Value;

            List<string> parameters = SplitParameters(content);

            return (name, parameters);
        }

        // Splits by comma, but respects nested parentheses if they exist (though rare in this DSL)
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
            // Remove wrapping quotes if they exist (e.g. "Enemy" -> Enemy)
            if (p.StartsWith("\"") && p.EndsWith("\"") && p.Length > 1)
                return p.Substring(1, p.Length - 2);
            return p;
        }

        // Splits a condition string by logical operators (AND, OR)
        public static List<(string op, string condition)> SplitConditions(string fullCondition)
        {
            var results = new List<(string, string)>();

            // Normalize operators
            string input = fullCondition
                .Replace(" && ", " AND ")
                .Replace(" || ", " OR ");

            // Split keeping delimiters is tricky with Regex, doing a linear pass
            string[] tokens = Regex.Split(input, @"\s+(AND|OR)\s+");

            string currentOp = ""; // First element has no operator

            // Regex.Split returns: [Cond1, OP, Cond2, OP, Cond3]
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();

                if (token == "AND" || token == "OR")
                {
                    currentOp = token;
                }
                else
                {
                    if (!string.IsNullOrEmpty(token))
                    {
                        results.Add((currentOp, token));
                        currentOp = ""; // Reset for next
                    }
                }
            }

            return results;
        }
    }
}