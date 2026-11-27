using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Randomness;
using PuzzleCore.Rules;
using Xunit;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// GameSession on every level presentation.json lists, as the catalogue loads it from the shipped texts (design section
    /// 10.1). The engine's par search, with the level's own bays and 200,000 nodes, finds the peakPar the file gives, and
    /// the winning order it finds, tapped through a new session, wins with three stars. Seeded runs of taps on any of a
    /// level's vehicles, blocked ones and ones that have left included, then check what the session works out itself
    /// against the engine: after every tap the peak equals LotSolver.PeakOf over the exits played, each bump names the
    /// first blocker the level gives for the vehicles still in the lot before the tap, and no win has a peak below the
    /// par. Every count is asserted, so no check can pass on an empty walk.
    /// </summary>
    public sealed class ShippedLevelSessionTests
    {
        /// <summary>The node budget of the par search: every shipped level needs far fewer (piece 4 found at most 5,256).</summary>
        private const int ParSearchNodes = 200_000;

        private const int RunsPerLevel = 3;

        /// <summary>A run ends when the level is over, or after this many taps for each of the level's vehicles.</summary>
        private const int TapsPerVehicle = 3;

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
        public void The_winning_order_the_par_search_finds_wins_each_level_through_a_session_with_three_stars()
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

                StarRating stars = Stars.For(session.Peak, level.Entry.PeakPar);
                if (!(session.Outcome is Outcome.Won) || stars != StarRating.Three)
                {
                    wrong.Add(level.Name + ": the order ends " + session.Outcome.GetType().Name + " with peak " + Number(session.Peak.Bays.Value)
                        + " on par " + Number(level.Entry.PeakPar.Bays.Value) + ", " + stars.ToString() + " stars");
                }
            }

            // Every vehicle leaves once in a winning order, so the orders tap each vehicle of each level once.
            Assert.Equal(ShippedCatalogue.ListedLevelCount, levels.Count);
            Assert.Equal(levels.Sum(level => level.Entry.Level.VehicleCount), taps);
            Assert.True(wrong.Count == 0, "These winning orders do not win with three stars through a session:\n" + string.Join("\n", wrong));
        }

        [Fact]
        public void Seeded_runs_of_taps_keep_the_peak_and_each_bump_true_to_the_engine_and_win_no_level_below_its_par()
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
            Assert.Equal(tally.Taps, tally.Moves + tally.Bumps + tally.Ignored);
            Assert.True(tally.Moves > 0 && tally.Ignored > 0, "The runs moved " + Number(tally.Moves) + " and ignored " + Number(tally.Ignored) + " taps.");
            Assert.True(tally.Bumps > 0, "No tap of the runs bumped, so no blocker was checked.");
            Assert.True(tally.Wins > 0 && tally.Fails > 0, "The runs won " + Number(tally.Wins) + " and failed " + Number(tally.Fails) + " levels.");
            Assert.True(tally.Wrong.Count == 0, "The session disagrees with the engine:\n" + string.Join("\n", tally.Wrong));
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

            /// <summary>Taps one vehicle, then checks the feedback and the peak against the engine.</summary>
            private void TapOnce()
            {
                var piece = new PieceId((byte)_random.NextIndex(Lot.VehicleCount));
                uint exitedBefore = _session.State.Exited;
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
                        CheckBlocker(bumped.Blocker, Lot.FirstBlocker(piece, exitedBefore), tap);
                        break;
                    case TapFeedback.Ignored:
                        _tally.Ignored++;
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
            }

            private void CheckBlocker(Blocker bumpedInto, Option<Blocker> firstBlocker, string tap)
            {
                if (firstBlocker != Option.Some(bumpedInto))
                {
                    _tally.Wrong.Add(tap + ": bumped into " + bumpedInto + ", the level's first blocker is " + firstBlocker);
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

            public List<string> Wrong { get; } = new();
        }
    }
}
