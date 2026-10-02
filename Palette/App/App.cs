using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Palette.Core;
using Palette.Revit;
using ricaun.Revit.UI;

namespace Palette
{
    /// <summary>Add-in entry point: ribbon button, shared services, and a one-time window warm-up on the first idle.</summary>
    [AppLoader]
    public class App : IExternalApplication
    {
        public const string TabName = "RK Tools";
        public const string PanelName = "Palette";

        private const string DarkIcon = "pack://application:,,,/Palette;component/Assets/Dark%20-%20Palette.png";
        private const string LightIcon = "pack://application:,,,/Palette;component/Assets/Light%20-%20Palette.png";
        private const string DarkIconSmall = "pack://application:,,,/Palette;component/Assets/Dark%20-%20Palette16.png";
        private const string LightIconSmall = "pack://application:,,,/Palette;component/Assets/Light%20-%20Palette16.png";

        private RibbonPanel _ribbonPanel;
        private PushButton _button;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                PaletteHost.Initialize(application);
                Diagnostics.SelfTest.TryAttach(application); // inert unless PALETTE_SELFTEST_OUTPUT is set

                try { application.CreateRibbonTab(TabName); } catch { /* tab already exists (other RK Tools plugins) */ }
                _ribbonPanel = application.CreateOrSelectPanel(TabName, PanelName);

                _button = _ribbonPanel.CreatePushButton<OpenPaletteCommand>("Palette");
                _button.SetToolTip("Palette – quick command launcher. Type to search, Enter to run. Assign a keyboard shortcut to this button in Revit for the fastest access.");
                ApplyThemeIcon();

                // Follow Revit's light/dark UI theme while Revit runs, not only at startup.
                application.ThemeChanged += OnThemeChanged;
                application.Idling += OnFirstIdling;

                PaletteLog.Info("Palette " + typeof(App).Assembly.GetName().Version + " loaded.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Palette failed to start", ex);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            application.ThemeChanged -= OnThemeChanged;
            application.Idling -= OnFirstIdling;
            PaletteHost.Shutdown();
            _ribbonPanel?.Remove();
            return Result.Succeeded;
        }

        private static void OnFirstIdling(object sender, IdlingEventArgs e)
        {
            var app = sender as UIApplication;
            if (app != null) app.Idling -= OnFirstIdling;
            PaletteHost.Prewarm();
        }

        private void OnThemeChanged(object sender, ThemeChangedEventArgs e) => ApplyThemeIcon();

        /// <summary>Light icon on Revit's dark theme, dark icon on the light theme.</summary>
        private void ApplyThemeIcon()
        {
            try
            {
                var dark = UIThemeManager.CurrentTheme == UITheme.Dark;
                _button?.SetLargeImage(dark ? DarkIcon : LightIcon).SetImage(dark ? DarkIconSmall : LightIconSmall);
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Setting the ribbon icon failed", ex);
            }
        }
    }
}
