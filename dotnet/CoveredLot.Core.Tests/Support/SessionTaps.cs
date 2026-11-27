using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Core.Session;
using PuzzleCore;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>Taps for the picture tests, which play a session to the moment they look at.</summary>
    internal static class SessionTaps
    {
        /// <summary>Taps the vehicles in order; returns each tap that did not move its vehicle, with the feedback it gave.</summary>
        public static IReadOnlyList<string> TapAll(GameSession session, params byte[] pieces)
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
    }
}
