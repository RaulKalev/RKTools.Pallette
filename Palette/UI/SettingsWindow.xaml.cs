using System;
using System.Windows;
using System.Windows.Input;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Palette.UI.ViewModels;

namespace Palette.UI
{
    /// <summary>Command management window (favorites, order, visibility). All behaviour lives in <see cref="SettingsViewModel"/>.</summary>
    public partial class SettingsWindow : Window
    {
        private readonly ThemeManager _theme;
        private readonly SettingsViewModel _viewModel;

        internal SettingsWindow(PaletteCommandRegistry registry, SettingsService settings, PaletteCatalog catalog)
        {
            InitializeComponent();
            _theme = new ThemeManager(this);
            _theme.ApplyIfChanged();

            _viewModel = new SettingsViewModel(registry, settings, catalog);
            DataContext = _viewModel;

            PreviewKeyDown += OnPreviewKeyDown;
            Closed += (s, e) => _viewModel.Dispose();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            Close();
            e.Handled = true;
        }
    }
}
