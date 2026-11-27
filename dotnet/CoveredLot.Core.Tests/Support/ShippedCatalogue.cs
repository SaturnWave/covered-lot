using System.Collections.Generic;
using CoveredLot.Core.Catalogue;
using PuzzleCore;
using PuzzleCore.Packs;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The catalogue that LevelCatalogue.Load gives over the texts the game ships, read from the files on disk: the Example
    /// first, then Level 1 to N. A session is made from one of its entries, as the game makes it. A broken catalogue fails
    /// the test that asks for it, naming its problems.
    /// </summary>
    internal static class ShippedCatalogue
    {
        /// <summary>The levels engine-sync.txt records for the synced commit; a sync that changes the count changes this.</summary>
        public const int ListedLevelCount = 98;

        /// <summary>The Example, the catalogue's first entry.</summary>
        public static CatalogueEntry Example()
        {
            CatalogueEntry first = Entries()[0];
            return first.Title is EntryTitle.Example
                ? first
                : throw new XunitException("The catalogue's first entry is " + CatalogueDescriptions.Entry(first) + ", not the Example.");
        }

        /// <summary>
        /// Each level presentation.json lists, in its order: the catalogue's entry for it, Level 1 to N, with the level as
        /// the file's entry gives it, read by the engine's reader apart from the catalogue. Each pair is checked to be the
        /// same level by its slot.
        /// </summary>
        public static IReadOnlyList<ListedLevel> Listed()
        {
            IReadOnlyList<PresentationLevel> presentation = ShippedLevels.Listed();
            ReadOnlyArray<CatalogueEntry> entries = Entries();
            if (entries.Count != presentation.Count + 1)
            {
                throw new XunitException("The catalogue holds " + CatalogueDescriptions.Number(entries.Count) + " entries; presentation.json lists "
                    + CatalogueDescriptions.Number(presentation.Count) + " levels besides the Example.");
            }

            var listed = new List<ListedLevel>();
            for (int position = 0; position < presentation.Count; position++)
            {
                CatalogueEntry entry = entries[position + 1];
                if (entry.Slot != Option.Some(presentation[position].Slot))
                {
                    throw new XunitException("Entry " + CatalogueDescriptions.Entry(entry) + " is not " + presentation[position].Level.Value
                        + ", which presentation.json lists in slot " + CatalogueDescriptions.Number(presentation[position].Slot.Value) + ".");
                }

                listed.Add(new ListedLevel(entry, presentation[position]));
            }

            return listed;
        }

        /// <summary>Every entry, as the game lists them: the Example, then Level 1 to N.</summary>
        public static ReadOnlyArray<CatalogueEntry> Entries() => CatalogueDescriptions.EntriesOf(LevelCatalogue.Load(new DiskLevelTexts()));
    }

    /// <summary>A level presentation.json lists: the catalogue's entry for it, and the file's entry for it as the engine reads it.</summary>
    internal sealed record ListedLevel(CatalogueEntry Entry, PresentationLevel Presentation)
    {
        /// <summary>The level's id, which names its text: "campaign-jam-0003".</summary>
        public string Name => Presentation.Level.Value;
    }
}
