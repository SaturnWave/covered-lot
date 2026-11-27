using System;
using CoveredLot.Core.Catalogue;
using PuzzleCore;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The Example, the brief's 6x6 board, with other covers and tags, for the picture tests: the shipped text with its
    /// covers line and its tags line replaced, loaded by the catalogue as the Example of a Jam pack that lists no level, so
    /// a session is made from it as the game makes one.
    /// </summary>
    internal static class SixBySixVariants
    {
        /// <summary>The last two lines of six-by-six.txt, its covers and its tags, which have no newline after them.</summary>
        private const string ShippedEnd = "\ncovers=3,7\nclues=Not(3,R)";

        /// <summary>The Example's text with this covers line ("covers=2,3,7") and this tags line ("clues=...") in place of its own.</summary>
        public static string Text(CoversLine covers, CluesLine clues)
        {
            string shipped = ShippedLevels.Text(ShippedLevels.ExampleName);
            if (!shipped.EndsWith(ShippedEnd, StringComparison.Ordinal))
            {
                throw new XunitException("six-by-six.txt does not end with the lines covers=3,7 and clues=Not(3,R).");
            }

            return shipped.Substring(0, shipped.Length - ShippedEnd.Length) + "\n" + covers.Line + "\n" + clues.Line;
        }

        /// <summary>The catalogue's entry for the text, loaded as the Example of a pack that lists no level.</summary>
        public static CatalogueEntry Entry(string text)
        {
            FakeLevelTexts texts = FakeLevelTexts.Nothing.WithPresentation(FakePresentations.EmptyJamPack).WithExample(text);
            ReadOnlyArray<CatalogueEntry> entries = CatalogueDescriptions.EntriesOf(LevelCatalogue.Load(texts));
            return entries.Count == 1 && entries[0].Title is EntryTitle.Example
                ? entries[0]
                : throw new XunitException("The catalogue gave " + CatalogueDescriptions.Number(entries.Count) + " entries for one Example.");
        }
    }

    /// <summary>A level text's covers line, "covers=3,7": a type of its own, so it cannot take the place of the tags line.</summary>
    internal readonly struct CoversLine
    {
        public CoversLine(string line) => Line = line;

        public string Line { get; }
    }

    /// <summary>A level text's tags line, "clues=Not(3,R)": a type of its own, so it cannot take the place of the covers line.</summary>
    internal readonly struct CluesLine
    {
        public CluesLine(string line) => Line = line;

        public string Line { get; }
    }
}
