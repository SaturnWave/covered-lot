using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Tests.Support;
using PuzzleCore.Jam;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The catalogue's copy (design section 9): the problem screen's one sentence for players, and the development-only
    /// line of each catalogue problem, exact for every kind of problem and both kinds of level source, with the slot in
    /// four digits or more. The words for the engine's LotTextProblem and LoadProblem values come from explicit maps, never
    /// an enum's name, which cover every value the engine declares, found by reflection, so a new value fails here (and
    /// in the pins) at the next sync. Every line is printable ASCII and holds none of the brief's banned words, and the
    /// rules are shown to catch each planted break. The tests run under tr-TR and under sv-SE, and the exact lines are the
    /// same under both.
    /// </summary>
    public sealed class CopyTests
    {
        private static readonly IReadOnlyDictionary<string, Func<LineCase>> LineCases = new Dictionary<string, Func<LineCase>>(StringComparer.Ordinal)
        {
            ["an unreadable presentation"] = () => Case(FakeLevelTexts.Shipped().WithPresentation("{\"format\":"), "presentation: missing or unreadable"),
            ["no presentation"] = () => Case(FakeLevelTexts.Shipped().WithoutPresentation(), "presentation: missing or unreadable"),
            ["a pack of another game"] = () => Case(FakeLevelTexts.Shipped().WithPresentation(FakePresentations.EmptySortPack), "presentation: a pack of another game"),
            ["no Example"] = () => Case(FakeLevelTexts.Shipped().WithoutExample(), "example: text missing"),
            ["no text for slot 3"] = () => Case(FakeLevelTexts.Shipped().WithoutLevel(FakeLevelTexts.Id("campaign-jam-0003")), "slot 0003: text missing"),
            ["no text for slot 71"] = () => Case(FakeLevelTexts.Shipped().WithoutLevel(FakeLevelTexts.Id("campaign-jam-0071")), "slot 0071: text missing"),
            ["slots of four and five digits"] = () => Case(
                FakeLevelTexts.Shipped().WithPresentation(FakePresentations.SortPackOfLongSlots),
                "presentation: a pack of another game", "slot 1234: text missing", "slot 12345: text missing"),
            ["carriage returns in slot 3"] = () => Case(WithLevel3(text => text.Replace("\n", "\r\n", StringComparison.Ordinal)), "slot 0003: line 1, wrong header"),
            ["a trailing newline after the Example"] = () => Case(WithExample(text => text + "\n"), "example: line 9, trailing text"),
            ["a colour total of the Example"] = () => Case(
                WithExample(text => text.Replace("vehicles=B:1:2:N", "vehicles=R:1:2:N", StringComparison.Ordinal)), "example: colour total not a multiple of three"),
            ["overlapping pieces in slot 3"] = () => Case(
                FakeLevelTexts.Shipped().WithLevel(FakeLevelTexts.Id("campaign-jam-0003"), BrokenLevelTexts.ByCase["overlapping vehicles"].Text),
                "slot 0003: pieces overlap (piece 8)"),
            ["a level of another game without a par"] = () => Case(
                FakeLevelTexts.Shipped()
                    .WithPresentation(FakePresentations.SortPackOfOneLevel)
                    .WithLevel(FakeLevelTexts.Id("campaign-sort-0000"), ShippedLevels.Text(ShippedLevels.ExampleName)),
                "presentation: a pack of another game", "slot 0000: no par in the presentation"),
            ["an Example that no order wins"] = () => Case(FakeLevelTexts.Shipped().WithExample(FakeLevels.Unwinnable), "example: no par found"),
        };

        public static TheoryData<string> LineCaseNames => new(LineCases.Keys.OrderBy(name => name, StringComparer.Ordinal));

        [Fact]
        public void The_problem_screen_tells_players_exactly_that_some_levels_could_not_be_loaded()
        {
            Assert.Equal("Some levels could not be loaded.", Copy.LevelsNotLoaded);
        }

        [Theory]
        [MemberData(nameof(LineCaseNames))]
        public void Each_problem_gives_its_exact_development_line(string lineCase)
        {
            LineCase expected = LineCases[lineCase]();

            IEnumerable<string> lines = ProblemsOf(LevelCatalogue.Load(expected.Texts)).Select(Copy.ProblemLine);

            Assert.Equal(expected.Lines, lines);
        }

        [Fact]
        public void The_development_line_cases_reach_every_kind_of_catalogue_problem()
        {
            var kinds = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Func<LineCase> lineCase in LineCases.Values)
            {
                foreach (CatalogueProblem problem in ProblemsOf(LevelCatalogue.Load(lineCase().Texts)))
                {
                    kinds.Add(problem.GetType().Name);
                }
            }

            Assert.Equal(CaseNamesOf(typeof(CatalogueProblem)), kinds);
        }

        [Theory]
        [InlineData(LotTextProblem.WrongHeader, "wrong header")]
        [InlineData(LotTextProblem.UnsupportedRulesVersion, "unsupported rules version")]
        [InlineData(LotTextProblem.MissingOrMisplacedLine, "missing or misplaced line")]
        [InlineData(LotTextProblem.BadNumber, "bad number")]
        [InlineData(LotTextProblem.BadSize, "bad size")]
        [InlineData(LotTextProblem.BadVehicle, "bad piece")]
        [InlineData(LotTextProblem.BadColour, "bad colour")]
        [InlineData(LotTextProblem.BadCover, "bad cover")]
        [InlineData(LotTextProblem.BadClue, "bad clue")]
        [InlineData(LotTextProblem.TrailingText, "trailing text")]
        public void Each_LotTextProblem_has_these_words(LotTextProblem problem, string words)
        {
            Assert.Equal(words, Copy.Of(problem));
        }

        [Theory]
        [InlineData(LoadProblem.BlockingCycle, "pieces wait on each other in a cycle")]
        [InlineData(LoadProblem.PillarBlocksPath, "pillar on a path")]
        [InlineData(LoadProblem.AheadWithoutBlocker, "ahead clue on a clear path")]
        [InlineData(LoadProblem.ColourTotalNotMultipleOfThree, "colour total not a multiple of three")]
        [InlineData(LoadProblem.BayCountOutOfRange, "bay count out of range")]
        [InlineData(LoadProblem.PieceOutOfLot, "piece outside the lot")]
        [InlineData(LoadProblem.PiecesOverlap, "pieces overlap")]
        [InlineData(LoadProblem.SizeOutOfRange, "size out of range")]
        [InlineData(LoadProblem.VehicleCountOutOfRange, "piece count out of range")]
        [InlineData(LoadProblem.PiecesNotInAnchorOrder, "pieces not in anchor order")]
        [InlineData(LoadProblem.PillarOutOfLot, "pillar outside the lot")]
        [InlineData(LoadProblem.UnknownPiece, "no such piece")]
        [InlineData(LoadProblem.ClueOnOpenVehicle, "clue on an open piece")]
        public void Each_LoadProblem_has_these_words(LoadProblem problem, string words)
        {
            Assert.Equal(words, Copy.Of(problem));
        }

        /// <summary>A value the engine adds reaches the map's discard arm, which throws UnknownEngineCaseException, and fails this test.</summary>
        [Fact]
        public void The_LotTextProblem_map_gives_every_value_the_engine_declares_words_of_its_own()
        {
            LotTextProblem[] values = Enum.GetValues<LotTextProblem>();

            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (LotTextProblem value in values)
            {
                words.Add(Copy.Of(value));
            }

            Assert.NotEmpty(values);
            Assert.Equal(values.Length, words.Count);
        }

        /// <summary>A value the engine adds reaches the map's discard arm, which throws UnknownEngineCaseException, and fails this test.</summary>
        [Fact]
        public void The_LoadProblem_map_gives_every_value_the_engine_declares_words_of_its_own()
        {
            LoadProblem[] values = Enum.GetValues<LoadProblem>();

            var words = new HashSet<string>(StringComparer.Ordinal);
            foreach (LoadProblem value in values)
            {
                words.Add(Copy.Of(value));
            }

            Assert.NotEmpty(values);
            Assert.Equal(values.Length, words.Count);
        }

        [Fact]
        public void Every_line_of_the_catalogue_copy_is_printable_ascii_and_holds_no_banned_word()
        {
            IReadOnlyList<string> lines = CatalogueCopy();

            IReadOnlyList<string> broken = lines
                .SelectMany(line => CopyRules.BrokenBy(line).Select(rule => "'" + line + "' " + rule))
                .ToList();

            // The title, the sentence, 10 + 13 map words, and a line per problem of every case above and every broken text.
            Assert.True(lines.Count > 2 + 10 + 13 + LineCases.Count + (2 * BrokenLevelTexts.ByCase.Count),
                "Only " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines were checked.");
            Assert.True(broken.Count == 0, string.Join("\n", broken));
        }

        [Theory]
        [InlineData("Jam session", "holds the word jam")]
        [InlineData("PARKING", "holds the word parking")]
        [InlineData("a robust run", "holds the word bus")]
        [InlineData("betray", "holds the word tray")]
        [InlineData("dashBoard", "holds the word board")]
        [InlineData("blocked", "holds the word block")]
        [InlineData("caf\u00E9", "holds a character that is not printable ASCII")]
        [InlineData("tab\there", "holds a character that is not printable ASCII")]
        [InlineData("line\n", "holds a character that is not printable ASCII")]
        public void The_copy_rules_report_each_planted_break(string line, string rule)
        {
            Assert.Equal(new[] { rule }, CopyRules.BrokenBy(line));
        }

        [Theory]
        [InlineData("Covered Lot")]
        [InlineData("slot 0003: line 1, wrong header")]
        [InlineData("~ !\"#$%&'()*+,-./0123456789:;<=>?@[\\]^_`{|}")]
        public void The_copy_rules_pass_printable_ascii_without_a_banned_word(string line)
        {
            Assert.Empty(CopyRules.BrokenBy(line));
        }

        /// <summary>Every string of the catalogue's copy: the title, the problem screen's sentence, the words of both maps, and the development line of every problem the cases and the broken texts give.</summary>
        private static IReadOnlyList<string> CatalogueCopy()
        {
            var lines = new List<string> { Copy.GameTitle, Copy.LevelsNotLoaded };
            foreach (LotTextProblem value in Enum.GetValues<LotTextProblem>())
            {
                lines.Add(Copy.Of(value));
            }

            foreach (LoadProblem value in Enum.GetValues<LoadProblem>())
            {
                lines.Add(Copy.Of(value));
            }

            foreach (FakeLevelTexts fake in BrokenFakes())
            {
                foreach (CatalogueProblem problem in ProblemsOf(LevelCatalogue.Load(fake)))
                {
                    lines.Add(Copy.ProblemLine(problem));
                }
            }

            return lines;
        }

        /// <summary>The texts of every development-line case, and the shipped texts with each broken level text as slot 3 and as the Example, and with each broken presentation file.</summary>
        private static IReadOnlyList<FakeLevelTexts> BrokenFakes()
        {
            var fakes = new List<FakeLevelTexts>();
            foreach (Func<LineCase> lineCase in LineCases.Values)
            {
                fakes.Add(lineCase().Texts);
            }

            FakeLevelTexts shipped = FakeLevelTexts.Shipped();
            foreach (BrokenLevelText broken in BrokenLevelTexts.ByCase.Values)
            {
                fakes.Add(shipped.WithLevel(FakeLevelTexts.Id("campaign-jam-0003"), broken.Text));
                fakes.Add(shipped.WithExample(broken.Text));
            }

            foreach (string presentation in BrokenPresentations.ByCase.Values)
            {
                fakes.Add(shipped.WithPresentation(presentation));
            }

            return fakes;
        }

        private static IEnumerable<CatalogueProblem> ProblemsOf(CatalogueResult result) => Assert.IsType<CatalogueResult.Broken>(result).Problems;

        /// <summary>The names of the cases nested in a closed set, in ordinal order.</summary>
        private static IEnumerable<string> CaseNamesOf(Type closedSet) => closedSet.GetNestedTypes()
            .Where(type => type.IsSubclassOf(closedSet))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

        private static FakeLevelTexts WithLevel3(Func<string, string> change)
        {
            FakeLevelTexts shipped = FakeLevelTexts.Shipped();
            PuzzleCore.Records.LevelId level = FakeLevelTexts.Id("campaign-jam-0003");
            return shipped.WithLevel(level, change(shipped.LevelText(level)));
        }

        private static FakeLevelTexts WithExample(Func<string, string> change) =>
            FakeLevelTexts.Shipped().WithExample(change(ShippedLevels.Text(ShippedLevels.ExampleName)));

        private static LineCase Case(FakeLevelTexts texts, params string[] lines) => new() { Texts = texts, Lines = lines };

        /// <summary>Texts the catalogue loads, and the development lines of the problems it gives, in order.</summary>
        private sealed record LineCase
        {
            public required FakeLevelTexts Texts { get; init; }

            public required IReadOnlyList<string> Lines { get; init; }
        }
    }
}
