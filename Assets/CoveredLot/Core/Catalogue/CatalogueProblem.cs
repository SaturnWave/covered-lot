using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// Why the catalogue could not load: the first problem of one source, the presentation file or one level. A problem with
    /// a level names its source. Closed; only the catalogue makes problems.
    /// </summary>
    public abstract class CatalogueProblem
    {
        private CatalogueProblem()
        {
        }

        /// <summary>
        /// The presentation file is missing, is not the pc-presentation JSON the engine's reader accepts (which also refuses
        /// a Jam level without a par), or has a format version the reader does not know.
        /// </summary>
        public sealed class PresentationUnreadable : CatalogueProblem
        {
            internal PresentationUnreadable()
            {
            }
        }

        /// <summary>The presentation file reads, but its pack belongs to another game.</summary>
        public sealed class NotAJamPack : CatalogueProblem
        {
            internal NotAJamPack()
            {
            }
        }

        /// <summary>The host holds no text for the level.</summary>
        public sealed class LevelTextMissing : CatalogueProblem
        {
            internal LevelTextMissing(LevelSource source) => Source = source;

            public LevelSource Source { get; }
        }

        /// <summary>The level's text is not a pc-lot text: the engine's reader names the first bad line, counted from 1, and what is wrong with it.</summary>
        public sealed class LevelTextMalformed : CatalogueProblem
        {
            internal LevelTextMalformed(LevelSource source, LotTextResult.Malformed malformed)
            {
                Source = source;
                Line = malformed.Line;
                Problem = malformed.Problem;
            }

            public LevelSource Source { get; }

            public int Line { get; }

            public LotTextProblem Problem { get; }
        }

        /// <summary>The level's text reads, but the engine's loader refuses the level, naming the piece it concerns when there is one.</summary>
        public sealed class LevelRejected : CatalogueProblem
        {
            internal LevelRejected(LevelSource source, LoadResult.Rejected rejected)
            {
                Source = source;
                Problem = rejected.Problem;
                Piece = rejected.Piece;
            }

            public LevelSource Source { get; }

            public LoadProblem Problem { get; }

            public Option<PieceId> Piece { get; }
        }

        /// <summary>
        /// A listed level loads, but its presentation entry gives no par. The engine's reader refuses a Jam level without
        /// one, so only a level of another game's pack gets here.
        /// </summary>
        public sealed class PeakParMissing : CatalogueProblem
        {
            internal PeakParMissing(LevelSource.Listed source) => Source = source;

            public LevelSource.Listed Source { get; }
        }

        /// <summary>The Example loads, but the engine's solver finds no par for it: no order wins with its bays, or the search ran out of nodes.</summary>
        public sealed class ExamplePeakParNotFound : CatalogueProblem
        {
            internal ExamplePeakParNotFound()
            {
            }
        }
    }
}
