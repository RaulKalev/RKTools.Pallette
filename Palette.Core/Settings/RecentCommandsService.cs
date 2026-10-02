using System;
using System.Collections.Generic;
using System.Linq;

namespace Palette.Core.Settings
{
    public sealed class RecentEntry
    {
        public string Id { get; set; }
        public DateTime LastUsedUtc { get; set; }
        public int UseCount { get; set; }
    }

    public sealed class RecentFile
    {
        public int Version { get; set; } = 1;
        public List<RecentEntry> Items { get; set; } = new List<RecentEntry>();
    }

    /// <summary>
    /// Remembers which commands were launched (id, last used, use count) in %AppData%\Palette\recent.json, newest first.
    /// Kept apart from settings.json because it changes on every launch while settings change rarely.
    /// The list is capped, so it never grows.
    /// </summary>
    public sealed class RecentCommandsService
    {
        public const int MaxStored = 12;

        private readonly object _lock = new object();
        private List<RecentEntry> _items = new List<RecentEntry>();

        public RecentCommandsService(string path = null)
        {
            Path = path ?? System.IO.Path.Combine(SettingsService.DefaultFolder, "recent.json");
        }

        public string Path { get; }

        public void Load()
        {
            lock (_lock)
            {
                string error;
                var file = JsonFileStore.Load<RecentFile>(Path, out error);
                var items = file?.Items ?? new List<RecentEntry>();
                _items = items
                    .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Id))
                    .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(i => i.LastUsedUtc).First())
                    .OrderByDescending(i => i.LastUsedUtc)
                    .Take(MaxStored)
                    .ToList();
            }
        }

        /// <summary>Records a launch and saves.</summary>
        public void RecordUse(string id, DateTime? nowUtc = null)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            lock (_lock)
            {
                var entry = _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
                if (entry == null) entry = new RecentEntry { Id = id };
                else _items.Remove(entry);
                entry.LastUsedUtc = nowUtc ?? DateTime.UtcNow;
                entry.UseCount++;
                _items.Insert(0, entry);
                if (_items.Count > MaxStored) _items.RemoveRange(MaxStored, _items.Count - MaxStored);
                JsonFileStore.TrySave(Path, new RecentFile { Items = _items });
            }
        }

        /// <summary>Most recently used command ids, newest first.</summary>
        public IReadOnlyList<string> RecentIds(int count)
        {
            lock (_lock) return _items.Take(Math.Max(0, count)).Select(i => i.Id).ToList();
        }

        public int UseCount(string id)
        {
            lock (_lock)
            {
                var e = _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
                return e == null ? 0 : e.UseCount;
            }
        }

        /// <summary>0 = not recent, otherwise larger for more recent entries (used as a small search boost).</summary>
        public int RecencyRank(string id)
        {
            lock (_lock)
            {
                var index = _items.FindIndex(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
                return index < 0 ? 0 : _items.Count - index;
            }
        }

        public IReadOnlyList<RecentEntry> Entries
        {
            get { lock (_lock) return _items.Select(i => new RecentEntry { Id = i.Id, LastUsedUtc = i.LastUsedUtc, UseCount = i.UseCount }).ToList(); }
        }
    }
}
