using Palette.Core.Layout;
using Xunit;

namespace Palette.Core.Tests
{
    public class PlacementTests
    {
        private static readonly PixelRect Screen = new PixelRect(0, 0, 1920, 1080);

        [Fact]
        public void PlentyOfSpace_OffsetBelowRightOfCursor()
        {
            var p = WindowPlacement.Compute(500, 400, Screen, 560, 420, 16);
            Assert.Equal(516, p.X);
            Assert.Equal(416, p.Y);
            Assert.False(p.AboveCursor);
            Assert.False(p.LeftOfCursor);
        }

        [Fact]
        public void NearBottom_FlipsAboveCursor()
        {
            var p = WindowPlacement.Compute(500, 1000, Screen, 560, 420, 16);
            Assert.True(p.AboveCursor);
            Assert.Equal(1000 - 16 - 420, p.Y);
        }

        [Fact]
        public void NearRight_FlipsLeftOfCursor()
        {
            var p = WindowPlacement.Compute(1800, 300, Screen, 560, 420, 16);
            Assert.True(p.LeftOfCursor);
            Assert.Equal(1800 - 16 - 560, p.X);
        }

        [Fact]
        public void BottomRightCorner_StaysFullyVisible()
        {
            var p = WindowPlacement.Compute(1919, 1079, Screen, 560, 420, 16);
            Assert.InRange(p.X, 0, 1920 - 560);
            Assert.InRange(p.Y, 0, 1080 - 420);
        }

        [Fact]
        public void TopLeftCorner_StaysFullyVisible()
        {
            var p = WindowPlacement.Compute(0, 0, Screen, 560, 420, 16);
            Assert.Equal(16, p.X);
            Assert.Equal(16, p.Y);
        }

        [Fact]
        public void SecondaryMonitorLeftOfPrimary_NegativeCoordinates()
        {
            var left = new PixelRect(-1920, 0, 0, 1080);
            var p = WindowPlacement.Compute(-30, 500, left, 560, 420, 16);
            Assert.InRange(p.X, -1920, -560);
            Assert.InRange(p.Y, 0, 1080 - 420);
        }

        [Fact]
        public void SecondaryMonitorWithOffsetWorkArea_RespectsTaskbarInset()
        {
            var work = new PixelRect(1920, 0, 3840, 1040); // taskbar at the bottom
            var p = WindowPlacement.Compute(3000, 1030, work, 560, 420, 16);
            Assert.InRange(p.Y + 420, 0, 1040);
            Assert.InRange(p.X, 1920, 3840 - 560);
        }

        [Fact]
        public void WindowLargerThanWorkArea_PinsToTopLeft()
        {
            var tiny = new PixelRect(0, 0, 400, 300);
            var p = WindowPlacement.Compute(200, 150, tiny, 560, 420, 16);
            Assert.Equal(0, p.X);
            Assert.Equal(0, p.Y);
        }
    }
}
