using System;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The number of a vehicle that carries tags (design section 9). A level's tagged vehicles are numbered from 1 in PieceId
    /// order, and the number shows as a badge on the vehicle's tile, on its ghost and on its row of the tags list, so each
    /// row names the vehicle it is about. Only the pictures make numbers. default(ClueNumber), whose value is 0, is no number.
    /// </summary>
    public readonly struct ClueNumber
    {
        internal ClueNumber(int value)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A clue number is 1 or more.");
            }

            Value = value;
        }

        public int Value { get; }

        /// <summary>The number as the badge shows it, written with the invariant culture: "1".</summary>
        public string Text => Copy.BadgeText(this);
    }
}
