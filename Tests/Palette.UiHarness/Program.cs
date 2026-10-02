using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Palette.Core;
using Palette.Core.Commands;
using Palette.Core.Settings;
using Palette.Infrastructure;
using Palette.UI;
using Palette.UI.ViewModels;

namespace Palette.UiHarness
{
    internal static class Program
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _failures;

        [STAThread]
        private static int Main(string[] args)
        {
            var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "PaletteHarness");
            Directory.CreateDirectory(output);
            PaletteLog.LogFilePath = Path.Combine(output, "palette.log");

            var registry = new PaletteCommandRegistry();
            var settings = new SettingsService(Path.Combine(output, "settings.json"));
            var recents = new RecentCommandsService(Path.Combine(output, "recent.json"));
            if (File.Exists(settings.Path)) File.Delete(settings.Path);
            if (File.Exists(recents.Path)) File.Delete(recents.Path);
            Seed(registry);
            var catalog = new PaletteCatalog(registry, settings, recents);
            settings.SetFavorite("camera.fov", true);
            settings.SetFavorite("panel.schematics", true);
            settings.SetFavorite("pulse.panel", true);
            settings.SetHidden("eule.experimental", true);
            recents.RecordUse("revit.pdf");
            recents.RecordUse("revit.links");

            Action<PaletteCommand> launch = c => Say("launch " + c.Id);
            Action openSettings = () => Say("open settings");

            foreach (var dark in new[] { true, false })
            {
                ThemeManager.DarkModeProvider = () => dark;
                var tag = dark ? "dark" : "light";

                var window = new PaletteWindow(catalog, settings, launch, openSettings);
                var context = new PaletteContext(hasDocument: true);
                window.Open(context);
                Flush();
                Capture(window, Path.Combine(output, "palette_home_" + tag + ".png"));

                ((PaletteViewModel)window.DataContext).Query = "pan";
                Flush();
                Capture(window, Path.Combine(output, "palette_search_pan_" + tag + ".png"));

                ((PaletteViewModel)window.DataContext).Query = "cam";
                Flush();
                Capture(window, Path.Combine(output, "palette_search_cam_" + tag + ".png"));

                ((PaletteViewModel)window.DataContext).Query = "zzzz";
                Flush();
                Capture(window, Path.Combine(output, "palette_nomatch_" + tag + ".png"));
                window.HidePalette();
                window.CloseForShutdown();

                var settingsWindow = new SettingsWindow(registry, settings, catalog);
                settingsWindow.Show();
                Flush();
                Capture(settingsWindow, Path.Combine(output, "settings_" + tag + ".png"));
                settingsWindow.Close();
            }

            CheckPlacement(catalog, settings, launch, openSettings);

            File.WriteAllText(Path.Combine(output, "harness-report.txt"), Report.ToString());
            return _failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ placement against the real monitors

        private static void CheckPlacement(PaletteCatalog catalog, SettingsService settings, Action<PaletteCommand> launch, Action openSettings)
        {
            ThemeManager.DarkModeProvider = () => true;
            var window = new PaletteWindow(catalog, settings, launch, openSettings);
            POINT original;
            GetCursorPos(out original);
            try
            {
                var monitors = Monitors();
                Say("monitors: " + string.Join("; ", monitors.Select(m => m.Left + "," + m.Top + " " + (m.Right - m.Left) + "x" + (m.Bottom - m.Top))));
                foreach (var work in monitors)
                {
                    var spots = new[]
                    {
                        new[] { work.Left + 5, work.Top + 5 },
                        new[] { work.Right - 5, work.Top + 5 },
                        new[] { work.Left + 5, work.Bottom - 5 },
                        new[] { work.Right - 5, work.Bottom - 5 },
                        new[] { (work.Left + work.Right) / 2, (work.Top + work.Bottom) / 2 }
                    };
                    foreach (var s in spots)
                    {
                        SetCursorPos(s[0], s[1]);
                        window.Open(new PaletteContext(hasDocument: true));
                        Flush();
                        RECT r;
                        GetWindowRect(new System.Windows.Interop.WindowInteropHelper(window).Handle, out r);
                        var inside = r.Left >= work.Left && r.Top >= work.Top && r.Right <= work.Right && r.Bottom <= work.Bottom;
                        Check(inside, "window " + r.Left + "," + r.Top + "-" + r.Right + "," + r.Bottom + " inside work area " +
                                      work.Left + "," + work.Top + "-" + work.Right + "," + work.Bottom + " for cursor " + s[0] + "," + s[1]);
                        window.HidePalette();
                    }
                }
            }
            finally
            {
                SetCursorPos(original.X, original.Y);
                window.CloseForShutdown();
            }
        }

        private static List<RECT> Monitors()
        {
            var list = new List<RECT>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr dc, ref RECT rc, IntPtr data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(m, ref info)) list.Add(info.rcWork);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        // ------------------------------------------------------------------ helpers

        private static void Seed(PaletteCommandRegistry r)
        {
            var nop = new DelegateAction(c => { });
            Func<string, string, string, string, string[], PaletteCommand> make = (id, name, cat, icon, kw) =>
                new PaletteCommand(id, name, nop, cat, "Demo command: " + name, kw, null, icon, "harness");
            r.Register(make("camera.fov", "Camera FOV", "Security", "Cctv", new[] { "camera", "cctv", "fov", "coverage" }));
            r.Register(make("panel.schematics", "Panel Schematics", "Electrical", "ElectricSwitch", new[] { "panel", "schematic" }));
            r.Register(make("panel.edit", "Edit Panel", "Electrical", "TableEdit", null));
            r.Register(make("pulse.panel", "Pulse — Analyse Panel", "Fire Alarm", "FireAlert", null));
            r.Register(make("doors.set", "Create Door Set", "Security", "DoorOpen", null));
            r.Register(make("revit.links", "Manage Links", "Revit", "LinkVariant", new[] { "link" }));
            r.Register(make("revit.vg", "Visibility/Graphics", "Views", "EyeOutline", new[] { "vg" }));
            r.Register(make("revit.pdf", "Export PDF", "Documentation", "FilePdfBox", new[] { "print" }));
            r.Register(make("revit.sync", "Synchronize with Central", "Coordination", "Sync", null));
            r.Register(make("palette.settings", "Palette Settings", "Palette", "CogOutline", null));
            r.Register(make("eule.experimental", "Experimental Tool", "EULE", "Flask", null));
        }

        private static void Flush() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

        private static void Capture(Window window, string path)
        {
            window.UpdateLayout();
            var w = (int)Math.Ceiling(window.ActualWidth);
            var h = (int)Math.Ceiling(window.ActualHeight);
            var rtb = new RenderTargetBitmap(w * 2, h * 2, 192, 192, PixelFormats.Pbgra32);
            var visual = (Visual)window.Content;
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen())
                dc.DrawRectangle((Brush)window.FindResource("Window.Background"), null, new Rect(0, 0, w, h));
            rtb.Render(bg);
            rtb.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) encoder.Save(fs);
            Say("wrote " + Path.GetFileName(path) + " (" + w + "x" + h + ")");
        }

        private static void Check(bool ok, string message)
        {
            if (!ok) _failures++;
            Say((ok ? "PASS " : "FAIL ") + message);
        }

        private static void Say(string line) => Report.AppendLine(line);

        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO i);
        private delegate bool MonitorEnumProc(IntPtr m, IntPtr dc, ref RECT rc, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    }
}
