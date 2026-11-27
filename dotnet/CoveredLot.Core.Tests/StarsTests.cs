using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using PuzzleCore;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The star rule for a win (design section 8.3): three stars when the peak is at most the par, two when it is the par
    /// plus one, and one for any other win. A peak below the par, which no win reaches, still gives three, since the rule
    /// says at most the par.
    /// </summary>
    public sealed class StarsTests
    {
        [Fact]
        public void A_peak_of_3_on_a_par_of_3_earns_three_stars()
        {
            Assert.Equal(StarRating.Three, Stars.For(PeakOf(3), ParOf(3)));
        }

        [Fact]
        public void A_peak_of_4_on_a_par_of_3_earns_two_stars()
        {
            Assert.Equal(StarRating.Two, Stars.For(PeakOf(4), ParOf(3)));
        }

        [Fact]
        public void A_peak_of_5_on_a_par_of_3_earns_one_star()
        {
            Assert.Equal(StarRating.One, Stars.For(PeakOf(5), ParOf(3)));
        }

        [Fact]
        public void A_peak_of_2_on_a_par_of_3_earns_three_stars_as_it_is_at_most_the_par()
        {
            Assert.Equal(StarRating.Three, Stars.For(PeakOf(2), ParOf(3)));
        }

        private static Peak PeakOf(int bays) => new(new CapacityCount(bays));

        private static PeakPar ParOf(int bays) => new(new CapacityCount(bays));
    }
}
