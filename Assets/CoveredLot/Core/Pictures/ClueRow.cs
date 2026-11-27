using PuzzleCore;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// One row of the tags list (design section 9): a vehicle that carries tags, its badge, and what each of its tags says,
    /// in the level's order of its tags, which is canonical for every shipped level, as in "not R" and "not Y".
    /// </summary>
    public sealed class ClueRow
    {
        internal ClueRow(PieceId piece, ClueNumber badge, ReadOnlyArray<string> tags)
        {
            Piece = piece;
            Badge = badge;
            Tags = tags;
            Line = Copy.TagLine(tags);
        }

        /// <summary>The vehicle the row is about.</summary>
        public PieceId Piece { get; }

        /// <summary>The vehicle's number, which its tile and its ghost show too.</summary>
        public ClueNumber Badge { get; }

        /// <summary>What each of the vehicle's tags says, one or more.</summary>
        public ReadOnlyArray<string> Tags { get; }

        /// <summary>The tags as the row shows them after its badge: "not R, not Y".</summary>
        public string Line { get; }
    }
}
