using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Rules;

namespace CoveredLot.Core.Session
{
    /// <summary>
    /// What the last tap of a session did (design section 8.1): nothing yet, a vehicle drove out, a vehicle bumped into the
    /// first thing on its path, or the tap was ignored. Each tap names the vehicle it was on. Closed; only the session makes
    /// feedback.
    /// </summary>
    public abstract class TapFeedback
    {
        private TapFeedback()
        {
        }

        /// <summary>No tap yet: the feedback of a new session.</summary>
        public sealed class None : TapFeedback
        {
            internal None()
            {
            }
        }

        /// <summary>The vehicle drove out and parked; the events say what the move did, in the engine's order.</summary>
        public sealed class Moved : TapFeedback
        {
            internal Moved(PieceId piece, ReadOnlyArray<PuzzleEvent> events)
            {
                Piece = piece;
                Events = events;
            }

            public PieceId Piece { get; }

            public ReadOnlyArray<PuzzleEvent> Events { get; }
        }

        /// <summary>The vehicle stayed, since something stands on its path: the engine names the first such thing, its blocker. A bump costs nothing.</summary>
        public sealed class Bumped : TapFeedback
        {
            internal Bumped(PieceId piece, Blocker blocker)
            {
                Piece = piece;
                Blocker = blocker;
            }

            public PieceId Piece { get; }

            public Blocker Blocker { get; }
        }

        /// <summary>The tap moved nothing and bumped nothing, for the reason given.</summary>
        public sealed class Ignored : TapFeedback
        {
            internal Ignored(PieceId piece, IgnoredTap reason)
            {
                Piece = piece;
                Reason = reason;
            }

            public PieceId Piece { get; }

            public IgnoredTap Reason { get; }
        }
    }
}
