using System;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// The number a listed level shows the player, as in "Level 12": its position in the presentation file, counted from 1,
    /// so the numbers have no gaps where the pack's slots do (design section 5). It is not the slot. default(LevelNumber),
    /// whose value is 0, is no level number.
    /// </summary>
    public readonly struct LevelNumber
    {
        public LevelNumber(int value)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A level number is 1 or more.");
            }

            Value = value;
        }

        public int Value { get; }
    }
}
