using CoveredLot.Core.Catalogue;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Play;
using PuzzleCore.Records;
using PuzzleCore.Rules;

namespace CoveredLot.Core.Session
{
    /// <summary>
    /// One attempt at the level of a catalogue entry (design section 8.2): the engine's Attempt over JamRules, in Campaign
    /// mode. A tap goes to the engine, which decides whether the vehicle leaves, what blocks it, which bay it takes, which
    /// sets clear, which covers lift and whether the level is won or failed. The session keeps only what the screen shows of
    /// the attempt: the last tap's feedback, the peak of bays in use, the outcome and which vehicles are shown covered.
    /// It has no retry: Again, Retry and Restart each make a new session, with a fresh attempt and nothing seen yet, so
    /// every cover comes back (design section 7).
    /// </summary>
    public sealed class GameSession
    {
        private readonly JamRules _rules;
        private readonly Attempt<LotState, Exit, ExitRejection> _attempt;

        public GameSession(CatalogueEntry entry)
        {
            Entry = entry;
            _rules = new JamRules(entry.Level);
            _attempt = new Attempt<LotState, Exit, ExitRejection>(_rules, _rules.Start, PlayMode.Campaign);
            Outcome = _attempt.Outcome;
            LastTap = new TapFeedback.None();
        }

        /// <summary>The catalogue entry this session plays: its title, its slot, its level and its par.</summary>
        public CatalogueEntry Entry { get; }

        /// <summary>The attempt's current state: which vehicles have left the lot and what waits in the bays.</summary>
        public LotState State => _attempt.Current;

        /// <summary>
        /// Whether the attempt is in play, won or failed. The session reads it from the attempt once when it starts and once
        /// after each tap, because each read of the attempt's own Outcome evaluates the state again.
        /// </summary>
        public Outcome Outcome { get; private set; }

        /// <summary>The most bays in use at once so far in this attempt; no bays before the first move.</summary>
        public Peak Peak { get; private set; }

        /// <summary>What the last tap did; None before the first tap.</summary>
        public TapFeedback LastTap { get; private set; }

        /// <summary>
        /// Taps the vehicle, and the engine's attempt tries to drive it out. A vehicle that leaves gives Moved, with the
        /// move's events, and the peak rises to the bays in use as it parks when that is more. A vehicle with something on
        /// its path gives Bumped, with its first blocker, and the attempt stays as it was. A tap on a vehicle that has left
        /// or that the level lacks, and any tap once the level is over, gives Ignored. The session then reads the outcome
        /// once. Returns the feedback, which LastTap holds until the next tap.
        /// </summary>
        /// <remarks>
        /// Once the level is failed, the engine's rules answer each tap with Illegal(NotInPlay); once it is won, the attempt
        /// refuses the tap (Refused, with ActionAfterEnd: Attempt.Move refuses nothing else, and the session never
        /// concedes). Both mean that the level is over. The discard arm stands for a case a later engine may add, which the
        /// pin tests catch at the next sync.
        /// </remarks>
        public TapFeedback Tap(PieceId piece)
        {
            LastTap = _attempt.Move(new Exit(piece)) switch
            {
                StepResult<ExitRejection>.Done done => new TapFeedback.Moved(piece, done.Events),
                StepResult<ExitRejection>.Illegal { Reason: ExitRejection.Blocked blocked } => new TapFeedback.Bumped(piece, blocked.By),
                StepResult<ExitRejection>.Illegal { Reason: ExitRejection.NotInLot } => new TapFeedback.Ignored(piece, IgnoredTap.NotInLot),
                StepResult<ExitRejection>.Illegal { Reason: ExitRejection.NotInPlay } => new TapFeedback.Ignored(piece, IgnoredTap.LevelOver),
                StepResult<ExitRejection>.Refused => new TapFeedback.Ignored(piece, IgnoredTap.LevelOver),
                var other => throw new UnknownEngineCaseException(other),
            };
            if (LastTap is TapFeedback.Moved moved)
            {
                Peak = PeakAfter(Peak, moved.Events);
            }

            Outcome = _attempt.Outcome;
            return LastTap;
        }

        /// <summary>
        /// Whether the screen shows the vehicle covered: it is still under its cover in this state, and its cover has not
        /// been seen in this attempt (the engine's playtest, PlaySession.IsCovered). The vehicle must be one of the level's.
        /// </summary>
        public bool IsShownCovered(PieceId piece) =>
            _rules.IsCovered(State, piece) && !_attempt.Seen.Contains(new HiddenCell(piece.Value));

        /// <summary>
        /// The peak after a move with these events: the vehicle that exited parked in the bay its Exited event names, and
        /// every other kind of event leaves the peak as it is. Every kind is named, so that a kind a later engine adds
        /// reaches the discard arm instead of passing unseen; the pin tests catch it at the next sync first.
        /// </summary>
        private static Peak PeakAfter(Peak peak, ReadOnlyArray<PuzzleEvent> events)
        {
            Peak after = peak;
            foreach (PuzzleEvent item in events)
            {
                after = item switch
                {
                    PuzzleEvent.Exited exited => after.Including(exited.Slot),
                    PuzzleEvent.CoverLifted or PuzzleEvent.SetCleared or PuzzleEvent.SpareAdded or PuzzleEvent.Rewound
                        or PuzzleEvent.Won or PuzzleEvent.Failed => after,
                    var other => throw new UnknownEngineCaseException(other),
                };
            }

            return after;
        }
    }
}
