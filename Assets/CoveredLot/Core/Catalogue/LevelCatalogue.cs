using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using PuzzleCore.Records;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// Loads every level the game offers, once, at boot (design section 5): the pack's presentation file, the Example and
    /// each level that file lists, read and loaded by the engine. All or nothing: Ready when every source loads, and Broken
    /// otherwise, which the game shows as its problem screen. Each problem is a value, never an exception: the only throws
    /// here are the discard arms below, which no text reaches, and the tests feed it a varied set of broken texts, each of
    /// which comes back as its problem.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item><description>The presentation file is read with the engine's strict PresentationJson.Read, and its game must be
    /// Jam.</description></item>
    /// <item><description>The Example is read with LotText.Read and loaded with LotLoader.Load. Its par is the one the
    /// engine's solver finds, LotSolver.PeakPar with the Example's own bays and 1,000,000 nodes, which must find
    /// one.</description></item>
    /// <item><description>Each level the presentation file lists, in the file's order, is read by its id and read and loaded
    /// in the same way, and its par is its presentation entry's Grade.Plan.PeakPar. Level n is the n-th, whatever its
    /// slot.</description></item>
    /// </list>
    /// A source gives its first problem only: a missing text, a malformed text, a level the loader refuses or a missing par,
    /// in that order. The listed levels are tried whenever the presentation file reads, also when its pack is of another
    /// game, so that each source names its own problem; a file that does not read lists no level to try. The discard arm
    /// of each switch over an engine result throws UnknownEngineCaseException: it stands for a case a later engine may add,
    /// which the pin tests catch at the next sync, and no text reaches it.
    /// </remarks>
    public static class LevelCatalogue
    {
        /// <summary>The node budget of the Example's par search, which is the playtest's (PlaytestLevels in the engine).</summary>
        private const int ExampleParNodes = 1_000_000;

        public static CatalogueResult Load(ILevelTexts texts)
        {
            PresentationRead presentation = ReadPresentation(texts);
            var sources = new List<SourceResult> { LoadExample(texts) };
            for (int position = 0; position < presentation.Levels.Count; position++)
            {
                sources.Add(LoadListed(texts, presentation.Levels[position], new LevelNumber(position + 1)));
            }

            var problems = new List<CatalogueProblem>();
            if (presentation.Problem.TryGet(out CatalogueProblem? presentationProblem))
            {
                problems.Add(presentationProblem);
            }

            problems.AddRange(sources.OfType<SourceResult.Failed>().Select(failed => failed.Problem));
            return problems.Count == 0
                ? new CatalogueResult.Ready(ReadOnlyArray.CopyOf(sources.OfType<SourceResult.Loaded>().Select(loaded => loaded.Entry)))
                : new CatalogueResult.Broken(ReadOnlyArray.CopyOf(problems));
        }

        private static PresentationRead ReadPresentation(ILevelTexts texts)
        {
            if (!texts.Read(new LevelTextName.Presentation()).TryGet(out string? text))
            {
                return PresentationRead.Unreadable();
            }

            return PresentationJson.Read(text) switch
            {
                PresentationReadResult.Read read => PresentationRead.Of(read.File),
                PresentationReadResult.UnsupportedVersion or PresentationReadResult.Invalid => PresentationRead.Unreadable(),
                var other => throw new UnknownEngineCaseException(other),
            };
        }

        private static SourceResult LoadExample(ILevelTexts texts) =>
            LoadLevel(texts, new LevelTextName.Example(), new LevelSource.Example(), ExampleEntry);

        private static SourceResult ExampleEntry(LotLevel level) =>
            LotSolver.PeakPar(level, level.Bays.AsCapacity, new NodeBudget(ExampleParNodes)) switch
            {
                PeakParResult.Found found => new SourceResult.Loaded(CatalogueEntry.ForExample(level, new PeakPar(found.Peak))),
                PeakParResult.Unsolvable or PeakParResult.BudgetExceeded => new SourceResult.Failed(new CatalogueProblem.ExamplePeakParNotFound()),
                var other => throw new UnknownEngineCaseException(other),
            };

        private static SourceResult LoadListed(ILevelTexts texts, PresentationLevel listed, LevelNumber number)
        {
            var source = new LevelSource.Listed(listed);
            return LoadLevel(texts, new LevelTextName.Level(listed.Level), source, level => ListedEntry(listed, number, level, source));
        }

        private static SourceResult ListedEntry(PresentationLevel listed, LevelNumber number, LotLevel level, LevelSource.Listed source) =>
            listed.Grade.Plan.PeakPar.TryGet(out CapacityCount peakPar)
                ? new SourceResult.Loaded(CatalogueEntry.ForListed(number, listed.Slot, level, new PeakPar(peakPar)))
                : new SourceResult.Failed(new CatalogueProblem.PeakParMissing(source));

        /// <summary>
        /// Reads the named text, has the engine read and load it, and makes the source's entry from the level with
        /// <paramref name="entryOf"/>; the first problem on the way is the source's.
        /// </summary>
        private static SourceResult LoadLevel(ILevelTexts texts, LevelTextName name, LevelSource source, Func<LotLevel, SourceResult> entryOf)
        {
            if (!texts.Read(name).TryGet(out string? text))
            {
                return new SourceResult.Failed(new CatalogueProblem.LevelTextMissing(source));
            }

            return LotText.Read(text) switch
            {
                LotTextResult.Read read => LoadDefinition(read.Definition, source, entryOf),
                LotTextResult.Malformed malformed => new SourceResult.Failed(new CatalogueProblem.LevelTextMalformed(source, malformed)),
                var other => throw new UnknownEngineCaseException(other),
            };
        }

        private static SourceResult LoadDefinition(LotDefinition definition, LevelSource source, Func<LotLevel, SourceResult> entryOf) =>
            LotLoader.Load(definition) switch
            {
                LoadResult.Loaded loaded => entryOf(loaded.Level),
                LoadResult.Rejected rejected => new SourceResult.Failed(new CatalogueProblem.LevelRejected(source, rejected)),
                var other => throw new UnknownEngineCaseException(other),
            };

        /// <summary>What the presentation file gave: the levels it lists (none when it does not read) and its problem, if it has one.</summary>
        private sealed class PresentationRead
        {
            private PresentationRead(ReadOnlyArray<PresentationLevel> levels, Option<CatalogueProblem> problem)
            {
                Levels = levels;
                Problem = problem;
            }

            public ReadOnlyArray<PresentationLevel> Levels { get; }

            public Option<CatalogueProblem> Problem { get; }

            public static PresentationRead Unreadable() =>
                new PresentationRead(ReadOnlyArray.Empty<PresentationLevel>(), Option.Some<CatalogueProblem>(new CatalogueProblem.PresentationUnreadable()));

            public static PresentationRead Of(PresentationFile file) =>
                new PresentationRead(file.Levels, file.Game == GameKind.Jam
                    ? Option.None<CatalogueProblem>()
                    : Option.Some<CatalogueProblem>(new CatalogueProblem.NotAJamPack()));
        }

        /// <summary>What one level source gave: its entry, or its first problem.</summary>
        private abstract class SourceResult
        {
            private SourceResult()
            {
            }

            public sealed class Loaded : SourceResult
            {
                public Loaded(CatalogueEntry entry) => Entry = entry;

                public CatalogueEntry Entry { get; }
            }

            public sealed class Failed : SourceResult
            {
                public Failed(CatalogueProblem problem) => Problem = problem;

                public CatalogueProblem Problem { get; }
            }
        }
    }
}
