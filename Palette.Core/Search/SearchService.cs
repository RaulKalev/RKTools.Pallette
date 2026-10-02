using System;
using System.Collections.Generic;
using System.Linq;
using Palette.Core.Commands;

namespace Palette.Core
{
    public sealed class SearchResult
    {
        public SearchResult(IndexedCommand entry, int score)
        {
            Entry = entry;
            Score = score;
        }

        public IndexedCommand Entry { get; }
        public PaletteCommand Command => Entry.Command;
        public int Score { get; }
    }

    /// <summary>
    /// Simple weighted search over the in-memory command index (no I/O, no reflection, nothing allocated per command
    /// beyond the result list). Priority, highest first:
    ///   exact name / alias  &gt;  name starts with  &gt;  a word of the name starts with  &gt;  initials  &gt;  name contains
    ///   &gt;  keyword  &gt;  category  &gt;  description  &gt;  loose subsequence of the name.
    /// Every whitespace separated word of the query must match something; the word scores are averaged.
    /// A small <c>boost</c> (favourites, recent use) only reorders results inside the same priority band.
    /// </summary>
    public sealed class SearchService
    {
        public const int MaxBoost = 49;

        public IReadOnlyList<SearchResult> Search(string query, IEnumerable<IndexedCommand> candidates,
            Func<PaletteCommand, int> boost = null, int max = 60)
        {
            var q = TextNormalizer.Normalize(query);
            if (q.Length == 0) return new SearchResult[0];
            var tokens = q.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            var results = new List<SearchResult>();
            foreach (var c in candidates)
            {
                var score = Score(q, tokens, c);
                if (score <= 0) continue;
                if (boost != null) score += Math.Max(0, Math.Min(MaxBoost, boost(c.Command)));
                results.Add(new SearchResult(c, score));
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Entry.Name.Length)
                .ThenBy(r => r.Command.Name, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }

        /// <summary>Score of a normalised query against one command; 0 means no match.</summary>
        public static int Score(string normalizedQuery, string[] tokens, IndexedCommand c)
        {
            if (c.Name == normalizedQuery) return 1000;
            if (tokens.Length == 0) return 0;

            var total = 0;
            foreach (var t in tokens)
            {
                var s = TokenScore(t, c);
                if (s == 0) return 0;
                total += s;
            }
            var score = total / tokens.Length;
            if (tokens.Length > 1 && c.Name.StartsWith(normalizedQuery, StringComparison.Ordinal)) score = Math.Max(score, 800);
            return score;
        }

        private static int TokenScore(string t, IndexedCommand c)
        {
            var best = 0;

            if (c.Name == t) return 1000;
            foreach (var a in c.Aliases) if (a == t) return 950;

            if (c.Name.StartsWith(t, StringComparison.Ordinal)) best = 800;
            else
            {
                foreach (var a in c.Aliases)
                    if (a.StartsWith(t, StringComparison.Ordinal)) { best = 780; break; }
            }
            if (best >= 780) return best;

            foreach (var w in c.NameWords)
                if (w.StartsWith(t, StringComparison.Ordinal)) { best = 700; break; }
            if (best > 0) return best;

            if (t.Length >= 2 && c.Initials.StartsWith(t, StringComparison.Ordinal)) return 600;
            if (c.Name.Contains(t)) return 500;
            foreach (var a in c.Aliases) if (a.Contains(t)) return 480;

            foreach (var k in c.Keywords)
            {
                if (k == t) best = Math.Max(best, 450);
                else if (k.StartsWith(t, StringComparison.Ordinal)) best = Math.Max(best, 400);
                else if (k.Contains(t)) best = Math.Max(best, 300);
            }
            if (best > 0) return best;

            if (c.Category.StartsWith(t, StringComparison.Ordinal)) return 220;
            if (c.Category.Contains(t)) return 150;
            if (t.Length >= 3 && c.Description.Contains(t)) return 120;

            if (t.Length >= 3) return Subsequence(t, c.Name);
            return 0;
        }

        /// <summary>"cfov" matches "camera fov": letters in order, not too spread out. 60..110, or 0.</summary>
        private static int Subsequence(string t, string name)
        {
            var ti = 0;
            var first = -1;
            var last = -1;
            for (var i = 0; i < name.Length && ti < t.Length; i++)
            {
                if (name[i] != t[ti]) continue;
                if (first < 0) first = i;
                last = i;
                ti++;
            }
            if (ti < t.Length) return 0;
            var span = last - first + 1;
            if (span > t.Length * 3) return 0;
            return 110 - Math.Min(50, (span - t.Length) * 5);
        }
    }
}
