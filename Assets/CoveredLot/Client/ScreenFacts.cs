using System;
using CoveredLot.Core.Layout;
using UnityEngine;

namespace CoveredLot.Client
{
    /// <summary>
    /// The screen as Unity reported it at one moment, in whole pixels: its size, and its safe area from its bottom-left
    /// corner. The safe-area padding (GameRoot) and the development line (ScreenHost) read it.
    /// </summary>
    internal readonly struct ScreenFacts
    {
        private ScreenFacts(PixelSize size, PixelRect safeArea)
        {
            Size = size;
            SafeArea = safeArea;
        }

        /// <summary>The screen's width and height in pixels, never negative.</summary>
        public PixelSize Size { get; }

        /// <summary>
        /// Screen.safeArea in whole pixels from the screen's bottom-left corner. Its left and bottom edges are rounded up
        /// and its right and top edges down, so a fraction of a pixel never moves an edge into a cutout.
        /// </summary>
        public PixelRect SafeArea { get; }

        /// <summary>The screen as Unity reports it now.</summary>
        public static ScreenFacts Now()
        {
            var size = new PixelSize(new HorizontalPixels(Math.Max(0, Screen.width)), new VerticalPixels(Math.Max(0, Screen.height)));
            Rect safe = Screen.safeArea;
            int left = Whole.Ceiling(safe.xMin);
            int bottom = Whole.Ceiling(safe.yMin);
            int width = Math.Max(0, Whole.Floor(safe.xMax) - left);
            int height = Math.Max(0, Whole.Floor(safe.yMax) - bottom);
            var safeArea = new PixelRect(new HorizontalPixels(left), new VerticalPixels(bottom), new PixelSize(new HorizontalPixels(width), new VerticalPixels(height)));
            return new ScreenFacts(size, safeArea);
        }

        /// <summary>Whether the other moment saw the same size and the same safe area, pixel for pixel.</summary>
        public bool Matches(ScreenFacts other) =>
            Size.Width.Value == other.Size.Width.Value && Size.Height.Value == other.Size.Height.Value
            && SafeArea.X.Value == other.SafeArea.X.Value && SafeArea.Y.Value == other.SafeArea.Y.Value
            && SafeArea.Size.Width.Value == other.SafeArea.Size.Width.Value && SafeArea.Size.Height.Value == other.SafeArea.Size.Height.Value;
    }
}
