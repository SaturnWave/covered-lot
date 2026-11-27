using PuzzleCore;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// One pill of the ledger (design sections 8.3 and 9): a colour of the level's vehicles, by its swatch, which paints the
    /// pill's border, and its text, the colour's letter and how many vehicles of it are still covered in the lot: "R 1", or
    /// "Y 0" once none is (BRIEF:15).
    /// </summary>
    public sealed class LedgerPill
    {
        internal LedgerPill(ColourId colour, int covered)
        {
            Swatch = Palette.SwatchOf(colour);
            Text = Copy.PillText(colour, covered);
        }

        public Swatch Swatch { get; }

        /// <summary>"R 1".</summary>
        public string Text { get; }
    }
}
