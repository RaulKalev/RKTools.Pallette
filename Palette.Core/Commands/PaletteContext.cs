using System;
using System.Collections.Generic;

namespace Palette.Core.Commands
{
    /// <summary>
    /// Snapshot of the Revit state when Palette opened. Built inside a valid Revit API context (the ribbon command),
    /// then handed to <see cref="PaletteCommand.Availability"/> rules from the UI thread, which must not touch the API.
    /// Context-aware commands ("a panel is selected") read the selection fields.
    /// </summary>
    public sealed class PaletteContext
    {
        public static readonly PaletteContext Empty = new PaletteContext();

        private readonly HashSet<string> _postable;
        private readonly HashSet<string> _selectedCategories;

        public PaletteContext(
            bool hasDocument = false,
            bool isFamilyDocument = false,
            string documentTitle = null,
            string activeViewType = null,
            int selectionCount = 0,
            IEnumerable<string> selectedCategories = null,
            IEnumerable<string> postableCommands = null)
        {
            HasDocument = hasDocument;
            IsFamilyDocument = isFamilyDocument;
            DocumentTitle = documentTitle ?? "";
            ActiveViewType = activeViewType ?? "";
            SelectionCount = selectionCount;
            _selectedCategories = new HashSet<string>(selectedCategories ?? new string[0], StringComparer.OrdinalIgnoreCase);
            _postable = new HashSet<string>(postableCommands ?? new string[0], StringComparer.Ordinal);
        }

        public bool HasDocument { get; }
        public bool IsFamilyDocument { get; }
        public string DocumentTitle { get; }

        /// <summary>Name of the active view's ViewType ("FloorPlan", "ThreeD", ...), empty without a view.</summary>
        public string ActiveViewType { get; }

        public int SelectionCount { get; }

        /// <summary>Category names of the selected elements (capped by the host), e.g. "Electrical Equipment".</summary>
        public IReadOnlyCollection<string> SelectedCategories => _selectedCategories;

        public bool IsCategorySelected(string categoryName) => categoryName != null && _selectedCategories.Contains(categoryName);

        /// <summary>
        /// True when the host verified (CanPostCommand) that the command registered under this key can be posted right
        /// now. Keys are chosen by the command adapter, e.g. "postable:ManageLinks".
        /// </summary>
        public bool IsPostable(string key) => key != null && _postable.Contains(key);
    }
}
