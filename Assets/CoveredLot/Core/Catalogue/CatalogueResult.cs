using PuzzleCore;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>What loading the catalogue gave: every entry, or the problems that stopped it. All or nothing. Closed.</summary>
    public abstract class CatalogueResult
    {
        private CatalogueResult()
        {
        }

        /// <summary>Every level loaded: the Example first, then Level 1 to N in the presentation file's order.</summary>
        public sealed class Ready : CatalogueResult
        {
            internal Ready(ReadOnlyArray<CatalogueEntry> entries) => Entries = entries;

            public ReadOnlyArray<CatalogueEntry> Entries { get; }
        }

        /// <summary>
        /// At least one source has a problem: the first problem of each such source, the presentation file's first, then
        /// the Example's, then those of the listed levels in the file's order. Never empty.
        /// </summary>
        public sealed class Broken : CatalogueResult
        {
            internal Broken(ReadOnlyArray<CatalogueProblem> problems) => Problems = problems;

            public ReadOnlyArray<CatalogueProblem> Problems { get; }
        }
    }
}
