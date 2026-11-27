using CoveredLot.Core.Layout;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The layout's units as the tests write them, one axis and one unit each, so that a test, as the game, cannot pass a
    /// width as a height or pixels as panel units: Across(344) and Down(420) in panel units, PixelsAcross(1080) and
    /// PixelsUp(2400) on the screen.
    /// </summary>
    internal static class Units
    {
        public static HorizontalPanelUnits Across(int units) => new(units);

        public static VerticalPanelUnits Down(int units) => new(units);

        public static HorizontalPixels PixelsAcross(int pixels) => new(pixels);

        public static VerticalPixels PixelsUp(int pixels) => new(pixels);
    }
}
