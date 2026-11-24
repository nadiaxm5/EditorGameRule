using System.Collections.Generic;

namespace GameRuleEditor.Core
{
    /// <summary>The individual conditions inside the existing single When expression.</summary>
    internal sealed class RuleConditionTerm
    {
        public string JoinBefore;
        public string Source;

        public RuleConditionTerm(string joinBefore, string source)
        {
            JoinBefore = joinBefore;
            Source = source ?? string.Empty;
        }
    }

    internal static class RuleConditionSequence
    {
        public static List<RuleConditionTerm> Parse(string source)
        {
            var result = new List<RuleConditionTerm>();
            if (string.IsNullOrWhiteSpace(source))
            {
                result.Add(new RuleConditionTerm(null, string.Empty));
                return result;
            }

            string join = null;
            bool negated = false;
            foreach (string token in GameRuleParser.TokenizeCondition(source))
            {
                if (token == "AND" || token == "OR")
                {
                    join = token;
                }
                else if (token == "NOT")
                {
                    negated = !negated;
                }
                else
                {
                    result.Add(new RuleConditionTerm(result.Count == 0 ? null : join ?? "AND",
                        (negated ? "NOT " : string.Empty) + token));
                    join = null;
                    negated = false;
                }
            }

            if (result.Count == 0) result.Add(new RuleConditionTerm(null, string.Empty));
            return result;
        }

        public static string Build(IReadOnlyList<RuleConditionTerm> terms)
        {
            var parts = new List<string>();
            foreach (RuleConditionTerm term in terms)
            {
                if (string.IsNullOrWhiteSpace(term.Source)) continue;
                if (parts.Count > 0) parts.Add(term.JoinBefore == "OR" ? "OR" : "AND");
                parts.Add(term.Source.Trim());
            }
            return string.Join(" ", parts);
        }

        public static void Move(List<RuleConditionTerm> terms, int from, int to)
        {
            if (terms == null || from < 0 || to < 0 ||
                from >= terms.Count || to >= terms.Count || from == to) return;

            RuleConditionTerm moved = terms[from];
            // The connector belongs to the condition after it. When an item crosses
            // the first position, transfer that connector to the new second item.
            if (to == 0)
            {
                terms[0].JoinBefore = moved.JoinBefore;
                moved.JoinBefore = null;
            }
            else if (from == 0)
            {
                moved.JoinBefore = terms[1].JoinBefore;
                terms[1].JoinBefore = null;
            }

            terms.RemoveAt(from);
            terms.Insert(to, moved);
        }

        public static string WithoutNot(string source)
        {
            return source != null && source.StartsWith("NOT ", System.StringComparison.Ordinal)
                ? source.Substring(4) : source ?? string.Empty;
        }

        public static bool IsNegated(string source)
        {
            return source != null && source.StartsWith("NOT ", System.StringComparison.Ordinal);
        }
    }
}
