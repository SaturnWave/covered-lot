using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// One level the player can open: its title, its slot in the pack, the level the engine loaded and its par. Only the
    /// catalogue makes entries: the Example has no slot, and a listed level has the slot its presentation entry gives.
    /// </summary>
    public sealed class CatalogueEntry
    {
        private CatalogueEntry(EntryTitle title, Option<LevelIndex> slot, LotLevel level, PeakPar peakPar)
        {
            Title = title;
            Slot = slot;
            Level = level;
            PeakPar = peakPar;
        }

        public EntryTitle Title { get; }

        /// <summary>The level's slot in the pack, which the development line shows in four digits or more; None for the Example.</summary>
        public Option<LevelIndex> Slot { get; }

        public LotLevel Level { get; }

        public PeakPar PeakPar { get; }

        internal static CatalogueEntry ForExample(LotLevel level, PeakPar peakPar) =>
            new CatalogueEntry(new EntryTitle.Example(), Option.None<LevelIndex>(), level, peakPar);

        internal static CatalogueEntry ForListed(LevelNumber number, LevelIndex slot, LotLevel level, PeakPar peakPar) =>
            new CatalogueEntry(new EntryTitle.Numbered(number), Option.Some(slot), level, peakPar);
    }
}
