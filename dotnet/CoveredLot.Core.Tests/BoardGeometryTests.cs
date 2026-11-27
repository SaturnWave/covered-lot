using System.Collections.Generic;
using System.Linq;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using Xunit;
using Xunit.Sdk;
using static CoveredLot.Core.Tests.Support.LayoutDescriptions;
using static CoveredLot.Core.Tests.Support.Units;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The board's geometry (design sections 6 and 9), for every lot size the engine allows, 5x5 to 8x8 with width and height
    /// apart, in three frames: a phone's, 344x420 (the 360-unit panel less 8-unit margins), a short one, 344x300, and a wide
    /// one, 760x600. In each, the cells are square, 2 units apart and the largest that fit; the board is the box of its
    /// cells, lies inside the frame and is centred in it; and the rectangle of each tile spans exactly the cells the
    /// engine gives its vehicle. A pitch table pins the phone frame's cells of each square lot, 41-unit cells at a 43-unit
    /// pitch for 8 columns; the Example's board in it is pinned unit by unit, its pillar and the variant board's link
    /// ends too; and a frame too small for cells of one unit gives no geometry, a non-square lot showing that the width
    /// is read across and the height down.
    /// </summary>
    public sealed class BoardGeometryTests
    {
        private const int Gap = 2;

        private static readonly PanelSize PhoneFrame = new(Across(344), Down(420));

        private static readonly IReadOnlyList<PanelSize> Frames = new[] { PhoneFrame, new PanelSize(Across(344), Down(300)), new PanelSize(Across(760), Down(600)) };

        [Fact]
        public void In_every_frame_every_lot_size_gets_square_cells_two_units_apart_on_a_board_centred_inside_the_frame()
        {
            var broken = new List<string>();
            int fitted = 0;
            foreach (PanelSize frame in Frames)
            {
                foreach (LotSize lot in LotsOfEverySize.Sizes())
                {
                    BoardGeometry geometry = FitOrFail(frame, lot);
                    broken.AddRange(CellsOutOfGrid(geometry, lot).Select(problem => Where(lot, frame) + problem));
                    broken.AddRange(BoardOutOfPlace(geometry, frame).Select(problem => Where(lot, frame) + problem));
                    fitted++;
                }
            }

            Assert.Equal(Frames.Count * 16, fitted);
            Assert.True(broken.Count == 0, string.Join("\n", broken));
        }

        [Fact]
        public void In_every_frame_every_lot_size_gets_the_largest_cells_that_fit()
        {
            var broken = new List<string>();
            int fitted = 0;
            foreach (PanelSize frame in Frames)
            {
                foreach (LotSize lot in LotsOfEverySize.Sizes())
                {
                    int side = FitOrFail(frame, lot).CellSide;
                    bool oneMoreFitsAcross = (lot.Width * (side + 1)) + ((lot.Width - 1) * Gap) <= frame.Width.Value;
                    bool oneMoreFitsDown = (lot.Height * (side + 1)) + ((lot.Height - 1) * Gap) <= frame.Height.Value;
                    if (side < 1 || (oneMoreFitsAcross && oneMoreFitsDown))
                    {
                        broken.Add(Where(lot, frame) + "cells of " + Number(side) + " units, where " + Number(side + 1) + " fit");
                    }

                    fitted++;
                }
            }

            Assert.Equal(Frames.Count * 16, fitted);
            Assert.True(broken.Count == 0, string.Join("\n", broken));
        }

        /// <summary>
        /// The tiles of a plain level of each size, whose boxes stand in each corner and run both ways (LotsOfEverySize):
        /// each tile's rectangle is the box of the rectangles of the cells of its vehicle's footprint, as the engine gives
        /// it (LotLevel.FootprintOf).
        /// </summary>
        [Fact]
        public void In_every_frame_the_rect_of_each_tile_spans_exactly_the_cells_of_its_vehicle_for_every_lot_size()
        {
            var broken = new List<string>();
            int compared = 0;
            foreach (LotSize lot in LotsOfEverySize.Sizes())
            {
                CatalogueEntry entry = LotsOfEverySize.Entry(lot);
                BoardPicture board = BoardPicture.Of(new GameSession(entry));
                string boxes = string.Join(" ", board.Tiles.Select(tile => PictureDescriptions.Box(tile.Box)));
                string expectedBoxes = string.Join(" ", LotsOfEverySize.BoxesOf(lot));
                if (boxes != expectedBoxes)
                {
                    broken.Add(lot.ToString() + ": the tiles' boxes are " + boxes + ", not " + expectedBoxes);
                }

                foreach (PanelSize frame in Frames)
                {
                    BoardGeometry geometry = FitOrFail(frame, lot);
                    foreach (TilePicture tile in board.Tiles)
                    {
                        string expected = BoxOfCells(geometry, entry.Level.FootprintOf(tile.Piece));
                        string actual = Rect(geometry.RectOf(tile.Box));
                        if (actual != expected)
                        {
                            broken.Add(Where(lot, frame) + "tile " + Number(tile.Piece.Value) + " is " + actual + ", its cells " + expected);
                        }

                        compared++;
                    }
                }
            }

            Assert.Equal(16 * Frames.Count * 6, compared);
            Assert.True(broken.Count == 0, string.Join("\n", broken));
        }

        /// <summary>The pitch is measured between the first two cells of the top row, so it is the cells' side plus the gap as drawn.</summary>
        [Fact]
        public void In_the_phone_frame_the_square_lots_get_67_55_47_and_41_unit_cells_at_a_pitch_of_69_57_49_and_43()
        {
            var table = new List<string>();
            for (int lotSide = LotSize.MinSide; lotSide <= LotSize.MaxSide; lotSide++)
            {
                LotSize lot = LotsOfEverySize.Sizes().Single(size => size.Width == lotSide && size.Height == lotSide);
                BoardGeometry geometry = FitOrFail(PhoneFrame, lot);
                int pitch = geometry.Cells[1].X.Value - geometry.Cells[0].X.Value;
                table.Add(lot.ToString() + ": cell " + Number(geometry.CellSide) + ", pitch " + Number(pitch));
            }

            Assert.Equal(new[] { "5x5: cell 67, pitch 69", "6x6: cell 55, pitch 57", "7x7: cell 47, pitch 49", "8x8: cell 41, pitch 43" }, table);
        }

        /// <summary>
        /// The Example, 6x6, in the phone frame: 55-unit cells at a 57-unit pitch, so a 340-unit board 2 units from the left
        /// and 40 from the top. Its tiles 0 (0,1,2x1), 1 (1,2,1x2) and 3 (2,2,2x1), its pillar at (0,4), and the variant
        /// board's link from cell 27 (4,3) to cell 24 (4,0), whose ends are the cells' centres, 27 units into each.
        /// </summary>
        [Fact]
        public void In_the_phone_frame_the_Example_s_board_tiles_pillar_and_link_ends_lie_unit_by_unit_where_the_pitch_puts_them()
        {
            var session = new GameSession(ShippedCatalogue.Example());
            BoardPicture board = BoardPicture.Of(session);
            BoardPicture variant = BoardPicture.Of(new GameSession(SixBySixVariants.Entry(SixBySixVariants.Text(
                new CoversLine("covers=3,7"), new CluesLine("clues=Not(3,R);Not(3,Y);Ahead(7,Same);Touch(7,Y,2)")))));
            BoardGeometry geometry = FitOrFail(PhoneFrame, board.Size);

            var tiles = new List<string>();
            foreach (TilePicture tile in board.Tiles)
            {
                if (tile.Piece.Value is 0 or 1 or 3)
                {
                    tiles.Add(PictureDescriptions.Tile(tile) + " " + Rect(geometry.RectOf(tile.Box)));
                }
            }

            AheadLinkPicture link = Assert.Single(variant.AheadLinks);
            Assert.Equal("cell 27 to cell 24", PictureDescriptions.Link(link, variant.Size));
            Assert.Equal(55, geometry.CellSide);
            Assert.Equal("(2,40,340x340)", Rect(geometry.Board));
            Assert.Equal(new[] { "0 (0,1,2x1) B^ (59,40,55x112)", "1 (1,2,1x2) R> (116,97,112x55)", "3 (2,2,2x1) ?v (116,154,55x112)" }, tiles);
            Assert.Equal("(0,4) # (230,40,55x55)", PictureDescriptions.Pillar(Assert.Single(board.Pillars)) + " " + Rect(geometry.RectOf(board.Pillars[0].Cell)));
            Assert.Equal(new[] { "(200,295)", "(29,295)" }, new[] { Point(geometry.CentreOf(link.From)), Point(geometry.CentreOf(link.To)) });
        }

        /// <summary>
        /// A lot 5 wide and 8 high needs 13 units across for cells of one unit (5 cells and 4 gaps) and 22 down (8 cells and 7
        /// gaps): a frame of 13x22 fits it, and one a unit narrower or a unit shorter does not. The 8x8 lot needs 22 each way.
        /// </summary>
        [Fact]
        public void A_frame_too_small_for_cells_of_one_unit_gives_no_geometry_and_the_smallest_that_fits_gives_one_unit_cells()
        {
            LotSize narrowAndTall = LotsOfEverySize.Sizes().Single(size => size.Width == 5 && size.Height == 8);
            LotSize largest = LotsOfEverySize.Sizes().Single(size => size.Width == 8 && size.Height == 8);

            Option<BoardGeometry> fits = BoardGeometry.Fit(new PanelSize(Across(13), Down(22)), narrowAndTall);
            Option<BoardGeometry> fitsLargest = BoardGeometry.Fit(new PanelSize(Across(22), Down(22)), largest);

            Assert.True(fits.TryGet(out BoardGeometry? geometry), "A 13x22 frame does not fit a 5x8 lot.");
            Assert.Equal((1, "(0,0,13x22)"), (geometry.CellSide, Rect(geometry.Board)));
            Assert.True(fitsLargest.TryGet(out BoardGeometry? largestGeometry), "A 22x22 frame does not fit an 8x8 lot.");
            Assert.Equal((1, "(0,0,22x22)"), (largestGeometry.CellSide, Rect(largestGeometry.Board)));
            Assert.False(BoardGeometry.Fit(new PanelSize(Across(12), Down(22)), narrowAndTall).HasValue, "A 12x22 frame fits a 5x8 lot.");
            Assert.False(BoardGeometry.Fit(new PanelSize(Across(13), Down(21)), narrowAndTall).HasValue, "A 13x21 frame fits a 5x8 lot.");
            Assert.False(BoardGeometry.Fit(new PanelSize(Across(0), Down(0)), largest).HasValue, "A frame of no units fits an 8x8 lot.");
        }

        /// <summary>
        /// The grid's rule, from the first cell: one rectangle per cell, in the engine's cell order, each a square of the
        /// geometry's side, the cell at row r and column c lying r and c pitches (the side and the gap) from the first.
        /// </summary>
        private static IEnumerable<string> CellsOutOfGrid(BoardGeometry geometry, LotSize lot)
        {
            if (geometry.Cells.Count != lot.CellCount)
            {
                yield return Number(geometry.Cells.Count) + " cell rectangles for " + Number(lot.CellCount) + " cells";
                yield break;
            }

            PanelRect first = geometry.Cells[0];
            int pitch = geometry.CellSide + Gap;
            for (int index = 0; index < lot.CellCount; index++)
            {
                var cell = new LotCell((byte)index);
                int row = lot.RowOf(cell).Value;
                int column = lot.ColumnOf(cell).Value;
                string expected = "(" + Number(first.X.Value + (column * pitch)) + "," + Number(first.Y.Value + (row * pitch)) + ","
                    + Number(geometry.CellSide) + "x" + Number(geometry.CellSide) + ")";
                string actual = Rect(geometry.Cells[index]);
                if (actual != expected)
                {
                    yield return "cell " + Number(index) + " is " + actual + ", not " + expected;
                }
            }
        }

        /// <summary>
        /// The board's rule: it is the box of its cells, from the first cell to the far corner of the last; it lies inside
        /// the frame; and it is centred, its margins on each axis equal or the right or the bottom one a unit wider.
        /// </summary>
        private static IEnumerable<string> BoardOutOfPlace(BoardGeometry geometry, PanelSize frame)
        {
            PanelRect board = geometry.Board;
            string cellsBox = BoxOf(geometry.Cells);
            if (Rect(board) != cellsBox)
            {
                yield return "the board is " + Rect(board) + ", its cells " + cellsBox;
            }

            int left = board.X.Value;
            int right = frame.Width.Value - board.X.Value - board.Size.Width.Value;
            int top = board.Y.Value;
            int bottom = frame.Height.Value - board.Y.Value - board.Size.Height.Value;
            if (right < left || right > left + 1 || bottom < top || bottom > top + 1)
            {
                yield return "the board " + Rect(board) + " has margins left " + Number(left) + ", right " + Number(right) + ", top "
                    + Number(top) + " and bottom " + Number(bottom);
            }
        }

        /// <summary>The box of the rectangles of the footprint's cells, each the geometry's rectangle of that cell index.</summary>
        private static string BoxOfCells(BoardGeometry geometry, ulong footprint)
        {
            var cells = new List<PanelRect>();
            for (int index = 0; index < geometry.Cells.Count; index++)
            {
                if ((footprint & new LotCell((byte)index).Bit) != 0)
                {
                    cells.Add(geometry.Cells[index]);
                }
            }

            return BoxOf(cells);
        }

        /// <summary>The smallest rectangle that holds all of these, written as Rect writes one: "(left,top,widthxheight)".</summary>
        private static string BoxOf(IReadOnlyList<PanelRect> rects)
        {
            if (rects.Count == 0)
            {
                throw new XunitException("There are no rectangles to box.");
            }

            int left = rects.Min(rect => rect.X.Value);
            int top = rects.Min(rect => rect.Y.Value);
            int right = rects.Max(rect => rect.X.Value + rect.Size.Width.Value);
            int bottom = rects.Max(rect => rect.Y.Value + rect.Size.Height.Value);
            return "(" + Number(left) + "," + Number(top) + "," + Number(right - left) + "x" + Number(bottom - top) + ")";
        }

        private static BoardGeometry FitOrFail(PanelSize frame, LotSize lot) =>
            BoardGeometry.Fit(frame, lot).TryGet(out BoardGeometry? geometry)
                ? geometry
                : throw new XunitException(Where(lot, frame) + "no geometry, though cells of one unit fit.");

        private static string Where(LotSize lot, PanelSize frame) => lot.ToString() + " in " + Size(frame) + ": ";
    }
}
