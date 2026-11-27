using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Rules;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// GameSession on the Example, the brief's 6x6 board, as the catalogue loads it from the shipped texts (design section
    /// 8.2): the known answers of its taps. A new session shows vehicles 3 and 7 covered, with a peak of 0 and no tap yet.
    /// Vehicle 5 bumps into vehicle 4 at the start, and the state, the vehicles shown covered, the peak and the outcome stay
    /// as they were. The blue vehicles 0, 3 and 4 fill bays 0 to 2 and leave as a set. The taps 0,3,4,2,8,5,1,6,7 win with a
    /// peak of 3, the Example's par, for three stars. The taps 0,3,1,8 fill all four bays and fail, and a tap after that is
    /// ignored as the level is over, again with all four as they were.
    /// </summary>
    /// <remarks>
    /// The session shows the state, the outcome, the peak, the last tap and which vehicles it shows covered, not the
    /// attempt's seen covers or its count of moves. So a tap that leaves the attempt as it was is checked on what the
    /// session shows: the state, whose exited vehicles count the moves (each move sends one vehicle out), and the vehicles
    /// shown covered, which is where the seen covers show.
    /// </remarks>
    public sealed class GameSessionTests
    {
        /// <summary>The vehicles of the Example, ids 0 to 8.</summary>
        private const int ExampleVehicleCount = 9;

        [Fact]
        public void A_new_session_shows_vehicles_3_and_7_covered_with_a_peak_of_0_and_no_tap_yet()
        {
            CatalogueEntry example = ShippedCatalogue.Example();

            var session = new GameSession(example);

            Assert.Same(example, session.Entry);
            Assert.Equal(new JamRules(example.Level).Start, session.State);
            Assert.Equal(new[] { 3, 7 }, ShownCovered(session));
            Assert.Equal(0, session.Peak.Bays.Value);
            Assert.IsType<TapFeedback.None>(session.LastTap);
            Assert.Equal(new Outcome.InPlay(), session.Outcome);
        }

        [Fact]
        public void Tapping_vehicle_5_first_bumps_it_into_vehicle_4_and_leaves_the_state_the_covers_the_peak_and_the_outcome_as_they_were()
        {
            var session = new GameSession(ShippedCatalogue.Example());
            LotState start = session.State;

            TapFeedback feedback = session.Tap(new PieceId(5));

            TapFeedback.Bumped bumped = Assert.IsType<TapFeedback.Bumped>(feedback);
            Assert.Same(feedback, session.LastTap);
            Assert.Equal(new PieceId(5), bumped.Piece);
            Assert.Equal(new Blocker.Piece(new PieceId(4)), bumped.Blocker);
            Assert.Equal(start, session.State);
            Assert.Equal(0, session.State.ExitedCount);
            Assert.Equal(new[] { 3, 7 }, ShownCovered(session));
            Assert.Equal(0, session.Peak.Bays.Value);
            Assert.Equal(new Outcome.InPlay(), session.Outcome);
        }

        [Fact]
        public void The_taps_0_3_4_reach_a_peak_of_3_and_leave_every_bay_empty()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 4);

            Assert.Empty(notMoved);
            Assert.Equal(3, session.Peak.Bays.Value);
            Assert.Equal(new[] { "empty", "empty", "empty", "empty" }, Bays(session.State));
            Assert.Equal(new Outcome.InPlay(), session.Outcome);
        }

        [Fact]
        public void The_taps_0_3_4_2_8_5_1_6_7_win_with_a_peak_of_3_for_three_stars()
        {
            CatalogueEntry example = ShippedCatalogue.Example();
            var session = new GameSession(example);

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 4, 2, 8, 5, 1, 6, 7);

            Assert.Empty(notMoved);
            Assert.Equal(new Outcome.Won(), session.Outcome);
            Assert.Equal(3, session.Peak.Bays.Value);
            Assert.Equal(3, example.PeakPar.Bays.Value);
            Assert.Equal(StarRating.Three, Stars.For(session.Peak, example.PeakPar));
        }

        [Fact]
        public void The_taps_0_3_1_8_fail_with_the_bays_full_and_one_more_tap_is_ignored_as_the_level_is_over()
        {
            CatalogueEntry example = ShippedCatalogue.Example();
            var session = new GameSession(example);
            var four = new PieceId(4);

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 1, 8);
            Outcome failed = session.Outcome;
            LotState full = session.State;
            int peak = session.Peak.Bays.Value;
            IReadOnlyList<int> covered = ShownCovered(session);
            TapFeedback feedback = session.Tap(four);

            Assert.Empty(notMoved);
            Assert.Equal(new Outcome.Failed(FailKind.SlotsFull), failed);
            Assert.Equal(4, peak);
            Assert.Equal(new[] { 7 }, covered);
            // The premise, from the engine: vehicle 4 is still in the lot with nothing on its path, so in play it would leave.
            Assert.False(full.HasExited(four), "Vehicle 4 has left the lot.");
            Assert.False(example.Level.FirstBlocker(four, full.Exited).HasValue, "Something stands on vehicle 4's path.");
            TapFeedback.Ignored ignored = Assert.IsType<TapFeedback.Ignored>(feedback);
            Assert.Same(feedback, session.LastTap);
            Assert.Equal(four, ignored.Piece);
            Assert.Equal(IgnoredTap.LevelOver, ignored.Reason);
            Assert.Equal(full, session.State);
            Assert.Equal(covered, ShownCovered(session));
            Assert.Equal(peak, session.Peak.Bays.Value);
            Assert.Equal(new Outcome.Failed(FailKind.SlotsFull), session.Outcome);
        }

        /// <summary>Taps the vehicles in order; returns each tap that did not move its vehicle, with the feedback it gave.</summary>
        private static IReadOnlyList<string> TapAll(GameSession session, params byte[] pieces)
        {
            var notMoved = new List<string>();
            foreach (byte piece in pieces)
            {
                TapFeedback feedback = session.Tap(new PieceId(piece));
                if (!(feedback is TapFeedback.Moved moved && moved.Piece.Value == piece))
                {
                    notMoved.Add("vehicle " + piece.ToString(CultureInfo.InvariantCulture) + ": " + feedback.GetType().Name);
                }
            }

            return notMoved;
        }

        /// <summary>The ids of the Example's vehicles that the session shows covered, in ascending order.</summary>
        private static IReadOnlyList<int> ShownCovered(GameSession session)
        {
            Assert.Equal(ExampleVehicleCount, session.Entry.Level.VehicleCount);
            var covered = new List<int>();
            for (int piece = 0; piece < session.Entry.Level.VehicleCount; piece++)
            {
                if (session.IsShownCovered(new PieceId((byte)piece)))
                {
                    covered.Add(piece);
                }
            }

            return covered;
        }

        /// <summary>Each bay of the state from the left, as "empty" or the colour id waiting in it.</summary>
        private static IReadOnlyList<string> Bays(LotState state)
        {
            var bays = new List<string>();
            for (int bay = 0; bay < state.Capacity.Value; bay++)
            {
                bays.Add(state.ColourIn(new SlotIndex((byte)bay)).TryGet(out ColourId colour)
                    ? "colour " + colour.Value.ToString(CultureInfo.InvariantCulture)
                    : "empty");
            }

            return bays;
        }
    }
}
