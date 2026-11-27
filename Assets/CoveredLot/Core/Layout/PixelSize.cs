namespace CoveredLot.Core.Layout
{
    /// <summary>A size in the screen's pixels, as Screen.width and Screen.height give the screen's: a width and a height.</summary>
    public readonly struct PixelSize
    {
        public PixelSize(HorizontalPixels width, VerticalPixels height)
        {
            Width = width;
            Height = height;
        }

        public HorizontalPixels Width { get; }

        public VerticalPixels Height { get; }
    }
}
