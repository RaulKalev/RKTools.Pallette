using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.UI;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Palette.UI;

namespace Palette.Revit
{
    /// <summary>
    /// Wires the pieces together: the registry and settings services (created once, usable before Revit finishes
    /// starting so plugins may register early), the ExternalEvent executor, and the two windows. Everything here runs on
    /// Revit's UI thread; the only place that touches the Revit API is the ribbon command that opens Palette.
    /// </summary>
    internal static class PaletteHost
    {
        public static readonly PaletteCommandRegistry Registry = new PaletteCommandRegistry();
        public static readonly SettingsService Settings = new SettingsService();
        public static readonly RecentCommandsService Recents = new RecentCommandsService();
        public static readonly PaletteCatalog Catalog = new PaletteCatalog(Registry, Settings, Recents);

        private static CommandExecutor _executor;
        private static PaletteWindow _paletteWindow;
        private static SettingsWindow _settingsWindow;
        private static IntPtr _mainWindow;
        private static string _manifestSignature;
        private static readonly HashSet<string> ManifestSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static PaletteWindow CurrentWindow => _paletteWindow;
        internal static SettingsWindow CurrentSettingsWindow => _settingsWindow;

        public static IEnumerable<string> ManifestFolders => new[]
        {
            Path.Combine(SettingsService.DefaultFolder, "Commands"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Palette", "Commands")
        };

        /// <summary>Add-in startup (valid API context): load user data, register built-in commands, create the executor.</summary>
        public static void Initialize(UIControlledApplication app)
        {
            _mainWindow = app.MainWindowHandle;
            ThemeManager.DarkModeProvider = () => UIThemeManager.CurrentTheme == UITheme.Dark;

            Settings.Load();
            Recents.Load();
            if (Settings.LoadError != null) PaletteLog.Warn("Settings could not be read and defaults are in use: " + Settings.LoadError);

            Registry.RegisterRange(BuiltInCommands.Create());
            Registry.Register(new PaletteCommand(
                id: "palette.settings",
                name: "Palette Settings",
                action: new DelegateAction(c => OpenSettings()),
                category: "Palette",
                description: "Choose which commands appear, favorites and their order",
                keywords: new[] { "configure", "customize", "favorites", "hide", "order", "preferences", "shortcut" },
                iconKind: "CogOutline",
                source: "palette"));

            _executor = new CommandExecutor();
        }

        /// <summary>First idle after startup: build the window once (XAML parse) so the first open is as fast as later ones.</summary>
        public static void Prewarm()
        {
            try { EnsureWindow(); }
            catch (Exception ex) { PaletteLog.Error("Pre-creating the Palette window failed", ex); }
        }

        /// <summary>Called from the ribbon command (valid API context): snapshot the context, then show Palette at the cursor.</summary>
        public static void ShowPalette(UIApplication app)
        {
            RefreshManifests();
            var context = ContextBuilder.Build(app, Registry);
            EnsureWindow().Open(context);
        }

        /// <summary>Called by the window (UI thread, no API context): remember the launch, then hand over to the executor.</summary>
        public static void Launch(PaletteCommand command)
        {
            Recents.RecordUse(command.Id);
            _executor.Enqueue(command);
        }

        public static void OpenSettings()
        {
            try
            {
                if (_settingsWindow == null)
                {
                    _settingsWindow = new SettingsWindow(Registry, Settings, Catalog);
                    new WindowInteropHelper(_settingsWindow) { Owner = _mainWindow };
                    _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                }
                _settingsWindow.Show();
                _settingsWindow.Activate();
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Opening the settings window failed", ex);
                _settingsWindow = null;
            }
        }

        public static void Shutdown()
        {
            try { _paletteWindow?.CloseForShutdown(); } catch (Exception ex) { PaletteLog.Error("Closing Palette failed", ex); }
            try { _settingsWindow?.Close(); } catch (Exception ex) { PaletteLog.Error("Closing settings failed", ex); }
            _paletteWindow = null;
            _settingsWindow = null;
        }

        private static PaletteWindow EnsureWindow()
        {
            if (_paletteWindow != null) return _paletteWindow;
            var window = new PaletteWindow(Catalog, Settings, Launch, OpenSettings);
            new WindowInteropHelper(window) { Owner = _mainWindow };
            _paletteWindow = window;
            return window;
        }

        /// <summary>Loads *.palette.json manifests, but only when a file was added, removed or edited since the last scan.</summary>
        private static void RefreshManifests()
        {
            try
            {
                var folders = ManifestFolders.ToList();
                var signature = ManifestLoader.Signature(folders);
                if (signature == _manifestSignature) return;
                _manifestSignature = signature;

                foreach (var source in ManifestSources) Registry.UnregisterSource(source);
                ManifestSources.Clear();

                var result = ManifestLoader.Load(folders);
                foreach (var error in result.Errors) PaletteLog.Warn("Manifest: " + error);
                foreach (var file in result.Files)
                {
                    var source = "manifest:" + Path.GetFileName(file.FilePath);
                    ManifestSources.Add(source);
                    var label = string.IsNullOrWhiteSpace(file.Source) ? source : file.Source;
                    foreach (var c in file.Commands)
                    {
                        try
                        {
                            var action = !string.IsNullOrWhiteSpace(c.PostableCommand)
                                ? PostCommandAction.ForPostable(c.PostableCommand.Trim())
                                : PostCommandAction.ForCommandId(c.RevitCommandId.Trim());
                            Registry.Register(new PaletteCommand(c.Id, c.Name, action, c.Category, c.Description, c.Keywords,
                                c.Aliases, c.Icon, source, ctx => ctx.IsPostable(action.AvailabilityKey)));
                        }
                        catch (Exception ex)
                        {
                            PaletteLog.Error("Registering manifest command '" + c.Id + "' from " + label + " failed", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PaletteLog.Error("Reading command manifests failed", ex);
            }
        }
    }
}
