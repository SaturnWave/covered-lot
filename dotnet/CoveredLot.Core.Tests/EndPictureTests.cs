using System.Collections.Generic;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Rules;
using Xunit;
using static CoveredLot.Core.Tests.Support.PictureDescriptions;
using static CoveredLot.Core.Tests.Support.SessionTaps;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// EndPicture, the end card (design section 9). On the Example, as the catalogue loads it from the shipped texts: a
    /// session in play has no card; the winning line 0,3,4,2,8,5,1,6,7 gives Cleared(Three, 3 stars, Most bays used: 3 (par
    /// 3)); and the taps 0,3,1,8 give BaysFull(All bays are full., Three of one colour leave the bays together.), the
    /// fail's sentence and the rule that frees bays. For any outcome, peak and par: a win at the par, or
    /// below it, gives three stars, one bay over gives "2 stars" and two over "1 star"; a level failed with no move left,
    /// which no loaded level reaches, gives NoMoveLeft(No move is possible.). The tests run under tr-TR and under sv-SE, and
    /// the lines are the same under both.
    /// </summary>
    public sealed class EndPictureTests
    {
        private static readonly PeakPar ParOf3 = new(new CapacityCount(3));

        [Fact]
        public void A_session_in_play_has_no_card_at_the_start_and_after_a_move()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            string start = End(EndPicture.Of(session));
            TapFeedback move = session.Tap(new PieceId(0));
            string afterMove = End(EndPicture.Of(session));

            Assert.IsType<TapFeedback.Moved>(move);
            Assert.Equal(new[] { "Playing", "Playing" }, new[] { start, afterMove });
        }

        [Fact]
        public void The_winning_line_gives_Cleared_Three_3_stars_Most_bays_used_3_par_3()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 4, 2, 8, 5, 1, 6, 7);

            Assert.Empty(notMoved);
            Assert.Equal("Cleared(Three, 3 stars, Most bays used: 3 (par 3))", End(EndPicture.Of(session)));
        }

        [Fact]
        public void The_taps_0_3_1_8_give_BaysFull_All_bays_are_full_with_the_rule_that_frees_bays()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 1, 8);

            Assert.Empty(notMoved);
            Assert.Equal("BaysFull(All bays are full., Three of one colour leave the bays together.)", End(EndPicture.Of(session)));
        }

        [Theory]
        [InlineData(2, "Cleared(Three, 3 stars, Most bays used: 2 (par 3))")]
        [InlineData(3, "Cleared(Three, 3 stars, Most bays used: 3 (par 3))")]
        [InlineData(4, "Cleared(Two, 2 stars, Most bays used: 4 (par 3))")]
        [InlineData(5, "Cleared(One, 1 star, Most bays used: 5 (par 3))")]
        public void A_win_s_card_rates_its_peak_against_the_par(int peak, string card)
        {
            EndPicture end = EndPicture.Of(new Outcome.Won(), new Peak(new CapacityCount(peak)), ParOf3);

            Assert.Equal(card, End(end));
        }

        [Fact]
        public void A_level_failed_with_no_move_left_gives_NoMoveLeft_No_move_is_possible()
        {
            EndPicture end = EndPicture.Of(new Outcome.Failed(FailKind.NoLegalMove), default, ParOf3);

            Assert.Equal("NoMoveLeft(No move is possible.)", End(end));
        }
    }
}
