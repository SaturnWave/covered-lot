using System;

namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// A count of the screen's pixels across: an x coordinate, counted from the screen's left edge, or a width. Never
    /// negative. A type of its own (CS-8), so that a width cannot be passed as a height, nor screen pixels as panel units.
    /// </summary>
    public readonly struct HorizontalPixels
    {
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        public HorizontalPixels(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A count of pixels is never negative.");
            }

            Value = value;
        }

        public int Value { get; }
    }
}
