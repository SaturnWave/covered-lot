using CoveredLot.Core.Layout;
using Xunit;
using static CoveredLot.Core.Tests.Support.LayoutDescriptions;
using static CoveredLot.Core.Tests.Support.Units;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The safe area's padding in panel units (design section 6), on a 1080x2400 screen and the 360x800 panel it scales to,
    /// so that 3 pixels make a unit on each axis. A safe area 63 pixels up from the bottom and 2247 high leaves 90 pixels
    /// above it, so it pads 30 units at the top and 21 at the bottom; a full safe area pads nothing. Pixels that make part
    /// of a unit take the whole unit, on each of the four sides; a safe area that a rounding takes a pixel past the
    /// screen's edge pads nothing on that side; and a screen with no pixels pads nothing at all.
    /// </summary>
    public sealed class SafeInsetsTests
    {
        private static readonly PixelSize Screen1080x2400 = new(PixelsAcross(1080), PixelsUp(2400));

        private static readonly PanelSize Panel360x800 = new(Across(360), Down(800));

        [Fact]
        public void A_safe_area_63_pixels_up_and_2247_high_pads_the_panel_30_units_at_the_top_and_21_at_the_bottom()
        {
            var safeArea = new PixelRect(PixelsAcross(0), PixelsUp(63), new PixelSize(PixelsAcross(1080), PixelsUp(2247)));

            SafeInsets insets = SafeInsets.ToPanel(Screen1080x2400, safeArea, Panel360x800);

            Assert.Equal("top 30, left 0, bottom 21, right 0", Insets(insets));
        }

        [Fact]
        public void A_full_safe_area_pads_nothing()
        {
            var safeArea = new PixelRect(PixelsAcross(0), PixelsUp(0), Screen1080x2400);

            SafeInsets insets = SafeInsets.ToPanel(Screen1080x2400, safeArea, Panel360x800);

            Assert.Equal("top 0, left 0, bottom 0, right 0", Insets(insets));
        }

        /// <summary>100 pixels at the left, 80 at the right, 64 at the bottom and 91 at the top: 33.3, 26.7, 21.3 and 30.3 units.</summary>
        [Fact]
        public void Pixels_that_make_part_of_a_unit_take_the_whole_unit_on_each_side()
        {
            var safeArea = new PixelRect(PixelsAcross(100), PixelsUp(64), new PixelSize(PixelsAcross(900), PixelsUp(2245)));

            SafeInsets insets = SafeInsets.ToPanel(Screen1080x2400, safeArea, Panel360x800);

            Assert.Equal("top 31, left 34, bottom 22, right 27", Insets(insets));
        }

        [Fact]
        public void A_safe_area_a_pixel_past_the_screen_s_right_and_top_edges_pads_nothing_on_those_sides()
        {
            var safeArea = new PixelRect(PixelsAcross(0), PixelsUp(63), new PixelSize(PixelsAcross(1081), PixelsUp(2338)));

            SafeInsets insets = SafeInsets.ToPanel(Screen1080x2400, safeArea, Panel360x800);

            Assert.Equal("top 0, left 0, bottom 21, right 0", Insets(insets));
        }

        [Fact]
        public void A_screen_with_no_pixels_pads_nothing()
        {
            var noScreen = new PixelSize(PixelsAcross(0), PixelsUp(0));
            var safeArea = new PixelRect(PixelsAcross(0), PixelsUp(0), noScreen);

            SafeInsets insets = SafeInsets.ToPanel(noScreen, safeArea, Panel360x800);

            Assert.Equal("top 0, left 0, bottom 0, right 0", Insets(insets));
        }
    }
}
