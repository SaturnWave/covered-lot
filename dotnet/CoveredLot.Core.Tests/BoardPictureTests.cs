using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// BoardPicture and CluePicture on the Example, the brief's 6x6 board, as the catalogue loads it from the shipped text
    /// (design section 9): the known answers of what the board and the tags list show. Each vehicle's box, as (row, column,
    /// rows x columns), and its label, the pillar at (0,4); vehicles 3 and 7 shown covered, with no colour; vehicle 3's
    /// badge 1 and its row of the tags list, "1: not R"; after the taps 0 and 3, the ghosts of both, vehicle 3's as Bv with
    /// badge 1 at (2,2,2x1), and the row still there; and the bump of vehicle 5 into vehicle 4, which marks tile 4 until the
    /// next tap.
    /// </summary>
    public sealed class BoardPictureTests
    {
        /// <summary>The vehicles of the Example, ids 0 to 8.</summary>
        private const int ExampleVehicleCount = 9;

        [Fact]
        public void The_Example_s_tiles_have_these_boxes_and_labels_and_its_pillar_stands_at_0_4()
        {
            BoardPicture board = BoardPicture.Of(new GameSession(ShippedCatalogue.Example()));

            Assert.Equal((6, 6), (board.Size.Width, board.Size.Height));
            Assert.Equal(
                new[]
                {
                    "0 (0,1,2x1) B^", "1 (1,2,1x2) R>", "2 (2,0,1x2) Y>", "3 (2,2,2x1) ?v", "4 (2,4,1x2) B>", "5 (3,5,2x1) Y^",
                    "6 (4,0,2x1) R^", "7 (4,3,1x2) ?<", "8 (5,3,1x2) Y>",
                },
                board.Tiles.Select(Tile));
            Assert.Equal(new[] { "(0,4) #" }, board.Pillars.Select(Pillar));
            Assert.Empty(board.Ghosts);
            Assert.Empty(board.AheadLinks);
        }

        [Fact]
        public void Tiles_3_and_7_are_shown_covered_with_no_colour_and_every_other_tile_open_in_its_own_swatch()
        {
            BoardPicture board = BoardPicture.Of(new GameSession(ShippedCatalogue.Example()));

            Assert.Equal(
                new[]
                {
                    "0 Open(Blue, B)", "1 Open(Red, R)", "2 Open(Yellow, Y)", "3 Covered", "4 Open(Blue, B)", "5 Open(Yellow, Y)",
                    "6 Open(Red, R)", "7 Covered", "8 Open(Yellow, Y)",
                },
                board.Tiles.Select(tile => Number(tile.Piece.Value) + " " + Face(tile.Face)));
            // A covered face holds nothing at all, so no colour of a covered vehicle can reach the screen through it.
            Assert.Empty(typeof(TileFace.Covered).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.Empty(typeof(TileFace.Covered).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        [Fact]
        public void Tile_3_carries_badge_1_and_its_row_of_the_tags_list_reads_1_not_R()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            BoardPicture board = BoardPicture.Of(session);
            CluePicture tags = CluePicture.Of(session.Entry.Level);

            Assert.Equal(
                new[]
                {
                    "0 no badge", "1 no badge", "2 no badge", "3 badge 1", "4 no badge", "5 no badge", "6 no badge", "7 no badge",
                    "8 no badge",
                },
                board.Tiles.Select(tile => Number(tile.Piece.Value) + " " + Badge(tile.Badge)));
            ClueRow row = Assert.Single(tags.Rows);
            Assert.Equal("1: not R", Row(row));
            Assert.Equal(new PieceId(3), row.Piece);
            Assert.Equal(1, row.Badge.Value);
            Assert.Equal(new[] { "not R" }, row.Tags);
        }

        [Fact]
        public void After_the_taps_0_and_3_tile_3_is_gone_its_ghost_Bv_with_badge_1_sits_at_2_2_2x1_and_row_1_remains()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<TapFeedback> taps = new[] { session.Tap(new PieceId(0)), session.Tap(new PieceId(3)) };
            BoardPicture board = BoardPicture.Of(session);

            Assert.All(taps, tap => Assert.IsType<TapFeedback.Moved>(tap));
            Assert.Equal(new[] { 1, 2, 4, 5, 6, 7, 8 }, board.Tiles.Select(tile => (int)tile.Piece.Value));
            Assert.Equal(new[] { "0 (0,1,2x1) B^ Blue no badge", "3 (2,2,2x1) Bv Blue badge 1" }, board.Ghosts.Select(Ghost));
            Assert.Equal(new[] { "1: not R" }, CluePicture.Of(session.Entry.Level).Rows.Select(Row));
        }

        [Fact]
        public void A_bump_of_vehicle_5_marks_tile_4_as_the_blocker_and_the_next_tap_clears_the_mark()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            BoardPicture start = BoardPicture.Of(session);
            TapFeedback bump = session.Tap(new PieceId(5));
            BoardPicture bumped = BoardPicture.Of(session);
            TapFeedback move = session.Tap(new PieceId(0));
            BoardPicture moved = BoardPicture.Of(session);

            Assert.Equal(PlainTiles(Enumerable.Range(0, ExampleVehicleCount)), Marks(start));
            Assert.Equal(new Blocker.Piece(new PieceId(4)), Assert.IsType<TapFeedback.Bumped>(bump).Blocker);
            Assert.Equal(
                new[]
                {
                    "0 Plain", "1 Plain", "2 Plain", "3 Plain", "4 Blocker", "5 Plain", "6 Plain", "7 Plain", "8 Plain", "(0,4) Plain",
                },
                Marks(bumped));
            Assert.IsType<TapFeedback.Moved>(move);
            Assert.Equal(PlainTiles(Enumerable.Range(1, ExampleVehicleCount - 1)), Marks(moved));
        }

        /// <summary>Each tile's mark, by its vehicle, then each pillar's, by its cell.</summary>
        private static IReadOnlyList<string> Marks(BoardPicture board) => board.Tiles
            .Select(tile => Number(tile.Piece.Value) + " " + tile.Mark.ToString())
            .Concat(board.Pillars.Select(pillar => Cell(pillar.Cell) + " " + pillar.Mark.ToString()))
            .ToList();

        /// <summary>The marks of a picture with tiles for these vehicles in which nothing is marked, the pillar at (0,4) included.</summary>
        private static IReadOnlyList<string> PlainTiles(IEnumerable<int> pieces) =>
            pieces.Select(piece => Number(piece) + " Plain").Append("(0,4) Plain").ToList();
    }
}
