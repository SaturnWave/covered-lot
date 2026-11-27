using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Rules;
using Xunit;
using static CoveredLot.Core.Tests.Support.PictureDescriptions;
using static CoveredLot.Core.Tests.Support.SessionTaps;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The copy the pictures hold along every shipped level's par path (design section 9): for the Example and each level
    /// presentation.json lists, as the catalogue loads them from the shipped texts, a new session taps the winning order
    /// the engine's par search finds, and at the start and after every tap every string of the board, the HUD and the end
    /// card, found by walking the pictures' public properties (PictureStrings), is printable ASCII and holds none of the
    /// banned words, in any letter case. The walk is shown to find each string of the Example's pictures at the start, and
    /// the lines of its win card and of its fail card. Every count is asserted, so no check can pass on an empty walk. The
    /// seeded runs of ShippedLevelSessionTests hold their pictures' strings to the same rules.
    /// </summary>
    public sealed class ShippedLevelCopyTests
    {
        /// <summary>The node budget of the par search, as the session tests give it: every shipped level needs far fewer.</summary>
        private const int ParSearchNodes = 200_000;

        /// <summary>The HUD's lines that every picture holds, whatever the level: its title, its free bays, its peak and its status.</summary>
        private const int HudLines = 4;

        /// <summary>How many of the walks' findings a failure lists; it gives their count as well.</summary>
        private const int ShownFindings = 20;

        [Fact]
        public void Along_each_shipped_level_s_par_path_every_string_of_the_pictures_is_printable_ascii_and_holds_no_banned_word()
        {
            ReadOnlyArray<CatalogueEntry> entries = ShippedCatalogue.Entries();

            var tally = new CopyTally();
            foreach (CatalogueEntry entry in entries)
            {
                WalkParPath(entry, tally);
            }

            Assert.Equal(ShippedCatalogue.ListedLevelCount + 1, entries.Count);
            // Every vehicle leaves once in a winning order, so the orders tap each vehicle of each level once.
            Assert.Equal(entries.Sum(entry => entry.Level.VehicleCount), tally.Taps);
            Assert.Equal(entries.Count + tally.Taps, tally.Moments);
            // Each moment holds at least the HUD's lines and a label for each vehicle still in the lot.
            Assert.True(tally.Strings >= (tally.Moments * HudLines) + tally.TilesInTheLot,
                "Only " + Number(tally.Strings) + " strings were checked at " + Number(tally.Moments) + " moments.");
            Assert.Equal(entries.Count, tally.WinCards);
            Assert.True(tally.StatusesWithWords > 0, "No status line of the walks said anything.");
            Assert.True(tally.Wrong.Count == 0, "The walks went wrong " + Number(tally.Wrong.Count) + " times; the first "
                + Number(Math.Min(ShownFindings, tally.Wrong.Count)) + ":\n" + string.Join("\n", tally.Wrong.Take(ShownFindings)));
        }

        [Fact]
        public void The_walk_finds_each_string_of_the_Example_s_pictures_at_the_start()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> strings = PictureStrings.Of(BoardPicture.Of(session), HudPicture.Of(session), EndPicture.Of(session));

            var expected = new[]
            {
                // The board: each tile's label, each open tile's letter, the badge of tile 3 and the pillar's glyph.
                "B^", "R>", "Y>", "?v", "B>", "Y^", "R^", "?<", "Y>",
                "B", "R", "Y", "B", "Y", "R", "Y",
                "1", "#",
                // The HUD: the title, the free bays, the peak and the empty status, the ledger's pills, and the tags list's
                // row: its badge, its one tag and its line. The empty bays and the card hold no string.
                "Example", "4 bays free", "Most bays used: 0 (par 3)", string.Empty,
                "R 1", "B 1", "Y 0",
                "1", "not R", "not R",
            };
            Assert.Equal(expected.Order(StringComparer.Ordinal), strings.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void The_walk_finds_the_lines_of_the_win_card_and_of_the_fail_card()
        {
            var won = new GameSession(ShippedCatalogue.Example());
            var failed = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(won, 0, 3, 4, 2, 8, 5, 1, 6, 7).Concat(TapAll(failed, 0, 3, 1, 8)).ToList();

            Assert.Empty(notMoved);
            Assert.Equal(new[] { "3 stars", "Most bays used: 3 (par 3)" }, PictureStrings.Of(EndPicture.Of(won)));
            Assert.Equal(new[] { "All bays are full." }, PictureStrings.Of(EndPicture.Of(failed)));
        }

        /// <summary>Taps the level's par path through a new session, checking the pictures' strings at the start and after every tap.</summary>
        private static void WalkParPath(CatalogueEntry entry, CopyTally tally)
        {
            string name = CatalogueDescriptions.Entry(entry);
            var session = new GameSession(entry);
            CheckStrings(session, name + " at the start", tally);
            if (!(LotSolver.PeakPar(entry.Level, entry.Level.Bays.AsCapacity, new NodeBudget(ParSearchNodes)) is PeakParResult.Found found))
            {
                tally.Wrong.Add(name + ": the par search found no winning order");
                return;
            }

            int taps = 0;
            foreach (Exit exit in found.Path)
            {
                TapFeedback feedback = session.Tap(exit.Piece);
                taps++;
                tally.Taps++;
                string moment = name + " after tap " + Number(taps) + " (vehicle " + Number(exit.Piece.Value) + ")";
                if (!(feedback is TapFeedback.Moved))
                {
                    tally.Wrong.Add(moment + ": the tap gave " + feedback.GetType().Name);
                }

                CheckStrings(session, moment, tally);
            }

            if (EndPicture.Of(session) is EndPicture.Cleared)
            {
                tally.WinCards++;
            }
        }

        private static void CheckStrings(GameSession session, string moment, CopyTally tally)
        {
            BoardPicture board = BoardPicture.Of(session);
            HudPicture hud = HudPicture.Of(session);
            IReadOnlyList<string> strings = PictureStrings.Of(board, hud, EndPicture.Of(session));
            tally.Moments++;
            tally.TilesInTheLot += board.Tiles.Count;
            tally.Strings += strings.Count;
            if (hud.StatusLine.Length > 0)
            {
                tally.StatusesWithWords++;
            }

            foreach (string line in strings)
            {
                tally.Wrong.AddRange(CopyRules.BrokenBy(line).Select(rule => moment + ": '" + line + "' " + rule));
            }
        }

        /// <summary>What the walks did, counted so that no check passes on an empty walk, and what they found wrong.</summary>
        private sealed class CopyTally
        {
            public int Taps { get; set; }

            public int Moments { get; set; }

            public int TilesInTheLot { get; set; }

            public int Strings { get; set; }

            public int WinCards { get; set; }

            public int StatusesWithWords { get; set; }

            public List<string> Wrong { get; } = new();
        }
    }
}
