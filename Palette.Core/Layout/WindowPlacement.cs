using System;

namespace Palette.Core.Layout
{
    /// <summary>A rectangle in physical (device) pixels of the virtual screen.</summary>
    public struct PixelRect
    {
        public PixelRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public struct PlacementResult
    {
        public PlacementResult(int x, int y, bool aboveCursor, bool leftOfCursor)
        {
            X = x;
            Y = y;
            AboveCursor = aboveCursor;
            LeftOfCursor = leftOfCursor;
        }

        /// <summary>Top-left of the window in physical pixels.</summary>
        public int X { get; }
        public int Y { get; }

        /// <summary>The window did not fit below the cursor and was placed above it.</summary>
        public bool AboveCursor { get; }

        /// <summary>The window did not fit right of the cursor and was placed to its left.</summary>
        public bool LeftOfCursor { get; }
    }

    /// <summary>
    /// Where to put the Palette window: slightly below and right of the cursor, flipped to the other side when that would
    /// leave the monitor's work area, and finally clamped so the whole window is always visible. Pure maths in physical
    /// pixels; the caller supplies the cursor, the work area of the monitor under the cursor and the window size.
    /// </summary>
    public static class WindowPlacement
    {
        public static PlacementResult Compute(int cursorX, int cursorY, PixelRect workArea, int width, int height, int offset)
        {
            var x = cursorX + offset;
            var left = false;
            if (x + width > workArea.Right)
            {
                x = cursorX - offset - width;
                left = true;
            }

            var y = cursorY + offset;
            var above = false;
            if (y + height > workArea.Bottom)
            {
                y = cursorY - offset - height;
                above = true;
            }

            // Whatever is left over (cursor near a corner, window larger than the space): clamp inside the work area.
            x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - width));
            y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - height));
            return new PlacementResult(x, y, above, left);
        }
    }
}
