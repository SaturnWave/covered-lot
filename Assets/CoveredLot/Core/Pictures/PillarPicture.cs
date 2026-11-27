using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// A pillar's cell, as the board shows it (design section 9): where it stands, its glyph "#", and its mark. The engine's
    /// loader refuses a level with a pillar on a vehicle's path, so no tap can bump into a pillar; the mark follows the
    /// last tap all the same, as a tile's does.
    /// </summary>
    public sealed class PillarPicture
    {
        internal PillarPicture(LotCell cell, LotSize size, TileMark mark)
        {
            Cell = new GridCell(cell, size);
            Label = Copy.PillarGlyph;
            Mark = mark;
        }

        public GridCell Cell { get; }

        /// <summary>The pillar's glyph, "#".</summary>
        public string Label { get; }

        public TileMark Mark { get; }
    }
}
