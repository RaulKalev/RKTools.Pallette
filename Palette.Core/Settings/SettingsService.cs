using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Palette.Core.Settings
{
    /// <summary>
    /// User customisation, persisted as JSON. Unknown properties are ignored and missing ones fall back to defaults,
    /// so files written by older or newer versions load.
    /// </summary>
    public sealed class PaletteSettings
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        /// <summary>Favourite command ids in the order the user arranged them.</summary>
        public List<string> Favorites { get; set; } = new List<string>();

        /// <summary>Commands the user hid; they never show up in Palette.</summary>
        public List<string> Hidden { get; set; } = new List<string>();

        /// <summary>Custom order of the remaining commands; commands not listed come after, by category and name.</summary>
        public List<string> Order { get; set; } = new List<string>();

        /// <summary>How many recent commands are shown on the empty search.</summary>
        public int RecentShown { get; set; } = 6;
    }

    /// <summary>
    /// Owns the in-memory <see cref="PaletteSettings"/> and its file (default %AppData%\Palette\settings.json).
    /// Ids of commands that are not currently registered (a plugin that is not loaded) are kept, so favourites survive
    /// a plugin being absent for a while. All access is from the UI thread; the lock only guards the file.
    /// </summary>
    public sealed class SettingsService
    {
        private readonly object _lock = new object();

        public SettingsService(string path = null)
        {
            Path = path ?? DefaultPath;
        }

        public static string DefaultFolder => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Palette");

        public static string DefaultPath => System.IO.Path.Combine(DefaultFolder, "settings.json");

        public string Path { get; }
        public PaletteSettings Current { get; private set; } = new PaletteSettings();

        /// <summary>Why the last <see cref="Load"/> fell back to defaults (null when it did not).</summary>
        public string LoadError { get; private set; }

        /// <summary>Raised after any change (favourite, hidden, order).</summary>
        public event EventHandler Changed;

        public void Load()
        {
            lock (_lock)
            {
                string error;
                var loaded = JsonFileStore.Load<PaletteSettings>(Path, out error);
                LoadError = error;
                Current = Sanitize(loaded ?? new PaletteSettings());
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool Save()
        {
            lock (_lock) return JsonFileStore.TrySave(Path, Current);
        }

        // ------------------------------------------------------------------ queries

        public bool IsFavorite(string id) => Contains(Current.Favorites, id);
        public bool IsHidden(string id) => Contains(Current.Hidden, id);

        public int FavoriteIndex(string id) => IndexOf(Current.Favorites, id);

        // ------------------------------------------------------------------ changes (each saves immediately)

        public void SetFavorite(string id, bool favorite)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var index = IndexOf(Current.Favorites, id);
            if (favorite && index < 0)
            {
                Current.Favorites.Add(id);
                if (IsHidden(id)) Current.Hidden.RemoveAll(h => SameId(h, id)); // a favourite is never hidden
            }
            else if (!favorite && index >= 0) Current.Favorites.RemoveAt(index);
            else return;
            Commit();
        }

        public void ToggleFavorite(string id) => SetFavorite(id, !IsFavorite(id));

        public void MoveFavorite(string id, int delta)
        {
            var index = IndexOf(Current.Favorites, id);
            var target = index + delta;
            if (index < 0 || target < 0 || target >= Current.Favorites.Count) return;
            var item = Current.Favorites[index];
            Current.Favorites.RemoveAt(index);
            Current.Favorites.Insert(target, item);
            Commit();
        }

        public void SetHidden(string id, bool hidden)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var index = IndexOf(Current.Hidden, id);
            if (hidden && index < 0)
            {
                Current.Hidden.Add(id);
                Current.Favorites.RemoveAll(f => SameId(f, id)); // hidden commands are not favourites any more
            }
            else if (!hidden && index >= 0) Current.Hidden.RemoveAt(index);
            else return;
            Commit();
        }

        /// <summary>Replaces the custom order with the given ids (the UI passes the order it displays).</summary>
        public void SetOrder(IEnumerable<string> ids)
        {
            Current.Order = Distinct(ids);
            Commit();
        }

        public void MoveInOrder(IList<string> currentOrder, string id, int delta)
        {
            var list = currentOrder.ToList();
            var index = list.FindIndex(x => SameId(x, id));
            var target = index + delta;
            if (index < 0 || target < 0 || target >= list.Count) return;
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(target, item);
            SetOrder(list);
        }

        public void Reset()
        {
            Current = new PaletteSettings();
            Commit();
        }

        private void Commit()
        {
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------------ helpers

        private static PaletteSettings Sanitize(PaletteSettings s)
        {
            s.Favorites = Distinct(s.Favorites);
            s.Hidden = Distinct(s.Hidden);
            s.Hidden.RemoveAll(h => s.Favorites.Any(f => SameId(f, h)));
            s.Order = Distinct(s.Order);
            if (s.RecentShown < 0) s.RecentShown = 0;
            if (s.RecentShown > 20) s.RecentShown = 20;
            return s;
        }

        private static List<string> Distinct(IEnumerable<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            foreach (var id in ids ?? new string[0])
                if (!string.IsNullOrWhiteSpace(id) && seen.Add(id)) list.Add(id);
            return list;
        }

        private static bool SameId(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static bool Contains(List<string> list, string id) => IndexOf(list, id) >= 0;
        private static int IndexOf(List<string> list, string id) => list.FindIndex(x => SameId(x, id));
    }
}
