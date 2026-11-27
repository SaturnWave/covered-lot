using System.Collections.Generic;
using System.Linq;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using Xunit;
using static CoveredLot.Core.Tests.Support.PictureDescriptions;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The tags list and the Ahead links on variants of the Example (design sections 8.3 and 9). The variant board keeps the
    /// covers 3 and 7 and carries the truthful canonical tags line Not(3,R);Not(3,Y);Ahead(7,Same);Touch(7,Y,2): its rows
    /// read "1: not R, not Y" and "2: same as ahead, touches 2 Y", and its one link runs from cell 27, the front of vehicle 7,
    /// which faces west, to cell 24, the first cell of vehicle 6 on its path, and stays so after both have left. The east
    /// variant also covers vehicle 2, which faces east, and tags it Ahead(2,Differs): its link starts at the highest cell of
    /// its footprint, 13, and points at cell 14 of vehicle 3, and its row comes first, as its vehicle does.
    /// </summary>
    public sealed class VariantBoardTests
    {
        private static readonly CoversLine ShippedCovers = new("covers=3,7");
        private static readonly CluesLine VariantClues = new("clues=Not(3,R);Not(3,Y);Ahead(7,Same);Touch(7,Y,2)");
        private static readonly CoversLine EastCovers = new("covers=2,3,7");
        private static readonly CluesLine EastClues = new("clues=Ahead(2,Differs);Not(3,R)");

        /// <summary>Colour ids 1 and 3, whose letters are R and Y (LotText.ColourLetters).</summary>
        private static readonly ColourId Red = new(1);
        private static readonly ColourId Yellow = new(3);

        [Fact]
        public void The_variant_text_is_the_text_the_engine_writes_for_its_level_and_each_of_its_tags_is_true()
        {
            string text = SixBySixVariants.Text(ShippedCovers, VariantClues);

            LotLevel level = SixBySixVariants.Entry(text).Level;

            Assert.Equal(text, LotText.Write(level));
            Assert.Equal(4, level.Clues.Count);
            LotClue.Not notRed = Assert.IsType<LotClue.Not>(level.Clues[0]);
            LotClue.Not notYellow = Assert.IsType<LotClue.Not>(level.Clues[1]);
            Assert.Equal((Piece(3), Red), (notRed.On, notRed.Colour));
            Assert.Equal((Piece(3), Yellow), (notYellow.On, notYellow.Colour));
            Assert.NotEqual(Red, level.VehicleOf(Piece(3)).Colour);
            Assert.NotEqual(Yellow, level.VehicleOf(Piece(3)).Colour);
            Assert.Equal(new LotClue.Ahead(Piece(7), AheadRelation.Same), level.Clues[2]);
            Assert.Equal(Option.Some(Piece(6)), level.FirstOnPathAtStart(Piece(7)));
            Assert.Equal(level.VehicleOf(Piece(6)).Colour, level.VehicleOf(Piece(7)).Colour);
            Assert.Equal(new LotClue.Touch(Piece(7), Yellow, 2), level.Clues[3]);
            Assert.Equal(new[] { 5, 8 }, YellowNeighbours(level, Piece(7)));
        }

        [Fact]
        public void The_variant_rows_read_1_not_R_not_Y_and_2_same_as_ahead_touches_2_Y()
        {
            LotLevel level = SixBySixVariants.Entry(SixBySixVariants.Text(ShippedCovers, VariantClues)).Level;

            CluePicture tags = CluePicture.Of(level);

            Assert.Equal(new[] { "1: not R, not Y", "2: same as ahead, touches 2 Y" }, tags.Rows.Select(Row));
            Assert.Equal(new[] { 3, 7 }, tags.Rows.Select(row => (int)row.Piece.Value));
            Assert.Equal(new[] { new[] { "not R", "not Y" }, new[] { "same as ahead", "touches 2 Y" } }, tags.Rows.Select(row => row.Tags.ToArray()));
        }

        [Fact]
        public void The_variant_has_exactly_one_link_from_cell_27_to_cell_24_and_it_stays_as_vehicles_6_and_7_leave()
        {
            var session = new GameSession(SixBySixVariants.Entry(SixBySixVariants.Text(ShippedCovers, VariantClues)));
            var links = new List<string> { Links(BoardPicture.Of(session)) };

            var notMoved = new List<string>();
            foreach (byte piece in new byte[] { 0, 3, 4, 2, 6, 7 })
            {
                if (!(session.Tap(Piece(piece)) is TapFeedback.Moved))
                {
                    notMoved.Add(Number(piece));
                }

                links.Add(Links(BoardPicture.Of(session)));
            }

            Assert.Empty(notMoved);
            Assert.True(session.State.HasExited(Piece(6)) && session.State.HasExited(Piece(7)), "Vehicles 6 and 7 are still in the lot.");
            Assert.Equal(Enumerable.Repeat("cell 27 to cell 24", 7), links);
        }

        [Fact]
        public void An_east_facing_tagged_vehicle_s_link_starts_at_the_highest_cell_of_its_footprint()
        {
            string text = SixBySixVariants.Text(EastCovers, EastClues);
            CatalogueEntry entry = SixBySixVariants.Entry(text);
            LotLevel level = entry.Level;

            BoardPicture board = BoardPicture.Of(new GameSession(entry));

            Assert.Equal(text, LotText.Write(level));
            Assert.Equal(Facing.East, level.VehicleOf(Piece(2)).Facing);
            Assert.Equal(Option.Some(Piece(3)), level.FirstOnPathAtStart(Piece(2)));
            Assert.Equal("cell 13 to cell 14", Links(board));
        }

        [Fact]
        public void The_east_variant_numbers_vehicle_2_before_vehicle_3_and_its_row_reads_differs_from_ahead()
        {
            var session = new GameSession(SixBySixVariants.Entry(SixBySixVariants.Text(EastCovers, EastClues)));

            BoardPicture board = BoardPicture.Of(session);

            Assert.Equal(new[] { "1: differs from ahead", "2: not R" }, CluePicture.Of(session.Entry.Level).Rows.Select(Row));
            Assert.Equal(
                new[] { "2 badge 1", "3 badge 2" },
                board.Tiles.Where(tile => tile.Badge.HasValue).Select(tile => Number(tile.Piece.Value) + " " + Badge(tile.Badge)));
        }

        private static PieceId Piece(byte value) => new(value);

        /// <summary>Every link of the picture, "cell 27 to cell 24", in its order, joined by "; ".</summary>
        private static string Links(BoardPicture board) => string.Join("; ", board.AheadLinks.Select(link => Link(link, board.Size)));

        /// <summary>The yellow vehicles that share an edge with the vehicle's footprint at the start, as the engine counts them for a Touch tag.</summary>
        private static IReadOnlyList<int> YellowNeighbours(LotLevel level, PieceId piece)
        {
            var yellow = new List<int>();
            foreach (PieceId neighbour in LotClues.Neighbours(level, piece))
            {
                if (level.VehicleOf(neighbour).Colour == Yellow)
                {
                    yellow.Add(neighbour.Value);
                }
            }

            return yellow;
        }
    }
}
