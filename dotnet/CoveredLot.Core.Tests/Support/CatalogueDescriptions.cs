using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Core.Catalogue;
using PuzzleCore;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The catalogue's results written out as plain lines in the tests' own words, apart from Copy: each problem with its
    /// kind, its source and what the engine said ("LevelTextMalformed(Listed(campaign-jam-0003, slot 3), 1, WrongHeader)"),
    /// and each entry with its title, slot and par ("Level 1, slot 0, par 3"). A test compares lists of them, so a failure
    /// shows every line that differs.
    /// </summary>
    internal static class CatalogueDescriptions
    {
        /// <summary>The problems of a Broken result; a Ready one fails the test.</summary>
        public static IReadOnlyList<string> ProblemsOf(CatalogueResult result) => result switch
        {
            CatalogueResult.Broken broken => broken.Problems.Select(Problem).ToList(),
            CatalogueResult.Ready ready => throw new XunitException(
                "The catalogue loaded all " + Number(ready.Entries.Count) + " entries; problems were expected."),
            _ => throw new ArgumentOutOfRangeException(nameof(result), "No description for " + result.GetType().Name + "."),
        };

        /// <summary>The entries of a Ready result; a Broken one fails the test, naming its problems.</summary>
        public static ReadOnlyArray<CatalogueEntry> EntriesOf(CatalogueResult result) => result switch
        {
            CatalogueResult.Ready ready => ready.Entries,
            CatalogueResult.Broken broken => throw new XunitException(
                "The catalogue is broken: " + string.Join("; ", broken.Problems.Select(Problem)) + "."),
            _ => throw new ArgumentOutOfRangeException(nameof(result), "No description for " + result.GetType().Name + "."),
        };

        public static string Problem(CatalogueProblem problem) => problem switch
        {
            CatalogueProblem.PresentationUnreadable => "PresentationUnreadable",
            CatalogueProblem.NotAJamPack => "NotAJamPack",
            CatalogueProblem.LevelTextMissing missing => "LevelTextMissing(" + Source(missing.Source) + ")",
            CatalogueProblem.LevelTextMalformed malformed =>
                "LevelTextMalformed(" + Source(malformed.Source) + ", " + Number(malformed.Line) + ", " + malformed.Problem.ToString() + ")",
            CatalogueProblem.LevelRejected rejected =>
                "LevelRejected(" + Source(rejected.Source) + ", " + rejected.Problem.ToString() + ", " + Piece(rejected.Piece) + ")",
            CatalogueProblem.PeakParMissing missing => "PeakParMissing(" + Source(missing.Source) + ")",
            CatalogueProblem.ExamplePeakParNotFound => "ExamplePeakParNotFound",
            _ => throw new ArgumentOutOfRangeException(nameof(problem), "No description for " + problem.GetType().Name + "."),
        };

        /// <summary>"Example", or "Listed(campaign-jam-0003, slot 3)".</summary>
        public static string Source(LevelSource source) => source switch
        {
            LevelSource.Example => "Example",
            LevelSource.Listed listed => "Listed(" + listed.Id.Value + ", slot " + Number(listed.Slot.Value) + ")",
            _ => throw new ArgumentOutOfRangeException(nameof(source), "No description for " + source.GetType().Name + "."),
        };

        /// <summary>"Example, no slot, par 3", or "Level 63, slot 64, par 4".</summary>
        public static string Entry(CatalogueEntry entry) =>
            Title(entry.Title) + ", " + Slot(entry.Slot) + ", par " + Number(entry.PeakPar.Bays.Value);

        public static string Title(EntryTitle title) => title switch
        {
            EntryTitle.Example => "Example",
            EntryTitle.Numbered numbered => "Level " + Number(numbered.Number.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(title), "No description for " + title.GetType().Name + "."),
        };

        public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Slot(Option<LevelIndex> slot) => slot.TryGet(out LevelIndex index) ? "slot " + Number(index.Value) : "no slot";

        private static string Piece(Option<PieceId> piece) => piece.TryGet(out PieceId id) ? "piece " + Number(id.Value) : "no piece";
    }
}
