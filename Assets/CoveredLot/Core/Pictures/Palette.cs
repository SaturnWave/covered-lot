using System;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The level colours the screen can show (design section 8.1): colour ids 1 to 6, each with its swatch and its letter,
    /// the letter the engine's level texts write for it (LotText.ColourLetters, R B Y G P O). A colour id can be up to 15,
    /// but a level text has letters for 1 to 6 only, so every level the catalogue loads uses those six; any other id throws.
    /// </summary>
    public static class Palette
    {
        /// <summary>The colours the palette has: ids 1 to this.</summary>
        private const int ColourCount = 6;

        /// <summary>The swatch of a level colour.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The colour id is not 1 to 6.</exception>
        public static Swatch SwatchOf(ColourId colour) => colour.Value switch
        {
            1 => Swatch.Red,
            2 => Swatch.Blue,
            3 => Swatch.Yellow,
            4 => Swatch.Green,
            5 => Swatch.Pink,
            6 => Swatch.Orange,
            _ => throw new ArgumentOutOfRangeException(nameof(colour), "The palette has swatches for the colour ids 1 to 6 only."),
        };

        /// <summary>The letter of a level colour, as the level texts and the screen write it: "R" for colour id 1.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The colour id is not 1 to 6.</exception>
        public static string LetterOf(ColourId colour)
        {
            if (colour.Value > ColourCount)
            {
                throw new ArgumentOutOfRangeException(nameof(colour), "The palette has letters for the colour ids 1 to 6 only.");
            }

            return LotText.ColourLetters.Substring(colour.Value - 1, 1);
        }
    }
}
