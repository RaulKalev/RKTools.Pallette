using System;
using System.Collections.Generic;
using System.Linq;
using Palette.Core.Commands;
using Palette.Core.Settings;

namespace Palette.Core
{
    public enum EntryKind
    {
        Header,
        Command
    }

    /// <summary>One row of the Palette list: a section header or a command.</summary>
    public sealed class ListEntry
    {
        private ListEntry() { }

        public EntryKind Kind { get; private set; }
        public string Header { get; private set; }
        public PaletteCommand Command { get; private set; }

        public static ListEntry ForHeader(string text) => new ListEntry { Kind = EntryKind.Header, Header = text };
        public static ListEntry ForCommand(PaletteCommand command) => new ListEntry { Kind = EntryKind.Command, Command = command };
    }

    /// <summary>
    /// What Palette shows. Combines the registry (what exists), settings (hidden / favourites / order), recent use and the
    /// Revit context (availability) into list entries. Pure in-memory work: safe to call on every keystroke.
    /// </summary>
    public sealed class PaletteCatalog
    {
        private readonly PaletteCommandRegistry _registry;
        private readonly SettingsService _settings;
        private readonly RecentCommandsService _recents;
        private readonly SearchService _search;

        public PaletteCatalog(PaletteCommandRegistry registry, SettingsService settings, RecentCommandsService recents, SearchService search = null)
        {
            _registry = registry;
            _settings = settings;
            _recents = recents;
            _search = search ?? new SearchService();
        }

        public bool IsVisible(PaletteCommand command, PaletteContext context) =>
            !_settings.IsHidden(command.Id) && command.IsAvailable(context);

        /// <summary>Empty search: Favorites, then Recently used, then every other command.</summary>
        public IReadOnlyList<ListEntry> BuildHome(PaletteContext context)
        {
            var visible = _registry.Snapshot()
                .Select(i => i.Command)
                .Where(c => IsVisible(c, context))
                .ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);

            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var favorites = new List<PaletteCommand>();
            foreach (var id in _settings.Current.Favorites)
            {
                PaletteCommand c;
                if (visible.TryGetValue(id, out c) && shown.Add(c.Id)) favorites.Add(c);
            }

            var recents = new List<PaletteCommand>();
            foreach (var id in _recents.RecentIds(_settings.Current.RecentShown + favorites.Count))
            {
                PaletteCommand c;
                if (recents.Count >= _settings.Current.RecentShown) break;
                if (visible.TryGetValue(id, out c) && shown.Add(c.Id)) recents.Add(c);
            }

            var rest = OrderByCustomOrder(visible.Values.Where(c => !shown.Contains(c.Id)));

            var entries = new List<ListEntry>();
            var sections = (favorites.Count > 0 ? 1 : 0) + (recents.Count > 0 ? 1 : 0) + (rest.Count > 0 ? 1 : 0);
            Section(entries, "Favorites", favorites, sections > 1 || favorites.Count > 0);
            Section(entries, "Recently used", recents, true);
            Section(entries, "Commands", rest, sections > 1);
            return entries;
        }

        /// <summary>Live search: a flat, ranked list without headers.</summary>
        public IReadOnlyList<ListEntry> BuildSearch(string query, PaletteContext context, int max = 60)
        {
            var candidates = _registry.Snapshot().Where(i => IsVisible(i.Command, context));
            var results = _search.Search(query, candidates, Boost, max);
            return results.Select(r => ListEntry.ForCommand(r.Command)).ToList();
        }

        public ListEntry FirstCommand(IEnumerable<ListEntry> entries) => entries.FirstOrDefault(e => e.Kind == EntryKind.Command);

        /// <summary>Favourites and recently used commands win ties inside the same relevance band.</summary>
        private int Boost(PaletteCommand c)
        {
            var boost = 0;
            if (_settings.IsFavorite(c.Id)) boost += 25;
            boost += Math.Min(15, _recents.RecencyRank(c.Id)) + Math.Min(5, _recents.UseCount(c.Id));
            return boost;
        }

        /// <summary>The user's custom order first, then everything else by category and name.</summary>
        public List<PaletteCommand> OrderByCustomOrder(IEnumerable<PaletteCommand> commands)
        {
            var order = _settings.Current.Order;
            Func<PaletteCommand, int> position = c =>
            {
                var i = order.FindIndex(o => string.Equals(o, c.Id, StringComparison.OrdinalIgnoreCase));
                return i < 0 ? int.MaxValue : i;
            };
            return commands
                .OrderBy(position)
                .ThenBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void Section(List<ListEntry> entries, string header, List<PaletteCommand> commands, bool showHeader)
        {
            if (commands.Count == 0) return;
            if (showHeader) entries.Add(ListEntry.ForHeader(header));
            foreach (var c in commands) entries.Add(ListEntry.ForCommand(c));
        }
    }
}
