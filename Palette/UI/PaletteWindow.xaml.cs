using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Palette.Infrastructure;
using Palette.UI.ViewModels;

namespace Palette.UI
{
    /// <summary>
    /// The launcher overlay. Created once and shown/hidden, so reopening it is cheap. Code-behind handles chrome only:
    /// placement near the cursor, keyboard focus, key handling and closing when focus leaves. Behaviour lives in
    /// <see cref="PaletteViewModel"/>; running the chosen command is delegated to the host (ExternalEvent).
    /// </summary>
    public partial class PaletteWindow : Window
    {
        private const double PlacementHeightDips = 450; // header 52 + list 360 + footer 32 + borders, rounded up

        private readonly ThemeManager _theme;
        private readonly PaletteViewModel _viewModel;
        private PlacementInfo _placement = new PlacementInfo { BottomAnchorPx = -1, Scale = 1.0 };

        /// <summary>Hide-on-deactivate is only armed once the window is really active after opening (see <see cref="Open"/>).</summary>
        private bool _armed;
        private bool _shuttingDown;

        internal PaletteWindow(PaletteCatalog catalog, SettingsService settings, Action<PaletteCommand> launch, Action openSettings)
        {
            InitializeComponent();
            _theme = new ThemeManager(this);
            _theme.ApplyIfChanged();

            _viewModel = new PaletteViewModel(
                catalog,
                settings,
                command =>
                {
                    HidePalette();
                    launch(command);
                },
                () =>
                {
                    HidePalette();
                    openSettings();
                });
            DataContext = _viewModel;

            SourceInitialized += (s, e) => ApplyRoundedCorners();
            PreviewKeyDown += OnPreviewKeyDown;
            Deactivated += OnDeactivated;
            Closing += OnClosing;
            SizeChanged += (s, e) => WindowPositioner.KeepBottomAnchored(this, _placement);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>Windows 11 rounded window corners (DWMWA_WINDOW_CORNER_PREFERENCE = ROUND), like the other RK Tools windows.</summary>
        private void ApplyRoundedCorners()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int round = 2;
                DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
            }
            catch { /* older Windows: square corners */ }
        }

        /// <summary>Shows Palette next to the cursor with an empty search box that has keyboard focus.</summary>
        internal void Open(PaletteContext context)
        {
            _armed = false;
            _theme.ApplyIfChanged();
            _viewModel.Open(context);

            _placement = WindowPositioner.PlaceNearCursor(this, Width, PlacementHeightDips);
            if (!IsVisible) Show();
            WindowPositioner.Reapply(this, _placement); // WPF may position a window on its first Show
            Activate();
            FocusSearch();

            // Revit finishes the ribbon command (and may re-activate its own window) after we return from here.
            // Only once the queue is idle do we take focus again and start closing on deactivation.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (!IsVisible) return;
                Activate();
                FocusSearch();
                _armed = true;
            }));
        }

        internal void HidePalette()
        {
            _armed = false;
            if (IsVisible) Hide();
        }

        internal void CloseForShutdown()
        {
            _shuttingDown = true;
            Close();
        }

        private void FocusSearch()
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
            SearchBox.CaretIndex = SearchBox.Text.Length;
        }

        // ------------------------------------------------------------------ keyboard

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            switch (key)
            {
                case Key.Escape:
                    HidePalette();
                    break;
                case Key.Down:
                    _viewModel.MoveSelection(1);
                    break;
                case Key.Up:
                    _viewModel.MoveSelection(-1);
                    break;
                case Key.PageDown:
                    _viewModel.MoveSelection(6);
                    break;
                case Key.PageUp:
                    _viewModel.MoveSelection(-6);
                    break;
                case Key.Enter:
                    _viewModel.LaunchSelected();
                    break;
                case Key.D when ctrl:
                    _viewModel.ToggleFavoriteOfSelection();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        // ------------------------------------------------------------------ mouse

        private void OnRowMouseUp(object sender, MouseButtonEventArgs e)
        {
            var row = (sender as ListBoxItem)?.DataContext as ResultRow;
            _viewModel.Launch(row);
            e.Handled = true;
        }

        private void OnStarClick(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleFavorite((sender as FrameworkElement)?.DataContext as ResultRow);
            e.Handled = true;
            FocusSearch();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e) => _viewModel.OpenSettings();

        // ------------------------------------------------------------------ focus and closing

        /// <summary>Clicking outside (any other window getting focus) dismisses the launcher.</summary>
        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_armed && IsVisible) HidePalette();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_shuttingDown) return;
            e.Cancel = true; // Alt+F4 etc.: keep the instance, just hide it
            HidePalette();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PaletteViewModel.SelectedRow)) return;
            var row = _viewModel.SelectedRow;
            if (row == null) return;

            // With a section header above the first command, scroll the header into view too.
            var index = _viewModel.Rows.IndexOf(row);
            ResultsList.ScrollIntoView(index == 1 && _viewModel.Rows[0].IsHeader ? _viewModel.Rows[0] : row);
        }
    }
}
