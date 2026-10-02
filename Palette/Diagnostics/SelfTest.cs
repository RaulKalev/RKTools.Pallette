using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Palette.Core;
using Palette.Infrastructure;
using Palette.Revit;
using Palette.UI.ViewModels;

namespace Palette.Diagnostics
{
    /// <summary>
    /// Opt-in end-to-end self-test that runs inside Revit. It is inert unless the environment variable
    /// PALETTE_SELFTEST_OUTPUT points to a folder (see Tools/Run-RevitSelfTest.ps1). It never touches user documents: it
    /// creates its own empty project, opens Palette like the ribbon button does, drives the window with real routed key
    /// events, runs a command through the ExternalEvent route and writes selftest-results.txt.
    /// </summary>
    internal static class SelfTest
    {
        public const string OutputVariable = "PALETTE_SELFTEST_OUTPUT";
        public const string ExitVariable = "PALETTE_SELFTEST_EXIT";

        private static readonly StringBuilder Log = new StringBuilder();
        private static string _out;
        private static int _state;
        private static int _ticks;
        private static int _passed, _failed;

        public static void TryAttach(UIControlledApplication app)
        {
            _out = Environment.GetEnvironmentVariable(OutputVariable);
            if (string.IsNullOrWhiteSpace(_out)) return;
            app.Idling += OnIdling;
            PaletteLog.Info("Self-test armed, output: " + _out);
        }

        private static void OnIdling(object sender, IdlingEventArgs e)
        {
            var app = sender as UIApplication;
            if (app == null) return;
            e.SetRaiseWithoutDelay();
            _ticks++;
            try
            {
                if (!Step(app)) return;
            }
            catch (Exception ex)
            {
                Fail("Self-test crashed: " + ex);
            }
            Finish(app);
        }

        // Returns true when the test is finished.
        private static bool Step(UIApplication app)
        {
            switch (_state)
            {
                case 0: // let Revit settle, then create and open a scratch project
                    if (_ticks < 30) return false;
                    Directory.CreateDirectory(_out);
                    Say("Palette self-test " + typeof(SelfTest).Assembly.GetName().Version + " in " + app.Application.VersionName +
                        " (" + app.Application.VersionBuild + "), " + Environment.Version);
                    var path = Path.Combine(_out, "palette-selftest.rvt");
                    var doc = app.Application.NewProjectDocument(UnitSystem.Metric);
                    doc.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
                    doc.Close(false);
                    app.OpenAndActivateDocument(path);
                    return Next();

                case 1: // project active: check the ribbon, then open Palette the way the ribbon command does
                    if (app.ActiveUIDocument == null) return Timeout(300, "scratch project did not become active");
                    CheckRibbon();
                    Check(PaletteHost.Registry.Count > 10, "registry holds built-in commands (" + PaletteHost.Registry.Count + ")");
                    CursorTo(0.5, 0.5);
                    PaletteHost.ShowPalette(app);
                    return Next();

                case 2: // palette is open: focus, position, content
                    if (_ticks - _mark < 20) return false;
                    CheckOpenState("centre");
                    var vm = Vm();
                    Check(vm.SelectedRow != null && !vm.SelectedRow.IsHeader, "a command is selected on open");
                    Check(vm.Query == "", "search box starts empty");

                    vm.Query = "links";
                    Check(vm.Rows.Any(r => !r.IsHeader && r.Command.Id == "revit.ManageLinks"), "typing \"links\" lists Manage Links");
                    Check(vm.SelectedRow != null && vm.SelectedRow.Command.Id == "revit.ManageLinks", "best match is selected");

                    PressKey(System.Windows.Input.Key.Down);
                    Check(vm.SelectedRow != null, "Down keeps a selection");
                    PressKey(System.Windows.Input.Key.Up);
                    vm.Query = "";
                    PressKey(System.Windows.Input.Key.Escape);
                    Check(!Window().IsVisible, "Esc hides the palette");

                    CursorTo(0.999, 0.999);
                    PaletteHost.ShowPalette(app);
                    return Next();

                case 3: // flipped placement at the bottom-right corner, then run a command with Enter
                    if (_ticks - _mark < 20) return false;
                    CheckOpenState("bottom-right corner");
                    Vm().Query = "default 3d";
                    Check(Vm().SelectedRow?.Command.Id == "revit.Default3DView", "typing \"default 3d\" selects Default 3D View");
                    PressKey(System.Windows.Input.Key.Enter);
                    Check(!Window().IsVisible, "Enter hides the palette");
                    Check(PaletteHost.Recents.UseCount("revit.Default3DView") == 1, "launch is recorded as recently used");
                    return Next();

                case 4: // the ExternalEvent ran PostCommand: the active view should now be a 3D view
                    var uidoc = app.ActiveUIDocument;
                    if (uidoc != null && uidoc.ActiveView != null && uidoc.ActiveView.ViewType == ViewType.ThreeD)
                    {
                        Check(true, "PostCommand executed: active view is " + uidoc.ActiveView.Name);
                        PaletteHost.OpenSettings();
                        return Next();
                    }
                    return Timeout(600, "Default 3D View was not executed (active view: " + app.ActiveUIDocument?.ActiveView?.ViewType + ")");

                case 5:
                    if (_ticks - _mark < 20) return false;
                    var settings = PaletteHost.CurrentSettingsWindow;
                    Check(settings != null && settings.IsVisible, "settings window opens");
                    settings?.Close();

                    // a command that is not available must be hidden: no document-dependent command without a document is checked
                    // in the unit tests; here check that an unknown postable simply is not offered.
                    Check(!PaletteHost.Registry.All.Any(c => c.Id == "revit.DoesNotExist"), "unknown commands are not registered");
                    return true;
            }
            return true;
        }

        // ------------------------------------------------------------------ checks

        private static void CheckRibbon()
        {
            try
            {
                var tab = Autodesk.Windows.ComponentManager.Ribbon.Tabs.FirstOrDefault(t => t.Title == App.TabName);
                var panel = tab?.Panels.FirstOrDefault(p => p.Source?.Title == App.PanelName);
                var item = panel?.Source?.Items.FirstOrDefault(i => (i as Autodesk.Windows.RibbonItem)?.Text == "Palette");
                Check(tab != null, "ribbon tab \"" + App.TabName + "\" exists");
                Check(panel != null, "ribbon panel \"" + App.PanelName + "\" exists");
                Check(item != null, "ribbon button \"Palette\" exists");
            }
            catch (Exception ex)
            {
                Fail("Ribbon check failed: " + ex.Message);
            }
        }

        private static void CheckOpenState(string where)
        {
            var w = Window();
            Check(w.IsVisible, where + ": palette is visible");
            Check(w.IsActive, where + ": palette window is the active window");
            var focused = Keyboard.FocusedElement as System.Windows.Controls.TextBox;
            Check(focused != null && focused.Name == "SearchBox", where + ": keyboard focus is in the search box (" + (Keyboard.FocusedElement?.GetType().Name ?? "none") + ")");

            RECT r;
            var hwnd = new WindowInteropHelper(w).Handle;
            GetWindowRect(hwnd, out r);
            POINT cursor;
            GetCursorPos(out cursor);
            var monitor = MonitorFromPoint(cursor, 2);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(monitor, ref info);
            var work = info.rcWork;
            Check(r.Left >= work.Left && r.Top >= work.Top && r.Right <= work.Right && r.Bottom <= work.Bottom,
                where + ": window " + Rect(r) + " is inside the work area " + Rect(work) + " (cursor " + cursor.X + "," + cursor.Y + ")");
            var nearCursor = Math.Abs(r.Left - cursor.X) < 700 && Math.Abs(r.Top - cursor.Y) < 700;
            Check(nearCursor, where + ": window is next to the cursor");
        }

        private static string Rect(RECT r) => r.Left + "," + r.Top + "-" + r.Right + "," + r.Bottom;

        // ------------------------------------------------------------------ helpers

        private static int _mark;

        private static bool Next()
        {
            _state++;
            _mark = _ticks;
            return false;
        }

        private static bool Timeout(int ticks, string message)
        {
            if (_ticks - _mark < ticks) return false;
            Fail(message);
            return true;
        }

        private static Palette.UI.PaletteWindow Window() => PaletteHost.CurrentWindow;
        private static PaletteViewModel Vm() => (PaletteViewModel)Window().DataContext;

        /// <summary>Raises a real routed PreviewKeyDown on the palette window, as the keyboard would.</summary>
        private static void PressKey(System.Windows.Input.Key key)
        {
            var w = Window();
            var source = PresentationSource.FromVisual(w);
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            w.RaiseEvent(args);
        }

        private static void CursorTo(double fx, double fy)
        {
            POINT cursor;
            GetCursorPos(out cursor);
            var monitor = MonitorFromPoint(cursor, 2);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            GetMonitorInfo(monitor, ref info);
            var w = info.rcWork;
            SetCursorPos(w.Left + (int)((w.Right - w.Left) * fx), w.Top + (int)((w.Bottom - w.Top) * fy));
        }

        private static void Say(string line) => Log.AppendLine(line);

        private static void Check(bool ok, string message)
        {
            if (ok) _passed++; else _failed++;
            Say((ok ? "PASS " : "FAIL ") + message);
        }

        private static void Fail(string message)
        {
            _failed++;
            Say("FAIL " + message);
        }

        private static void Finish(UIApplication app)
        {
            app.Idling -= OnIdling;
            Log.Insert(0, (_failed == 0 ? "RESULT: PASS" : "RESULT: FAIL") + " (" + _passed + " passed, " + _failed + " failed)" + Environment.NewLine);
            File.WriteAllText(Path.Combine(_out, "selftest-results.txt"), Log.ToString());
            if (Environment.GetEnvironmentVariable(ExitVariable) == "1")
            {
                try { app.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit)); }
                catch (Exception ex) { PaletteLog.Error("Self-test exit failed", ex); }
            }
        }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT p, int flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO i);
    }
}
