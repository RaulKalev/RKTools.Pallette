using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Palette.UI.Mvvm;

namespace Palette.UI.ViewModels
{
    public enum SettingsSection
    {
        Favorites,
        Commands,
        Hidden
    }

    /// <summary>A row of the settings list: a section header, a hint, or a command with its state.</summary>
    public sealed class SettingsRow : ObservableObject
    {
        private bool _canMoveUp;
        private bool _canMoveDown;

        private SettingsRow() { }

        public static SettingsRow ForHeader(string text) => new SettingsRow { IsHeader = true, Text = text };
        public static SettingsRow ForHint(string text) => new SettingsRow { IsHint = true, Text = text };
        public static SettingsRow ForCommand(PaletteCommand command) => new SettingsRow { Command = command };

        public bool IsHeader { get; private set; }
        public bool IsHint { get; private set; }
        public bool IsCommand => Command != null;

        /// <summary>Header or hint text.</summary>
        public string Text { get; private set; }

        public PaletteCommand Command { get; private set; }
        public string Name => Command?.Name;
        public string Category => Command?.Category;
        public string IconKind => Command?.IconKind;

        public SettingsSection Section { get; set; }

        public bool CanMoveUp
        {
            get => _canMoveUp;
            set => Set(ref _canMoveUp, value);
        }

        public bool CanMoveDown
        {
            get => _canMoveDown;
            set => Set(ref _canMoveDown, value);
        }

        public bool IsFavorite => Section == SettingsSection.Favorites;
        public bool IsHidden => Section == SettingsSection.Hidden;
        public bool CanReorder => Section != SettingsSection.Hidden;

        /// <summary>The section changed: re-read the glyph/visibility bindings that depend on it.</summary>
        public void Refresh()
        {
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(IsHidden));
            OnPropertyChanged(nameof(CanReorder));
        }
    }

    /// <summary>
    /// Command management: favourites (and their order), the order of the other commands, and which commands are hidden.
    /// Every change is saved immediately by <see cref="SettingsService"/> and the list rebuilt from it.
    /// </summary>
    public sealed class SettingsViewModel : ObservableObject, IDisposable
    {
        private readonly PaletteCommandRegistry _registry;
        private readonly SettingsService _settings;
        private readonly PaletteCatalog _catalog;
        private readonly Dictionary<string, SettingsRow> _rows = new Dictionary<string, SettingsRow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SettingsRow> _texts = new Dictionary<string, SettingsRow>();

        public SettingsViewModel(PaletteCommandRegistry registry, SettingsService settings, PaletteCatalog catalog)
        {
            _registry = registry;
            _settings = settings;
            _catalog = catalog;

            ToggleFavoriteCommand = new RelayCommand(p => _settings.ToggleFavorite(IdOf(p)));
            ToggleHiddenCommand = new RelayCommand(p =>
            {
                var id = IdOf(p);
                if (id != null) _settings.SetHidden(id, !_settings.IsHidden(id));
            });
            MoveUpCommand = new RelayCommand(p => Move(p as SettingsRow, -1));
            MoveDownCommand = new RelayCommand(p => Move(p as SettingsRow, 1));
            OpenFolderCommand = new RelayCommand(OpenFolder);

            _settings.Changed += OnSettingsChanged;
            Rebuild();
        }

        public ObservableCollection<SettingsRow> Rows { get; } = new ObservableCollection<SettingsRow>();

        public RelayCommand ToggleFavoriteCommand { get; }
        public RelayCommand ToggleHiddenCommand { get; }
        public RelayCommand MoveUpCommand { get; }
        public RelayCommand MoveDownCommand { get; }
        public RelayCommand OpenFolderCommand { get; }

        public string SettingsFolder => Path.GetDirectoryName(_settings.Path);

        public void Dispose() => _settings.Changed -= OnSettingsChanged;

        private void OnSettingsChanged(object sender, EventArgs e) => Rebuild();

        private static string IdOf(object parameter) => (parameter as SettingsRow)?.Command?.Id;

        private void Move(SettingsRow row, int delta)
        {
            if (row == null || !row.IsCommand) return;
            var id = row.Command.Id;
            if (row.Section == SettingsSection.Favorites)
            {
                _settings.MoveFavorite(id, delta);
                return;
            }

            // Commands section: persist the displayed order, keeping the saved position of commands shown elsewhere.
            var shown = Rows.Where(r => r.IsCommand && r.Section == SettingsSection.Commands).Select(r => r.Command.Id).ToList();
            var index = shown.FindIndex(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
            var target = index + delta;
            if (index < 0 || target < 0 || target >= shown.Count) return;
            var moved = shown[index];
            shown.RemoveAt(index);
            shown.Insert(target, moved);
            var others = _settings.Current.Order.Where(o => !shown.Contains(o, StringComparer.OrdinalIgnoreCase));
            _settings.SetOrder(shown.Concat(others));
        }

        private void Rebuild()
        {
            var all = _registry.All.ToList();
            var byId = all.ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);

            var favorites = new List<PaletteCommand>();
            foreach (var id in _settings.Current.Favorites)
            {
                PaletteCommand c;
                if (byId.TryGetValue(id, out c) && !_settings.IsHidden(id)) favorites.Add(c);
            }
            var favoriteIds = new HashSet<string>(favorites.Select(f => f.Id), StringComparer.OrdinalIgnoreCase);
            var hidden = all.Where(c => _settings.IsHidden(c.Id)).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var rest = _catalog.OrderByCustomOrder(all.Where(c => !_settings.IsHidden(c.Id) && !favoriteIds.Contains(c.Id)));

            var desired = new List<SettingsRow>();
            desired.Add(Text("Favorites", true));
            if (favorites.Count == 0) desired.Add(Text("Star a command to pin it to the top of Palette.", false));
            desired.AddRange(CommandRows(favorites, SettingsSection.Favorites));
            desired.Add(Text("Commands", true));
            desired.AddRange(CommandRows(rest, SettingsSection.Commands));
            if (hidden.Count > 0)
            {
                desired.Add(Text("Hidden", true));
                desired.AddRange(CommandRows(hidden, SettingsSection.Hidden));
            }
            CollectionSync.Sync(Rows, desired);
        }

        private IEnumerable<SettingsRow> CommandRows(IList<PaletteCommand> commands, SettingsSection section)
        {
            for (var i = 0; i < commands.Count; i++)
            {
                SettingsRow row;
                var c = commands[i];
                if (!_rows.TryGetValue(c.Id, out row) || !ReferenceEquals(row.Command, c))
                {
                    row = SettingsRow.ForCommand(c);
                    _rows[c.Id] = row;
                }
                row.Section = section;
                row.CanMoveUp = section != SettingsSection.Hidden && i > 0;
                row.CanMoveDown = section != SettingsSection.Hidden && i < commands.Count - 1;
                // Section decides the star/eye glyphs; tell bindings that read it.
                row.Refresh();
                yield return row;
            }
        }

        private SettingsRow Text(string text, bool header)
        {
            SettingsRow row;
            var key = (header ? "h:" : "t:") + text;
            if (!_texts.TryGetValue(key, out row))
            {
                row = header ? SettingsRow.ForHeader(text) : SettingsRow.ForHint(text);
                _texts[key] = row;
            }
            return row;
        }

        private void OpenFolder()
        {
            try
            {
                var folder = SettingsFolder;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Opening the settings folder failed", ex);
            }
        }
    }
}
