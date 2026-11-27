using System;
using System.Collections.Generic;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// Where the board's cells lie in the frame the board is drawn in (design sections 6 and 9): square cells of a whole
    /// number of panel units, 2 units apart, as large as the frame allows for the lot's columns across and its rows down,
    /// and the board centred in the frame, the odd unit of a margin going to the right or the bottom. Row 0 is at the top,
    /// as in the engine, and UI Toolkit's y axis points down too, so nothing is flipped. It gives the rectangles of the
    /// board, of each cell and of each vehicle's box, and the centre of a cell, where an Ahead link's arrow ends, all in
    /// the frame's panel units from its top-left corner. In the phone's 344x420 frame an 8x8 lot gets 41-unit cells at a
    /// 43-unit pitch, below the 48 units of Android's touch guidance: a question for the designs (W1).
    /// </summary>
    public sealed class BoardGeometry
    {
        /// <summary>The space between two neighbouring cells, in panel units.</summary>
        private const int Gap = 2;

        private BoardGeometry(PanelSize frame, LotSize lot, int cellSide)
        {
            CellSide = cellSide;
            var board = new PanelSize(new HorizontalPanelUnits(SpanOf(lot.Width)), new VerticalPanelUnits(SpanOf(lot.Height)));
            Board = new PanelRect(
                new HorizontalPanelUnits((frame.Width.Value - board.Width.Value) / 2),
                new VerticalPanelUnits((frame.Height.Value - board.Height.Value) / 2),
                board);
            Cells = CellsOf(lot);
        }

        /// <summary>The side of every cell, in panel units: 1 or more.</summary>
        public int CellSide { get; }

        /// <summary>The board's rectangle in the frame: its cells and the gaps between them.</summary>
        public PanelRect Board { get; }

        /// <summary>The rectangle of every cell, row by row from the top-left, in the order of the engine's cell indexes.</summary>
        public ReadOnlyArray<PanelRect> Cells { get; }

        /// <summary>The cells' side plus the gap: how far each cell lies from its neighbour.</summary>
        private int Pitch => CellSide + Gap;

        /// <summary>
        /// The board of the lot in the frame, with the largest square cells that fit it. None when the frame is too small
        /// for cells of even one unit, as a frame can be before its first layout.
        /// </summary>
        public static Option<BoardGeometry> Fit(PanelSize frame, LotSize lot)
        {
            int sideAcross = (frame.Width.Value - ((lot.Width - 1) * Gap)) / lot.Width;
            int sideDown = (frame.Height.Value - ((lot.Height - 1) * Gap)) / lot.Height;
            int side = Math.Min(sideAcross, sideDown);
            return side >= 1 ? Option.Some(new BoardGeometry(frame, lot, side)) : Option.None<BoardGeometry>();
        }

        /// <summary>
        /// The rectangle of a vehicle's box, a tile's or a ghost's, in the lot the board was fitted to: from the box's
        /// top-left cell to its bottom-right one, the gaps between its cells included.
        /// </summary>
        public PanelRect RectOf(GridBox box) =>
            new PanelRect(XOf(box.Column), YOf(box.Row), new PanelSize(new HorizontalPanelUnits(SpanOf(box.Columns)), new VerticalPanelUnits(SpanOf(box.Rows))));

        /// <summary>The rectangle of one cell, such as a pillar's.</summary>
        public PanelRect RectOf(GridCell cell) =>
            new PanelRect(XOf(cell.Column), YOf(cell.Row), new PanelSize(new HorizontalPanelUnits(CellSide), new VerticalPanelUnits(CellSide)));

        /// <summary>
        /// The centre of one cell, where an Ahead link's arrow starts or ends, in whole units: when the cells' side is odd,
        /// half a unit left of and above the true centre.
        /// </summary>
        public PanelPoint CentreOf(GridCell cell) =>
            new PanelPoint(new HorizontalPanelUnits(XOf(cell.Column).Value + (CellSide / 2)), new VerticalPanelUnits(YOf(cell.Row).Value + (CellSide / 2)));

        private ReadOnlyArray<PanelRect> CellsOf(LotSize lot)
        {
            var cells = new List<PanelRect>();
            for (int index = 0; index < lot.CellCount; index++)
            {
                cells.Add(RectOf(new GridCell(new LotCell((byte)index), lot)));
            }

            return ReadOnlyArray.CopyOf(cells);
        }

        private HorizontalPanelUnits XOf(Column column) => new HorizontalPanelUnits(Board.X.Value + (column.Value * Pitch));

        private VerticalPanelUnits YOf(Row row) => new VerticalPanelUnits(Board.Y.Value + (row.Value * Pitch));

        /// <summary>The length of this many cells side by side, the gaps between them included.</summary>
        private int SpanOf(int cells) => (cells * Pitch) - Gap;
    }
}
