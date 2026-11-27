using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// One cell of the lot by its row and its column, as the engine counts them (LotSize.RowOf and ColumnOf): row 0 at the
    /// top, column 0 at the left. A pillar stands on one, and an Ahead link runs from the centre of one to the centre of
    /// another. The picture works out the row and the column, so the screen never reads a cell index.
    /// </summary>
    public readonly struct GridCell
    {
        internal GridCell(LotCell cell, LotSize size)
        {
            Row = size.RowOf(cell);
            Column = size.ColumnOf(cell);
        }

        public Row Row { get; }

        public Column Column { get; }
    }
}
