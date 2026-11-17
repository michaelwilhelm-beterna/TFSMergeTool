using System;
using System.Globalization;
using System.Linq;

namespace MergeHelper
{
    /// <summary>
    /// Spaltenfilter:
    ///  - Text: alle Wörter müssen enthalten sein (ohne Groß-/Kleinschreibung), "!wort" schließt aus.
    ///  - Zahl/Datum: optional Vergleichsoperator (&gt;, &gt;=, &lt;, &lt;=, =), sonst Textsuche.
    ///    Mehrere Bedingungen mit Leerzeichen kombinierbar, z.B. "&gt;=01.10.2026 &lt;01.11.2026".
    /// </summary>
    public static class FilterLogic
    {
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

        public static bool MatchText(string value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            value = value ?? "";
            foreach (var token in Tokens(filter))
            {
                if (token.StartsWith("!") && token.Length > 1)
                {
                    if (Contains(value, token.Substring(1))) return false;
                }
                else if (!Contains(value, token)) return false;
            }
            return true;
        }

        public static bool MatchNumber(int value, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            foreach (var token in Tokens(filter))
            {
                var (op, rest) = SplitOperator(token);
                if (op != null && int.TryParse(rest, out var n))
                {
                    if (!Compare(value.CompareTo(n), op)) return false;
                }
                else if (!MatchText(value.ToString(), token)) return false;
            }
            return true;
        }

        public static bool MatchDate(DateTime value, string text, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            foreach (var token in Tokens(filter))
            {
                var (op, rest) = SplitOperator(token);
                if (op != null && TryParseDate(rest, out var d, out var dateOnly))
                {
                    var cmp = dateOnly ? value.Date.CompareTo(d.Date) : value.CompareTo(d);
                    if (!Compare(cmp, op)) return false;
                }
                else if (!MatchText(text, token)) return false;
            }
            return true;
        }

        private static string[] Tokens(string filter) =>
            filter.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool Contains(string value, string part) =>
            value.IndexOf(part, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private static (string op, string rest) SplitOperator(string token)
        {
            foreach (var op in new[] { ">=", "<=", ">", "<", "=" })
                if (token.StartsWith(op) && token.Length > op.Length)
                    return (op, token.Substring(op.Length));
            return (null, token);
        }

        private static bool Compare(int cmp, string op)
        {
            switch (op)
            {
                case ">": return cmp > 0;
                case ">=": return cmp >= 0;
                case "<": return cmp < 0;
                case "<=": return cmp <= 0;
                default: return cmp == 0;
            }
        }

        private static bool TryParseDate(string s, out DateTime d, out bool dateOnly)
        {
            dateOnly = !s.Contains(":");
            string[] formats = { "d.M.yyyy", "d.M.yy", "yyyy-MM-dd", "d.M.yyyy HH:mm", "yyyy-MM-ddTHH:mm" };
            if (DateTime.TryParseExact(s, formats, De, DateTimeStyles.None, out d)) return true;
            // "d.M." ohne Jahr -> aktuelles Jahr
            if (DateTime.TryParseExact(s.TrimEnd('.'), "d.M", De, DateTimeStyles.None, out d)) return true;
            return DateTime.TryParse(s, De, DateTimeStyles.None, out d);
        }
    }
}
