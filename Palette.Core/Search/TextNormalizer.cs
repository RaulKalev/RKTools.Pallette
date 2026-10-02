using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Palette.Core
{
    /// <summary>Case- and diacritic-insensitive text folding for search (an accented letter matches its plain form).</summary>
    public static class TextNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var lower = text.Trim().ToLowerInvariant();
            var decomposed = lower.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>Splits on anything that is not a letter or digit ("Pulse — Analyse Panel" gives pulse, analyse, panel).</summary>
        public static string[] Words(string normalized)
        {
            var words = new List<string>();
            var sb = new StringBuilder();
            foreach (var ch in normalized ?? "")
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0) { words.Add(sb.ToString()); sb.Clear(); }
            }
            if (sb.Length > 0) words.Add(sb.ToString());
            return words.ToArray();
        }
    }
}
