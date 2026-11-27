using System;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The box a vehicle covers on the lot (design section 8.3): the bounding box of the cells the engine gives it
    /// (LotLevel.FootprintOf), never one worked out again from its anchor and its length. It is the row and the column of
    /// its top-left cell, and its height and width in cells: a car facing north or south is 2 x 1, one facing east or
    /// west 1 x 2.
    /// </summary>
    public readonly struct GridBox
    {
        /// <summary>The bounding box of the cells of the mask that lie in the lot.</summary>
        /// <exception cref="ArgumentOutOfRangeException">No cell of the mask lies in the lot.</exception>
        internal GridBox(ulong cells, LotSize size)
        {
            int top = int.MaxValue;
            int left = int.MaxValue;
            int bottom = -1;
            int right = -1;
            for (int index = 0; index < size.CellCount; index++)
            {
                var cell = new LotCell((byte)index);
                if ((cells & cell.Bit) != 0)
                {
                    int row = size.RowOf(cell).Value;
                    int column = size.ColumnOf(cell).Value;
                    top = Math.Min(top, row);
                    bottom = Math.Max(bottom, row);
                    left = Math.Min(left, column);
                    right = Math.Max(right, column);
                }
            }

            if (bottom < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cells), "A box holds at least one cell of the lot.");
            }

            Row = new Row((byte)top);
            Column = new Column((byte)left);
            Rows = bottom - top + 1;
            Columns = right - left + 1;
        }

        /// <summary>The row of the box's top cells.</summary>
        public Row Row { get; }

        /// <summary>The column of the box's leftmost cells.</summary>
        public Column Column { get; }

        /// <summary>The box's height, in cells.</summary>
        public int Rows { get; }

        /// <summary>The box's width, in cells.</summary>
        public int Columns { get; }
    }
}
