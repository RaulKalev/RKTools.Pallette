using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Palette.UI.Mvvm;

namespace Palette.UI.ViewModels
{
    /// <summary>One row of the result list: a section header or a command. Instances are cached and reused between searches.</summary>
    public sealed class ResultRow : ObservableObject
    {
        private bool _isFavorite;
        private bool _isSelected;

        private ResultRow() { }

        public static ResultRow ForHeader(string text) => new ResultRow { IsHeader = true, Header = text };

        public static ResultRow ForCommand(PaletteCommand command) => new ResultRow { Command = command };

        public bool IsHeader { get; private set; }
        public string Header { get; private set; }
        public PaletteCommand Command { get; private set; }

        public string Name => Command?.Name;
        public string Category => Command?.Category;
        public string Description => Command?.Description;
        public string IconKind => Command?.IconKind;

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (Set(ref _isFavorite, value)) OnPropertyChanged(nameof(ShowStar));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (Set(ref _isSelected, value)) OnPropertyChanged(nameof(ShowStar));
            }
        }

        /// <summary>The favourite star is shown for favourites and for the selected row (to offer "make favourite").</summary>
        public bool ShowStar => !IsHeader && (_isFavorite || _isSelected);
    }

    /// <summary>
    /// State of the launcher: the query, the visible rows and the selection. All filtering is in-memory work in
    /// <see cref="PaletteCatalog"/>, so it runs synchronously on every keystroke.
    /// </summary>
    public sealed class PaletteViewModel : ObservableObject
    {
        private readonly PaletteCatalog _catalog;
        private readonly SettingsService _settings;
        private readonly Action<PaletteCommand> _launch;
        private readonly Action _openSettings;
        private readonly Dictionary<string, ResultRow> _commandRows = new Dictionary<string, ResultRow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ResultRow> _headerRows = new Dictionary<string, ResultRow>();

        private PaletteContext _context = PaletteContext.Empty;
        private string _query = "";
        private ResultRow _selectedRow;

        public PaletteViewModel(PaletteCatalog catalog, SettingsService settings, Action<PaletteCommand> launch, Action openSettings)
        {
            _catalog = catalog;
            _settings = settings;
            _launch = launch;
            _openSettings = openSettings;
        }

        public ObservableCollection<ResultRow> Rows { get; } = new ObservableCollection<ResultRow>();

        public string Query
        {
            get => _query;
            set
            {
                if (Set(ref _query, value ?? "")) Refresh(null);
            }
        }

        public ResultRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (value != null && value.IsHeader) return; // headers are never selectable
                var old = _selectedRow;
                if (!Set(ref _selectedRow, value)) return;
                if (old != null) old.IsSelected = false;
                if (value != null) value.IsSelected = true;
            }
        }

        public bool HasRows => Rows.Count > 0;

        public string EmptyMessage => string.IsNullOrWhiteSpace(_query)
            ? "No commands available."
            : "No commands match “" + _query.Trim() + "”.";

        /// <summary>Prepares the view model for a fresh open: empty query, current context, first command selected.</summary>
        public void Open(PaletteContext context)
        {
            _context = context ?? PaletteContext.Empty;
            _query = "";
            OnPropertyChanged(nameof(Query));
            Refresh(null);
        }

        public void MoveSelection(int delta)
        {
            var commands = Rows.Where(r => !r.IsHeader).ToList();
            if (commands.Count == 0) return;
            var index = SelectedRow == null ? -1 : commands.IndexOf(SelectedRow);
            int next;
            if (index < 0) next = delta > 0 ? 0 : commands.Count - 1;
            else next = (index + delta + commands.Count) % commands.Count;
            if (Math.Abs(delta) > 1) next = Math.Max(0, Math.Min(commands.Count - 1, index + delta)); // paging does not wrap
            SelectedRow = commands[next];
        }

        public void LaunchSelected()
        {
            if (SelectedRow != null) Launch(SelectedRow);
        }

        public void Launch(ResultRow row)
        {
            if (row == null || row.IsHeader || row.Command == null) return;
            _launch(row.Command);
        }

        public void ToggleFavorite(ResultRow row)
        {
            if (row == null || row.IsHeader) return;
            _settings.ToggleFavorite(row.Command.Id);
            Refresh(row.Command.Id);
        }

        public void ToggleFavoriteOfSelection() => ToggleFavorite(SelectedRow);

        public void OpenSettings() => _openSettings();

        private void Refresh(string keepSelectionId)
        {
            var entries = string.IsNullOrWhiteSpace(_query)
                ? _catalog.BuildHome(_context)
                : _catalog.BuildSearch(_query, _context);

            var desired = new List<ResultRow>(entries.Count);
            foreach (var e in entries)
            {
                var row = ToRow(e);
                desired.Add(row);
            }
            CollectionSync.Sync(Rows, desired);

            ResultRow selection = null;
            if (keepSelectionId != null)
                selection = desired.FirstOrDefault(r => !r.IsHeader && string.Equals(r.Command.Id, keepSelectionId, StringComparison.OrdinalIgnoreCase));
            if (selection == null) selection = desired.FirstOrDefault(r => !r.IsHeader);
            SelectedRow = selection;

            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EmptyMessage));
        }

        private ResultRow ToRow(ListEntry entry)
        {
            ResultRow row;
            if (entry.Kind == EntryKind.Header)
            {
                if (!_headerRows.TryGetValue(entry.Header, out row))
                {
                    row = ResultRow.ForHeader(entry.Header);
                    _headerRows[entry.Header] = row;
                }
                return row;
            }

            // A re-registered command (same id, new instance) gets a fresh row so the old metadata is not shown.
            if (!_commandRows.TryGetValue(entry.Command.Id, out row) || !ReferenceEquals(row.Command, entry.Command))
            {
                row = ResultRow.ForCommand(entry.Command);
                _commandRows[entry.Command.Id] = row;
            }
            row.IsFavorite = _settings.IsFavorite(entry.Command.Id);
            return row;
        }
    }
}
