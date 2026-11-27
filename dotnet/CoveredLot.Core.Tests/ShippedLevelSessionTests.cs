using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Randomness;
using PuzzleCore.Rules;
using Xunit;
using Xunit.Sdk;
using static CoveredLot.Core.Tests.Support.PictureDescriptions;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// GameSession on every level presentation.json lists, as the catalogue loads it from the shipped texts (design section
    /// 10.1). The engine's par search, with the level's own bays and 200,000 nodes, finds the peakPar the file gives, and
    /// the winning order it finds, tapped through a new session, wins at the peak the search found, for three stars.
    /// Seeded runs of taps on any of a level's vehicles, blocked ones and ones that have left included, then check what
    /// the session and its pictures work out themselves against the engine. After every tap the peak equals
    /// LotSolver.PeakOf over the exits played, each bump names the first blocker the level gives for the vehicles still in
    /// the lot before the tap, and each ignored tap was on a vehicle that had left the lot and says so (NotInLot). The HUD's
    /// bays hold what the state's bays hold (LotState.ColourIn), its ledger counts for each colour of a covered level the
    /// vehicles the rules call covered (JamRules.IsCovered) and is empty on any other, the board's tiles are the vehicles
    /// still in the lot, each over exactly its footprint (LotLevel.FootprintOf), and every string of the board, the HUD
    /// and the end card is printable ASCII and holds none of the banned words. No win has a peak below the par. Every
    /// count is asserted, so no check can pass on an empty walk.
    /// </summary>
    public sealed class ShippedLevelSessionTests
    {
        /// <summary>The node budget of the par search: every shipped level needs far fewer (piece 4 found at most 5,256).</summary>
        private const int ParSearchNodes = 200_000;

        private const int RunsPerLevel = 3;

        /// <summary>A run ends when the level is over, or after this many taps for each of the level's vehicles.</summary>
        private const int TapsPerVehicle = 3;

        /// <summary>How many of the seeded runs' disagreements a failure lists; it gives their count as well.</summary>
        private const int ShownDisagreements = 20;

        /// <summary>The swatch of each colour id from 1, in the tests' own words.</summary>
        private static readonly string[] SwatchNames = { "Red", "Blue", "Yellow", "Green", "Pink", "Orange" };

        [Fact]
        public void The_par_search_with_each_level_s_own_bays_and_200000_nodes_finds_the_peakPar_its_file_gives()
        {
            IReadOnlyList<ListedLevel> levels = ShippedCatalogue.Listed();

            var wrong = new List<string>();
            foreach (ListedLevel level in levels)
            {
                int filePeakPar = FilePeakPar(level);
                PeakParResult result = ParSearch(level);
                if (!(result is PeakParResult.Found found && found.Peak.Value == filePeakPar))
                {
                    wrong.Add(level.Name + ": the search gave " + Described(result) + ", the file gives peakPar " + Number(filePeakPar));
                }
            }

            Assert.Equal(ShippedCatalogue.ListedLevelCount, levels.Count);
            Assert.True(wrong.Count == 0, "The par search disagrees with presentation.json:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void The_winning_order_the_par_search_finds_wins_each_level_through_a_session_at_the_search_s_peak_with_three_stars()
        {
            IReadOnlyList<ListedLevel> levels = ShippedCatalogue.Listed();

            var wrong = new List<string>();
            int taps = 0;
            foreach (ListedLevel level in levels)
            {
                if (!(ParSearch(level) is PeakParResult.Found found))
                {
                    wrong.Add(level.Name + ": the par search found no winning order");
                    continue;
                }

                var session = new GameSession(level.Entry);
                foreach (Exit exit in found.Path)
                {
                    TapFeedback feedback = session.Tap(exit.Piece);
                    taps++;
                    if (!(feedback is TapFeedback.Moved))
                    {
                        wrong.Add(level.Name + ": vehicle " + Number(exit.Piece.Value) + " gave " + feedback.GetType().Name);
                    }
                }

                // The search's order wins at the least peak, which the par-search test pins to the file's peakPar, so a
                // session whose peak is one too low fails here as well as in the seeded runs.
                StarRating stars = Stars.For(session.Peak, level.Entry.PeakPar);
                if (!(session.Outcome is Outcome.Won) || session.Peak.Bays.Value != found.Peak.Value || stars != StarRating.Three)
                {
                    wrong.Add(level.Name + ": the order ends " + session.Outcome.GetType().Name + " with peak " + Number(session.Peak.Bays.Value)
                        + " where the search found " + Number(found.Peak.Value) + ", on par " + Number(level.Entry.PeakPar.Bays.Value)
                        + ", " + stars.ToString() + " stars");
                }
            }

            // Every vehicle leaves once in a winning order, so the orders tap each vehicle of each level once.
            Assert.Equal(ShippedCatalogue.ListedLevelCount, levels.Count);
            Assert.Equal(levels.Sum(level => level.Entry.Level.VehicleCount), taps);
            Assert.True(wrong.Count == 0, "These winning orders do not win at the search's peak with three stars through a session:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void Seeded_runs_of_taps_keep_the_peak_each_bump_each_ignored_tap_and_the_pictures_true_to_the_engine_and_win_no_level_below_its_par()
        {
            IReadOnlyList<ListedLevel> levels = ShippedCatalogue.Listed();

            var tally = new RunTally();
            foreach (ListedLevel level in levels)
            {
                for (int run = 0; run < RunsPerLevel; run++)
                {
                    new SeededRun(level, run, tally).Play();
                }
            }

            Assert.Equal(ShippedCatalogue.ListedLevelCount, levels.Count);
            Assert.Equal(levels.Count * RunsPerLevel, tally.Runs);
            Assert.Equal(0, tally.RunsWithoutTaps);
            Assert.Equal(tally.Taps, tally.PeakChecks);
            Assert.Equal(tally.Taps, tally.PictureChecks);
            Assert.Equal(tally.Taps, tally.Moves + tally.Bumps + tally.Ignored);
            Assert.True(tally.Moves > 0 && tally.Ignored > 0, "The runs moved " + Number(tally.Moves) + " and ignored " + Number(tally.Ignored) + " taps.");
            Assert.True(tally.Bumps > 0, "No tap of the runs bumped, so no blocker was checked.");
            Assert.True(tally.Wins > 0 && tally.Fails > 0, "The runs won " + Number(tally.Wins) + " and failed " + Number(tally.Fails) + " levels.");
            Assert.True(tally.FilledBays > 0 && tally.CoveredCounted > 0 && tally.Tiles > 0,
                "The pictures were checked with " + Number(tally.FilledBays) + " filled bays, " + Number(tally.CoveredCounted)
                + " covered vehicles in their ledgers and " + Number(tally.Tiles) + " tiles.");
            // Each picture check reads at least the HUD's four lines and a label for each tile.
            Assert.True(tally.Strings >= (tally.PictureChecks * 4) + tally.Tiles,
                "Only " + Number(tally.Strings) + " strings were checked over " + Number(tally.PictureChecks) + " pictures.");
            Assert.True(tally.Wrong.Count == 0, "The session or its pictures disagree with the engine " + Number(tally.Wrong.Count) + " times; the first "
                + Number(Math.Min(ShownDisagreements, tally.Wrong.Count)) + ":\n" + string.Join("\n", tally.Wrong.Take(ShownDisagreements)));
        }

        private static PeakParResult ParSearch(ListedLevel level) =>
            LotSolver.PeakPar(level.Entry.Level, level.Entry.Level.Bays.AsCapacity, new NodeBudget(ParSearchNodes));

        /// <summary>The peakPar of the level's entry in presentation.json, as the engine's reader gives it.</summary>
        private static int FilePeakPar(ListedLevel level) =>
            level.Presentation.Grade.Plan.PeakPar.TryGet(out CapacityCount peakPar)
                ? peakPar.Value
                : throw new XunitException(level.Name + " has no peakPar in presentation.json.");

        private static string Described(PeakParResult result) => result switch
        {
            PeakParResult.Found found => "Found(" + Number(found.Peak.Value) + ")",
            PeakParResult.BudgetExceeded exceeded => "BudgetExceeded(" + Number(exceeded.NodesUsed.Value) + " nodes)",
            _ => result.GetType().Name,
        };

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// One seeded run on one level. Run r draws its taps from PCG32 seeded with r on the stream of the level's slot, so
        /// it repeats exactly. Each tap picks any of the level's vehicles, whether it can leave, is blocked or has left, and
        /// the run ends when the level is over or after three taps per vehicle.
        /// </summary>
        private sealed class SeededRun
        {
            private readonly ListedLevel _level;
            private readonly RunTally _tally;
            private readonly Pcg32 _random;
            private readonly GameSession _session;
            private readonly List<Exit> _exits = new();
            private readonly string _name;
            private int _taps;

            public SeededRun(ListedLevel level, int run, RunTally tally)
            {
                _level = level;
                _tally = tally;
                _random = Pcg32.Seeded((ulong)run, (ulong)level.Presentation.Slot.Value);
                _session = new GameSession(level.Entry);
                _name = level.Name + " run " + Number(run);
            }

            private LotLevel Lot => _level.Entry.Level;

            public void Play()
            {
                while (_taps < TapsPerVehicle * Lot.VehicleCount && _session.Outcome is Outcome.InPlay)
                {
                    TapOnce();
                }

                _tally.Runs++;
                _tally.Taps += _taps;
                if (_taps == 0)
                {
                    _tally.RunsWithoutTaps++;
                }

                CountTheEnd();
            }

            /// <summary>Taps one vehicle, then checks the feedback, the peak and the pictures against the engine.</summary>
            private void TapOnce()
            {
                var piece = new PieceId((byte)_random.NextIndex(Lot.VehicleCount));
                LotState before = _session.State;
                TapFeedback feedback = _session.Tap(piece);
                _taps++;
                string tap = _name + " tap " + Number(_taps) + " (vehicle " + Number(piece.Value) + ")";
                switch (feedback)
                {
                    case TapFeedback.Moved:
                        _exits.Add(new Exit(piece));
                        _tally.Moves++;
                        break;
                    case TapFeedback.Bumped bumped:
                        _tally.Bumps++;
                        CheckBlocker(bumped.Blocker, Lot.FirstBlocker(piece, before.Exited), tap);
                        break;
                    case TapFeedback.Ignored ignored:
                        _tally.Ignored++;
                        CheckIgnored(ignored.Reason, before.HasExited(piece), tap);
                        break;
                    default:
                        _tally.Wrong.Add(tap + ": gave " + feedback.GetType().Name);
                        break;
                }

                int peakOf = LotSolver.PeakOf(Lot, _exits);
                _tally.PeakChecks++;
                if (_session.Peak.Bays.Value != peakOf)
                {
                    _tally.Wrong.Add(tap + ": the session's peak is " + Number(_session.Peak.Bays.Value) + ", LotSolver.PeakOf gives " + Number(peakOf));
                }

                CheckPictures(tap);
            }

            /// <summary>
            /// The pictures after the tap: the HUD's bays against the state's bays, its ledger against the rules' covers, the
            /// board's tiles against the vehicles in the lot and their footprints, and every string of the board, the HUD and
            /// the end card against the text rules.
            /// </summary>
            private void CheckPictures(string tap)
            {
                BoardPicture board = BoardPicture.Of(_session);
                HudPicture hud = HudPicture.Of(_session);
                _tally.PictureChecks++;
                _tally.Tiles += board.Tiles.Count;
                Compare(tap + ": the bays", hud.Bays.Select(Bay).ToList(), ExpectedBays());
                Compare(tap + ": the ledger", hud.Ledger.Select(Pill).ToList(), ExpectedLedger());
                Compare(tap + ": the tiles", TilesOf(board), ExpectedTiles());
                foreach (string line in PictureStrings.Of(board, hud, EndPicture.Of(_session)))
                {
                    _tally.Strings++;
                    _tally.Wrong.AddRange(CopyRules.BrokenBy(line).Select(rule => tap + ": '" + line + "' " + rule));
                }
            }

            /// <summary>Each bay of the state from the left, as the colour waiting in it (LotState.ColourIn) gives it: "B Blue", or "empty".</summary>
            private IReadOnlyList<string> ExpectedBays()
            {
                LotState state = _session.State;
                var bays = new List<string>();
                for (int index = 0; index < state.Capacity.Value; index++)
                {
                    if (state.ColourIn(new SlotIndex((byte)index)).TryGet(out ColourId colour))
                    {
                        bays.Add(LetterOf(colour.Value) + " " + SwatchNameOf(colour.Value));
                        _tally.FilledBays++;
                    }
                    else
                    {
                        bays.Add("empty");
                    }
                }

                return bays;
            }

            /// <summary>
            /// On a covered level, for each colour id the level's vehicles have, in ascending order, its letter, how many of its
            /// vehicles the engine's rules call covered in the state (JamRules.IsCovered) and its swatch: "R 1 Red". On any other
            /// level, nothing.
            /// </summary>
            private IReadOnlyList<string> ExpectedLedger()
            {
                var pills = new List<string>();
                if (!Lot.IsCoveredLevel)
                {
                    return pills;
                }

                var rules = new JamRules(Lot);
                var covered = new SortedDictionary<int, int>();
                foreach (Vehicle vehicle in Lot.Vehicles)
                {
                    covered.TryGetValue(vehicle.Colour.Value, out int count);
                    covered[vehicle.Colour.Value] = rules.IsCovered(_session.State, vehicle.Id) ? count + 1 : count;
                }

                foreach (KeyValuePair<int, int> colour in covered)
                {
                    pills.Add(LetterOf(colour.Key) + " " + Number(colour.Value) + " " + SwatchNameOf(colour.Key));
                    _tally.CoveredCounted += colour.Value;
                }

                return pills;
            }

            /// <summary>Each vehicle still in the lot, by id in ascending order, with the cells of its footprint (LotLevel.FootprintOf).</summary>
            private IReadOnlyList<string> ExpectedTiles()
            {
                var tiles = new List<string>();
                for (int index = 0; index < Lot.VehicleCount; index++)
                {
                    var piece = new PieceId((byte)index);
                    if (!_session.State.HasExited(piece))
                    {
                        tiles.Add(Number(index) + " over cells " + CellsOf(Lot.FootprintOf(piece)));
                    }
                }

                return tiles;
            }

            /// <summary>Each tile of the board, by its vehicle, with the cells its box covers, each found by its row and column through the engine's LotSize.</summary>
            private IReadOnlyList<string> TilesOf(BoardPicture board)
            {
                var tiles = new List<string>();
                foreach (TilePicture tile in board.Tiles)
                {
                    ulong cells = 0;
                    for (int row = tile.Box.Row.Value; row < tile.Box.Row.Value + tile.Box.Rows; row++)
                    {
                        for (int column = tile.Box.Column.Value; column < tile.Box.Column.Value + tile.Box.Columns; column++)
                        {
                            cells |= Lot.Size.CellAt(new Row((byte)row), new Column((byte)column)).Bit;
                        }
                    }

                    tiles.Add(Number(tile.Piece.Value) + " over cells " + CellsOf(cells));
                }

                return tiles;
            }

            private void Compare(string what, IReadOnlyList<string> pictured, IReadOnlyList<string> expected)
            {
                if (!pictured.SequenceEqual(expected, StringComparer.Ordinal))
                {
                    _tally.Wrong.Add(what + " are [" + string.Join("; ", pictured) + "], the engine gives [" + string.Join("; ", expected) + "]");
                }
            }

            /// <summary>The cells of a mask, by index in ascending order: "14,20".</summary>
            private static string CellsOf(ulong cells)
            {
                var indexes = new List<string>();
                for (int index = 0; index <= LotCell.MaxIndex; index++)
                {
                    if ((cells & (1UL << index)) != 0)
                    {
                        indexes.Add(Number(index));
                    }
                }

                return string.Join(",", indexes);
            }

            /// <summary>The letter the engine's level texts write for a colour id (LotText.ColourLetters): "R" for 1.</summary>
            private static string LetterOf(int colour) => LotText.ColourLetters.Substring(colour - 1, 1);

            /// <summary>The swatch of a colour id from 1, in the tests' own words.</summary>
            private static string SwatchNameOf(int colour) => SwatchNames[colour - 1];

            private void CheckBlocker(Blocker bumpedInto, Option<Blocker> firstBlocker, string tap)
            {
                if (firstBlocker != Option.Some(bumpedInto))
                {
                    _tally.Wrong.Add(tap + ": bumped into " + bumpedInto + ", the level's first blocker is " + firstBlocker);
                }
            }

            /// <summary>
            /// A run taps only the level's vehicles and ends when the level is over, so a tap of it is ignored only as one on a
            /// vehicle that has left the lot, NotInLot.
            /// </summary>
            private void CheckIgnored(IgnoredTap reason, bool hadLeft, string tap)
            {
                if (reason != IgnoredTap.NotInLot || !hadLeft)
                {
                    _tally.Wrong.Add(tap + ": ignored as " + reason.ToString() + ", and the vehicle had " + (hadLeft ? "left" : "not left") + " the lot");
                }
            }

            private void CountTheEnd()
            {
                if (_session.Outcome is Outcome.Failed)
                {
                    _tally.Fails++;
                }
                else if (_session.Outcome is Outcome.Won)
                {
                    _tally.Wins++;
                    if (_session.Peak.Bays < _level.Entry.PeakPar.Bays)
                    {
                        _tally.Wrong.Add(_name + ": won with peak " + Number(_session.Peak.Bays.Value) + ", below the par " + Number(_level.Entry.PeakPar.Bays.Value));
                    }
                }
            }
        }

        /// <summary>What the seeded runs did, counted so that no check passes on an empty walk, and what they found wrong.</summary>
        private sealed class RunTally
        {
            public int Runs { get; set; }

            public int RunsWithoutTaps { get; set; }

            public int Taps { get; set; }

            public int PeakChecks { get; set; }

            public int Moves { get; set; }

            public int Bumps { get; set; }

            public int Ignored { get; set; }

            public int Wins { get; set; }

            public int Fails { get; set; }

            public int PictureChecks { get; set; }

            public int FilledBays { get; set; }

            /// <summary>The covered vehicles the expected ledgers counted, over every picture check.</summary>
            public int CoveredCounted { get; set; }

            public int Tiles { get; set; }

            public int Strings { get; set; }

            public List<string> Wrong { get; } = new();
        }
    }
}
