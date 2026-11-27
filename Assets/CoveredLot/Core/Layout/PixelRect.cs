namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// A rectangle of the screen in pixels, with its origin at the screen's bottom-left corner, as Screen.safeArea gives the
    /// safe area: how far its left edge lies from the screen's left edge, how far its bottom edge lies from the screen's
    /// bottom edge, and its size.
    /// </summary>
    public readonly struct PixelRect
    {
        public PixelRect(HorizontalPixels x, VerticalPixels y, PixelSize size)
        {
            X = x;
            Y = y;
            Size = size;
        }

        /// <summary>The distance of the rectangle's left edge from the screen's left edge.</summary>
        public HorizontalPixels X { get; }

        /// <summary>The distance of the rectangle's bottom edge from the screen's bottom edge.</summary>
        public VerticalPixels Y { get; }

        public PixelSize Size { get; }
    }
}
