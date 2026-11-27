using System.Collections.Generic;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// What the tags list shows (design section 9): one row for each vehicle that carries tags, in PieceId order, numbered
    /// from 1 in that order. Each row holds the vehicle's tags in the order the level gives them, the canonical order for
    /// every shipped level, since each shipped text is the text the engine writes for its level (LotText.Write). The tags
    /// describe the level's start, so the rows are made from the level alone and none disappears during an attempt.
    /// </summary>
    public sealed class CluePicture
    {
        private CluePicture(ReadOnlyArray<ClueRow> rows) => Rows = rows;

        /// <summary>The rows, in PieceId order; none on a level without tags.</summary>
        public ReadOnlyArray<ClueRow> Rows { get; }

        public static CluePicture Of(LotLevel level)
        {
            var rows = new List<ClueRow>();
            for (int index = 0; index < level.VehicleCount; index++)
            {
                var piece = new PieceId((byte)index);
                ReadOnlyArray<string> tags = TagsOf(level, piece);
                if (!tags.IsEmpty)
                {
                    rows.Add(new ClueRow(piece, new ClueNumber(rows.Count + 1), tags));
                }
            }

            return new CluePicture(ReadOnlyArray.CopyOf(rows));
        }

        /// <summary>The vehicle's badge: the number of its row, or None when it carries no tag.</summary>
        internal Option<ClueNumber> BadgeOf(PieceId piece)
        {
            foreach (ClueRow row in Rows)
            {
                if (row.Piece == piece)
                {
                    return Option.Some(row.Badge);
                }
            }

            return Option.None<ClueNumber>();
        }

        /// <summary>What each tag on the vehicle says, in the level's order of its tags.</summary>
        private static ReadOnlyArray<string> TagsOf(LotLevel level, PieceId piece)
        {
            var tags = new List<string>();
            foreach (LotClue clue in level.Clues)
            {
                if (clue.On == piece)
                {
                    tags.Add(Copy.TagText(clue));
                }
            }

            return ReadOnlyArray.CopyOf(tags);
        }
    }
}
