using CoveredLot.Core.Catalogue;

namespace CoveredLot.Core.Session
{
    /// <summary>The brief's star rule for a win, by its peak and the level's par (design section 8.3).</summary>
    public static class Stars
    {
        /// <summary>
        /// The stars of a win with this peak on a level with this par: Three when the peak is at most the par, Two when it is
        /// one bay more, and One for any other win. The brief gives Three for a peak equal to the par and says that no win
        /// has a lower one, since the par is the lowest peak over every winning order; a lower one would get Three all the
        /// same.
        /// </summary>
        public static StarRating For(Peak peak, PeakPar par)
        {
            int baysOverPar = peak.Bays.Value - par.Bays.Value;
            return baysOverPar switch
            {
                <= 0 => StarRating.Three,
                1 => StarRating.Two,
                _ => StarRating.One,
            };
        }
    }
}
