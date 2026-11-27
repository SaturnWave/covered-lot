using System.Globalization;
using CoveredLot.Core.Layout;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The layout's values written out as plain lines in the tests' own words: a frame as its width and height ("344x420"),
    /// a rectangle as its left, its top, its width and its height ("(2,40,340x340)"), a point as its two coordinates
    /// ("(200,295)") and the safe insets side by side ("top 30, left 0, bottom 21, right 0"). A test compares them, so a
    /// failure shows both values.
    /// </summary>
    internal static class LayoutDescriptions
    {
        /// <summary>"344x420".</summary>
        public static string Size(PanelSize size) => Number(size.Width.Value) + "x" + Number(size.Height.Value);

        /// <summary>"(left,top,widthxheight)": "(2,40,340x340)".</summary>
        public static string Rect(PanelRect rect) =>
            "(" + Number(rect.X.Value) + "," + Number(rect.Y.Value) + "," + Size(rect.Size) + ")";

        /// <summary>"(x,y)": "(200,295)".</summary>
        public static string Point(PanelPoint point) => "(" + Number(point.X.Value) + "," + Number(point.Y.Value) + ")";

        /// <summary>"top 30, left 0, bottom 21, right 0".</summary>
        public static string Insets(SafeInsets insets) =>
            "top " + Number(insets.Top.Value) + ", left " + Number(insets.Left.Value) + ", bottom " + Number(insets.Bottom.Value)
            + ", right " + Number(insets.Right.Value);

        public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
