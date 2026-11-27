using PuzzleCore;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// What a tile shows of its vehicle's colour (design section 9): the colour, or a cover. Closed; only the pictures make
    /// faces.
    /// </summary>
    public abstract class TileFace
    {
        private TileFace()
        {
        }

        /// <summary>The vehicle is shown open: its colour shows as its swatch, and its letter starts the tile's label.</summary>
        public sealed class Open : TileFace
        {
            internal Open(ColourId colour)
            {
                Swatch = Palette.SwatchOf(colour);
                Letter = Palette.LetterOf(colour);
            }

            public Swatch Swatch { get; }

            /// <summary>The colour's letter: "B" for blue.</summary>
            public string Letter { get; }
        }

        /// <summary>
        /// The vehicle is shown covered: grey, with "?" in place of its letter. The face holds no colour at all, so none can
        /// reach the screen.
        /// </summary>
        public sealed class Covered : TileFace
        {
            internal Covered()
            {
            }
        }
    }
}
