using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Palette.Core;
using Palette.Core.Layout;

namespace Palette.Infrastructure
{
    /// <summary>Result of placing the window; lets the caller keep a flipped (above-cursor) window anchored while it resizes.</summary>
    internal struct PlacementInfo
    {
        public int X, Y;

        /// <summary>Physical pixel row of the window's bottom edge when it was put above the cursor, otherwise -1.</summary>
        public int BottomAnchorPx;

        /// <summary>DPI scale (1.0 = 96 dpi) of the window on the monitor it was placed on.</summary>
        public double Scale;
    }

    /// <summary>
    /// Puts a WPF window next to the mouse cursor, fully inside the work area of the monitor that contains the cursor.
    ///
    /// Everything is done in physical pixels through Win32 (GetCursorPos / MonitorFromPoint / SetWindowPos), which stays
    /// consistent in every DPI awareness mode Revit may run in (system-aware or per-monitor): all of these calls report
    /// coordinates in the same space for the calling thread. The window is first moved onto the target monitor so
    /// GetDpiForWindow returns that monitor's scale before the final position is computed.
    /// </summary>
    internal static class WindowPositioner
    {
        private const int OffsetDips = 14;

        public static PlacementInfo PlaceNearCursor(Window window, double widthDips, double heightDips)
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();

            POINT cursor;
            if (!NativeMethods.GetCursorPos(out cursor)) return new PlacementInfo { BottomAnchorPx = -1, Scale = 1.0 };

            var monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
                return new PlacementInfo { BottomAnchorPx = -1, Scale = 1.0 };
            var work = new PixelRect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom);

            // Land on the right monitor first so the window picks up that monitor's DPI.
            Move(hwnd, work.Left, work.Top);
            var scale = DpiScale(hwnd);

            var width = (int)Math.Ceiling(widthDips * scale);
            var height = (int)Math.Ceiling(heightDips * scale);
            var offset = (int)Math.Round(OffsetDips * scale);
            var result = WindowPlacement.Compute(cursor.X, cursor.Y, work, width, height, offset);
            Move(hwnd, result.X, result.Y);

            return new PlacementInfo { X = result.X, Y = result.Y, BottomAnchorPx = result.AboveCursor ? result.Y + height : -1, Scale = scale };
        }

        /// <summary>Re-applies the position (WPF may reposition on the first Show) and re-anchors a flipped window.</summary>
        public static void Reapply(Window window, PlacementInfo info)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero) Move(hwnd, info.X, info.Y);
        }

        /// <summary>Moves the top edge so a window placed above the cursor keeps its bottom edge when its height changes.</summary>
        public static void KeepBottomAnchored(Window window, PlacementInfo info)
        {
            if (info.BottomAnchorPx < 0) return;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            RECT rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect)) return;
            var top = info.BottomAnchorPx - (rect.Bottom - rect.Top);
            if (top != rect.Top) Move(hwnd, rect.Left, top);
        }


        private static void Move(IntPtr hwnd, int x, int y) =>
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        private static double DpiScale(IntPtr hwnd)
        {
            try
            {
                var dpi = NativeMethods.GetDpiForWindow(hwnd);
                if (dpi > 0) return dpi / 96.0;
            }
            catch (EntryPointNotFoundException)
            {
                // Windows older than 10 1607: fall through to the system DPI
            }
            catch (Exception ex)
            {
                PaletteLog.Warn("GetDpiForWindow failed: " + ex.Message);
            }
            return 1.0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    internal static class NativeMethods
    {
        public const int MONITOR_DEFAULTTONEAREST = 2;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT point, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);
    }
}
