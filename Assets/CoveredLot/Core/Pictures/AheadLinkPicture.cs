using System;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The arrow of an Ahead tag (design section 8.3): from the centre of the tagged vehicle's front cell to the centre of
    /// the first cell of its path that the vehicle ahead of it stands on. The vehicle ahead is the first one on its path at
    /// the level's start (LotLevel.FirstOnPathAtStart), fixed for the whole attempt, so the arrow stays as it is when either
    /// vehicle leaves.
    /// </summary>
    public sealed class AheadLinkPicture
    {
        /// <summary>The link of the tagged vehicle to the first cell of its path that the vehicle ahead of it stands on.</summary>
        private AheadLinkPicture(LotLevel level, PieceId tagged, LotCell reference)
        {
            From = new GridCell(FrontCellOf(level, tagged), level.Size);
            To = new GridCell(reference, level.Size);
        }

        /// <summary>The tagged vehicle's front cell, where the arrow starts.</summary>
        public GridCell From { get; }

        /// <summary>The cell of the vehicle ahead that the arrow points at: the first of its cells on the tagged vehicle's path.</summary>
        public GridCell To { get; }

        /// <summary>
        /// The link of an Ahead tag on the vehicle. None when no vehicle stands on its path at the start, or none of that
        /// vehicle's cells lies on the path; neither happens on a loaded level, since the engine's loader refuses an Ahead
        /// tag on a clear path and the engine finds the first vehicle on a path by the cells of the path.
        /// </summary>
        internal static Option<AheadLinkPicture> Of(LotLevel level, PieceId tagged)
        {
            if (!level.FirstOnPathAtStart(tagged).TryGet(out PieceId ahead))
            {
                return Option.None<AheadLinkPicture>();
            }

            ulong aheadCells = level.FootprintOf(ahead);
            foreach (LotCell cell in level.PathOf(tagged))
            {
                if ((aheadCells & cell.Bit) != 0)
                {
                    return Option.Some(new AheadLinkPicture(level, tagged, cell));
                }
            }

            return Option.None<AheadLinkPicture>();
        }

        /// <summary>
        /// The vehicle's front cell, the cell of its footprint at the end it faces: the lowest cell when it faces north or
        /// west, the highest when it faces east or south, since cells count row by row from the top left.
        /// </summary>
        private static LotCell FrontCellOf(LotLevel level, PieceId piece)
        {
            ulong cells = level.FootprintOf(piece);
            return level.VehicleOf(piece).Facing switch
            {
                Facing.North or Facing.West => LowestCellOf(cells),
                Facing.East or Facing.South => HighestCellOf(cells),
                var other => throw new UnknownEngineCaseException(other),
            };
        }

        private static LotCell LowestCellOf(ulong cells)
        {
            for (int index = 0; index <= LotCell.MaxIndex; index++)
            {
                var cell = new LotCell((byte)index);
                if ((cells & cell.Bit) != 0)
                {
                    return cell;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(cells), "A footprint holds at least one cell.");
        }

        private static LotCell HighestCellOf(ulong cells)
        {
            for (int index = LotCell.MaxIndex; index >= 0; index--)
            {
                var cell = new LotCell((byte)index);
                if ((cells & cell.Bit) != 0)
                {
                    return cell;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(cells), "A footprint holds at least one cell.");
        }
    }
}
