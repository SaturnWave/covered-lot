using System;

namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// A count of panel units across: an x coordinate, counted from the left edge of the element it lies in, or a width. The
    /// panel is 360 units wide, so on a 360dp phone a unit is about one dp (design section 6). Never negative. A type of its
    /// own (CS-8), so that a width cannot be passed as a height, nor panel units as screen pixels.
    /// </summary>
    public readonly struct HorizontalPanelUnits
    {
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        public HorizontalPanelUnits(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A count of panel units is never negative.");
            }

            Value = value;
        }

        public int Value { get; }
    }
}
