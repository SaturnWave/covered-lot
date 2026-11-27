using System;

namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// A count of panel units down: a y coordinate, counted from the top edge of the element it lies in, as UI Toolkit counts
    /// it, or a height. Never negative. A type of its own (CS-8), so that a height cannot be passed as a width, nor panel
    /// units as screen pixels.
    /// </summary>
    public readonly struct VerticalPanelUnits
    {
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        public VerticalPanelUnits(int value)
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
