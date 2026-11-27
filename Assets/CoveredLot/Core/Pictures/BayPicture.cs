using PuzzleCore;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// One bay, as the bays row shows it (design section 9): empty, or filled with the colour of the vehicle that waits in
    /// it, by its swatch and its letter. Closed; only the pictures make bays.
    /// </summary>
    public abstract class BayPicture
    {
        private BayPicture()
        {
        }

        /// <summary>No vehicle waits in the bay: a light outline.</summary>
        public sealed class Empty : BayPicture
        {
            internal Empty()
            {
            }
        }

        /// <summary>A vehicle waits in the bay: its colour shows as its swatch, with its letter.</summary>
        public sealed class Filled : BayPicture
        {
            internal Filled(ColourId colour)
            {
                Swatch = Palette.SwatchOf(colour);
                Letter = Palette.LetterOf(colour);
            }

            public Swatch Swatch { get; }

            /// <summary>The colour's letter: "B" for blue.</summary>
            public string Letter { get; }
        }

        /// <summary>The bay that holds this colour, as the state gives it (LotState.ColourIn): filled with it, or empty when it holds none.</summary>
        internal static BayPicture Of(Option<ColourId> colour)
        {
            if (colour.TryGet(out ColourId waiting))
            {
                return new Filled(waiting);
            }

            return new Empty();
        }
    }
}
