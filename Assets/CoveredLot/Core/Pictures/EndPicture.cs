using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using PuzzleCore.Rules;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// What the end card shows of a session (design section 9): no card while the level is in play; the win card, with the
    /// stars the win earns and its peak against the par; or the fail card, with the sentence for the kind of fail. Closed;
    /// only the pictures make them.
    /// </summary>
    public abstract class EndPicture
    {
        private EndPicture()
        {
        }

        /// <summary>The level is in play: no card.</summary>
        public sealed class Playing : EndPicture
        {
            internal Playing()
            {
            }
        }

        /// <summary>The level is won: the win card, under its heading "Cleared!".</summary>
        public sealed class Cleared : EndPicture
        {
            internal Cleared(Peak peak, PeakPar par)
            {
                Rating = Stars.For(peak, par);
                StarsLine = Copy.StarsLine(Rating);
                PeakLine = Copy.PeakLine(peak, par);
            }

            /// <summary>The stars the win earns, by its peak and the level's par.</summary>
            public StarRating Rating { get; }

            /// <summary>"3 stars", "2 stars" or "1 star".</summary>
            public string StarsLine { get; }

            /// <summary>"Most bays used: 3 (par 3)".</summary>
            public string PeakLine { get; }
        }

        /// <summary>The level is failed with every bay full, the one fail a loaded level reaches: the fail card.</summary>
        public sealed class BaysFull : EndPicture
        {
            internal BaysFull() => Text = Copy.AllBaysFull;

            /// <summary>"All bays are full."</summary>
            public string Text { get; }
        }

        /// <summary>The level is failed with no vehicle able to leave, which no loaded level reaches (BRIEF:47): the fail card.</summary>
        public sealed class NoMoveLeft : EndPicture
        {
            internal NoMoveLeft() => Text = Copy.NoMovePossible;

            /// <summary>"No move is possible."</summary>
            public string Text { get; }
        }

        /// <summary>The card for the session's outcome, a win rated by the session's peak and its entry's par.</summary>
        public static EndPicture Of(GameSession session) => Of(session.Outcome, session.Peak, session.Entry.PeakPar);

        /// <summary>
        /// The card for an outcome, a win rated by this peak and this par. Every case of the outcome and every kind of fail
        /// is named, so that a case a later engine adds reaches the discard arm; the pin tests catch it at the next sync
        /// first.
        /// </summary>
        public static EndPicture Of(Outcome outcome, Peak peak, PeakPar par) => outcome switch
        {
            Outcome.InPlay => new Playing(),
            Outcome.Won => new Cleared(peak, par),
            Outcome.Failed { Kind: FailKind.SlotsFull } => new BaysFull(),
            Outcome.Failed { Kind: FailKind.NoLegalMove } => new NoMoveLeft(),
            _ => throw new UnknownEngineCaseException(outcome),
        };
    }
}
