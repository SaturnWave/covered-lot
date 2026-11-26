using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using PuzzleCore.Records;
using Xunit;
using static CoveredLot.Core.Tests.Support.CatalogueDescriptions;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// LevelCatalogue.Load (design section 5): over the texts the game ships it gives Ready, the Example first with the par
    /// the engine's solver finds, then Level 1 to N in the order presentation.json lists them, each with its slot, the par
    /// that file gives and the level its own text loads to. Over fakes it gives Broken with the first problem of each source
    /// that has one, the presentation file's first, then the Example's, then the listed levels' in the file's order, and it
    /// gives every broken text as a problem without throwing. The expected values come from the files themselves, read
    /// apart from the engine (System.Text.Json for presentation.json), and every count is asserted.
    /// </summary>
    public sealed class LevelCatalogueTests
    {
        private const string Level0003 = "campaign-jam-0003";
        private const string Level0071 = "campaign-jam-0071";
        private const string Level0083 = "campaign-jam-0083";
        private const string Listed0003 = "Listed(campaign-jam-0003, slot 3)";

        /// <summary>The par of the Example that the engine's solver finds for the brief's 6x6 board (design section 7).</summary>
        private const int ExamplePeakPar = 3;

        public static TheoryData<string> BrokenLevelTextCases => BrokenLevelTexts.CaseNames;

        public static TheoryData<string> BrokenPresentationCases => BrokenPresentations.CaseNames;

        [Fact]
        public void The_shipped_texts_give_the_Example_with_par_3_then_Level_1_to_N_in_slot_order_each_with_the_par_its_file_gives()
        {
            IReadOnlyList<FileLevel> file = PresentationFileLevels();

            ReadOnlyArray<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new DiskLevelTexts()));

            IEnumerable<string> expected = new[] { "Example, no slot, par " + Number(ExamplePeakPar) }
                .Concat(file.Select((level, position) => "Level " + Number(position + 1) + ", slot " + Number(level.Slot) + ", par " + Number(level.PeakPar)));
            Assert.NotEmpty(file);
            Assert.Equal(file.Count + 1, entries.Count);
            Assert.Equal(expected, entries.Select(Entry));
            // The file lists its levels in strictly ascending slots, so its order is slot order; and it skips slots (the
            // engine's gen rejected some), so a level's number, its position, differs from its slot after the first gap.
            Assert.True(file.Zip(file.Skip(1)).All(pair => pair.First.Slot < pair.Second.Slot), "presentation.json lists its slots out of order.");
            Assert.Contains(file.Select((level, position) => level.Slot != position), differs => differs);
        }

        [Fact]
        public void Each_entry_holds_the_level_its_own_text_loads_to()
        {
            IReadOnlyList<FileLevel> file = PresentationFileLevels();
            IReadOnlyList<string> textNames = new[] { ShippedLevels.ExampleName }.Concat(file.Select(level => level.Id)).ToList();

            ReadOnlyArray<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new DiskLevelTexts()));

            Assert.NotEmpty(file);
            Assert.Equal(textNames.Count, entries.Count);
            var differing = new List<string>();
            for (int index = 0; index < textNames.Count; index++)
            {
                if (LotText.Write(entries[index].Level) != ShippedLevels.Text(textNames[index]))
                {
                    differing.Add(textNames[index]);
                }
            }

            Assert.True(differing.Count == 0, "These entries hold a level that does not write back to their text: " + string.Join(", ", differing) + ".");
        }

        /// <summary>
        /// A listed level's par is the peakPar its presentation entry gives. Every shipped level's peakPar is also its bays less
        /// its plan's minFreeCapacity, so the shipped texts cannot tell the two apart; this pack gives campaign-jam-0000 a
        /// peakPar of 4 where those give 3, which the engine's reader does not cross-check.
        /// </summary>
        [Fact]
        public void A_listed_level_s_par_is_the_peakPar_of_its_presentation_entry()
        {
            LotLevel level0000 = Loaded(ShippedLevels.Text("campaign-jam-0000"));
            PresentationLevel listed = Assert.Single(
                Assert.IsType<PresentationReadResult.Read>(PresentationJson.Read(FakePresentations.JamPackOfLevel0000WithPeakPar4)).File.Levels);

            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithPresentation(FakePresentations.JamPackOfLevel0000WithPeakPar4));

            // The premise, from the engine: the level's bays less its entry's minFreeCapacity give 3, not the 4 its peakPar gives.
            Assert.Equal(3, level0000.Bays.AsCapacity.Value - listed.Grade.Plan.MinFreeCapacity.Value);
            Assert.Equal(new[] { "Example, no slot, par " + Number(ExamplePeakPar), "Level 1, slot 0, par 4" }, EntriesOf(result).Select(Entry));
        }

        [Fact]
        public void A_level_number_is_1_or_more()
        {
            Assert.Equal(1, new LevelNumber(1).Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelNumber(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelNumber(-1));
        }

        [Fact]
        public void The_catalogue_asks_for_the_presentation_then_the_Example_then_each_listed_level_by_its_Resources_path_and_file_name()
        {
            IReadOnlyList<FileLevel> file = PresentationFileLevels();
            var texts = new RecordingLevelTexts(new DiskLevelTexts());

            EntriesOf(LevelCatalogue.Load(texts));

            IEnumerable<string> expected = new[]
            {
                "Presentation: Levels/presentation, presentation.json",
                "Example: Levels/six-by-six, six-by-six.txt",
            }.Concat(file.Select(level => "Level " + level.Id + ": Levels/" + level.Id + ", " + level.Id + ".txt"));
            Assert.NotEmpty(file);
            Assert.Equal(expected, texts.Asked.Select(Asked));
        }

        [Fact]
        public void A_missing_level_text_gives_Broken_naming_that_level_and_its_slot()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithoutLevel(FakeLevelTexts.Id(Level0003)));

            Assert.Equal(new[] { "LevelTextMissing(" + Listed0003 + ")" }, ProblemsOf(result));
        }

        [Fact]
        public void A_level_text_with_carriage_returns_is_malformed_at_line_1_with_a_wrong_header()
        {
            FakeLevelTexts shipped = FakeLevelTexts.Shipped();
            LevelId level = FakeLevelTexts.Id(Level0003);

            CatalogueResult result = LevelCatalogue.Load(shipped.WithLevel(level, shipped.LevelText(level).Replace("\n", "\r\n", StringComparison.Ordinal)));

            Assert.Equal(new[] { "LevelTextMalformed(" + Listed0003 + ", 1, WrongHeader)" }, ProblemsOf(result));
        }

        [Fact]
        public void A_level_text_ending_in_a_newline_is_malformed_at_line_9_with_trailing_text()
        {
            FakeLevelTexts shipped = FakeLevelTexts.Shipped();
            LevelId level = FakeLevelTexts.Id(Level0003);

            CatalogueResult result = LevelCatalogue.Load(shipped.WithLevel(level, shipped.LevelText(level) + "\n"));

            Assert.Equal(new[] { "LevelTextMalformed(" + Listed0003 + ", 9, TrailingText)" }, ProblemsOf(result));
        }

        [Fact]
        public void A_level_whose_colour_totals_are_no_multiples_of_three_is_rejected()
        {
            FakeLevelTexts shipped = FakeLevelTexts.Shipped();
            LevelId level = FakeLevelTexts.Id(Level0003);

            CatalogueResult result = LevelCatalogue.Load(shipped.WithLevel(level, WithFirstVehicleRecoloured(shipped.LevelText(level))));

            Assert.Equal(new[] { "LevelRejected(" + Listed0003 + ", ColourTotalNotMultipleOfThree, no piece)" }, ProblemsOf(result));
        }

        [Fact]
        public void An_unreadable_presentation_gives_PresentationUnreadable_and_no_listed_level_is_asked_for()
        {
            var texts = new RecordingLevelTexts(FakeLevelTexts.Shipped().WithPresentation("{\"format\":"));

            CatalogueResult result = LevelCatalogue.Load(texts);

            Assert.Equal(new[] { "PresentationUnreadable" }, ProblemsOf(result));
            Assert.Equal(new[] { "Presentation: Levels/presentation, presentation.json", "Example: Levels/six-by-six, six-by-six.txt" }, texts.Asked.Select(Asked));
        }

        [Fact]
        public void A_missing_presentation_gives_PresentationUnreadable()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithoutPresentation());

            Assert.Equal(new[] { "PresentationUnreadable" }, ProblemsOf(result));
        }

        [Fact]
        public void A_pack_of_another_game_gives_NotAJamPack()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithPresentation(FakePresentations.EmptySortPack));

            Assert.Equal(new[] { "NotAJamPack" }, ProblemsOf(result));
        }

        /// <summary>
        /// The engine's reader refuses a Jam level without a par (the next test), so the only level without a par that the
        /// catalogue meets is one that a pack of another game lists, which it tries all the same.
        /// </summary>
        [Fact]
        public void A_level_without_a_par_gives_PeakParMissing_after_the_problem_of_its_pack_of_another_game()
        {
            FakeLevelTexts texts = FakeLevelTexts.Shipped()
                .WithPresentation(FakePresentations.SortPackOfOneLevel)
                .WithLevel(FakeLevelTexts.Id("campaign-sort-0000"), ShippedLevels.Text(ShippedLevels.ExampleName));

            CatalogueResult result = LevelCatalogue.Load(texts);

            Assert.Equal(new[] { "NotAJamPack", "PeakParMissing(Listed(campaign-sort-0000, slot 0))" }, ProblemsOf(result));
        }

        [Fact]
        public void A_Jam_level_without_a_par_makes_the_presentation_unreadable_as_the_engine_refuses_it()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithPresentation(FakePresentations.JamPackWithALevelWithoutPar));

            Assert.Equal(new[] { "PresentationUnreadable" }, ProblemsOf(result));
        }

        [Fact]
        public void Two_broken_levels_give_two_problems_in_the_file_order()
        {
            FakeLevelTexts shipped = FakeLevelTexts.Shipped();
            LevelId later = FakeLevelTexts.Id(Level0071);

            CatalogueResult result = LevelCatalogue.Load(shipped
                .WithLevel(later, shipped.LevelText(later) + "\n")
                .WithoutLevel(FakeLevelTexts.Id(Level0003)));

            Assert.Equal(
                new[] { "LevelTextMissing(" + Listed0003 + ")", "LevelTextMalformed(Listed(campaign-jam-0071, slot 71), 9, TrailingText)" },
                ProblemsOf(result));
        }

        [Fact]
        public void Problems_come_presentation_first_then_the_Example_then_the_listed_levels()
        {
            FakeLevelTexts texts = FakeLevelTexts.Shipped()
                .WithPresentation(FakePresentations.SortPackOfOneLevel)
                .WithExample(ShippedLevels.Text(ShippedLevels.ExampleName) + "\n");

            CatalogueResult result = LevelCatalogue.Load(texts);

            Assert.Equal(
                new[] { "NotAJamPack", "LevelTextMalformed(Example, 9, TrailingText)", "LevelTextMissing(Listed(campaign-sort-0000, slot 0))" },
                ProblemsOf(result));
        }

        [Fact]
        public void A_missing_Example_gives_LevelTextMissing_for_the_Example_after_an_unreadable_presentation()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithoutPresentation().WithoutExample());

            Assert.Equal(new[] { "PresentationUnreadable", "LevelTextMissing(Example)" }, ProblemsOf(result));
        }

        [Fact]
        public void An_Example_that_no_order_wins_gives_ExamplePeakParNotFound()
        {
            LotLevel unwinnable = Loaded(FakeLevels.Unwinnable);

            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithExample(FakeLevels.Unwinnable));

            // The premise, from the engine itself: the text loads, and with its own bays no order wins.
            Assert.IsType<PeakParResult.Unsolvable>(LotSolver.PeakPar(unwinnable, unwinnable.Bays.AsCapacity, new NodeBudget(1_000_000)));
            Assert.Equal(new[] { "ExamplePeakParNotFound" }, ProblemsOf(result));
        }

        /// <summary>
        /// The Example's par is searched with all its bays and a budget of many nodes. A shipped level as the Example whose
        /// par takes all five of its bays and 5,256 nodes gets that par (the brief's 6x6 board takes three of its four bays and
        /// 14 nodes, so it cannot show either). This pins a budget of at least 5,256 nodes, not the 1,000,000 itself: no
        /// shipped level needs more than 10,000.
        /// </summary>
        [Fact]
        public void An_Example_whose_par_takes_all_its_bays_and_thousands_of_nodes_gets_that_par()
        {
            LotLevel level0083 = Loaded(ShippedLevels.Text(Level0083));

            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithExample(ShippedLevels.Text(Level0083)));

            // The premises, from the engine itself: one bay fewer wins no order, and 1,000 nodes are too few.
            Assert.Equal(5, level0083.Bays.AsCapacity.Value);
            Assert.IsType<PeakParResult.Unsolvable>(LotSolver.PeakPar(level0083, new CapacityCount(4), new NodeBudget(1_000_000)));
            Assert.IsType<PeakParResult.BudgetExceeded>(LotSolver.PeakPar(level0083, level0083.Bays.AsCapacity, new NodeBudget(1_000)));
            Assert.Equal("Example, no slot, par 5", Entry(EntriesOf(result)[0]));
        }

        [Fact]
        public void A_Jam_pack_that_lists_no_level_gives_the_Example_alone()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithPresentation(FakePresentations.EmptyJamPack));

            Assert.Equal(new[] { "Example, no slot, par " + Number(ExamplePeakPar) }, EntriesOf(result).Select(Entry));
        }

        [Fact]
        public void No_text_at_all_gives_PresentationUnreadable_and_LevelTextMissing_for_the_Example()
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Nothing);

            Assert.Equal(new[] { "PresentationUnreadable", "LevelTextMissing(Example)" }, ProblemsOf(result));
        }

        [Theory]
        [MemberData(nameof(BrokenLevelTextCases))]
        public void A_broken_level_text_gives_its_first_problem_as_a_listed_level_and_as_the_Example_without_throwing(string brokenCase)
        {
            BrokenLevelText broken = BrokenLevelTexts.ByCase[brokenCase];

            CatalogueResult asListed = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithLevel(FakeLevelTexts.Id(Level0003), broken.Text));
            CatalogueResult asExample = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithExample(broken.Text));

            Assert.Equal(new[] { broken.Problem.DescribedAt(Listed0003) }, ProblemsOf(asListed));
            Assert.Equal(new[] { broken.Problem.DescribedAt("Example") }, ProblemsOf(asExample));
        }

        [Fact]
        public void The_broken_level_texts_reach_every_LoadProblem_and_every_LotTextProblem_that_LotText_Read_returns()
        {
            IReadOnlyList<ExpectedProblem> expected = BrokenLevelTexts.ByCase.Values.Select(broken => broken.Problem).ToList();

            IEnumerable<LotTextProblem> textProblems = expected.OfType<ExpectedProblem.Malformed>().Select(malformed => malformed.Problem).Distinct();
            IEnumerable<LoadProblem> loadProblems = expected.OfType<ExpectedProblem.Rejected>().Select(rejected => rejected.Problem).Distinct();

            Assert.Equal(Enum.GetValues<LotTextProblem>().Where(problem => problem != BrokenLevelTexts.UnreachableTextProblem).Order(), textProblems.Order());
            Assert.Equal(Enum.GetValues<LoadProblem>().Order(), loadProblems.Order());
        }

        [Theory]
        [MemberData(nameof(BrokenPresentationCases))]
        public void A_broken_presentation_gives_PresentationUnreadable_without_throwing(string brokenCase)
        {
            CatalogueResult result = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithPresentation(BrokenPresentations.ByCase[brokenCase]));

            Assert.Equal(new[] { "PresentationUnreadable" }, ProblemsOf(result));
        }

        /// <summary>"Level campaign-jam-0003: Levels/campaign-jam-0003, campaign-jam-0003.txt": the kind of text, its Resources path and its file name.</summary>
        private static string Asked(LevelTextName name) => name switch
        {
            LevelTextName.Level level => "Level " + level.Id.Value + ": " + level.ResourcesPath + ", " + level.FileName,
            _ => name.GetType().Name + ": " + name.ResourcesPath + ", " + name.FileName,
        };

        /// <summary>The text with its first vehicle in another colour: two colour totals are then no multiple of three.</summary>
        private static string WithFirstVehicleRecoloured(string text)
        {
            const string VehiclesKey = "\nvehicles=";
            int key = text.IndexOf(VehiclesKey, StringComparison.Ordinal);
            Assert.True(key >= 0, "The text has no vehicles line.");
            int colour = key + VehiclesKey.Length;
            char other = text[colour] == 'R' ? 'B' : 'R';
            return text[..colour] + other + text[(colour + 1)..];
        }

        private static LotLevel Loaded(string text)
        {
            LotTextResult.Read read = Assert.IsType<LotTextResult.Read>(LotText.Read(text));
            return Assert.IsType<LoadResult.Loaded>(LotLoader.Load(read.Definition)).Level;
        }

        /// <summary>presentation.json's levels as System.Text.Json reads them, apart from the engine's reader, in the file's order.</summary>
        private static IReadOnlyList<FileLevel> PresentationFileLevels()
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(ShippedLevels.PresentationPath()));
            var levels = new List<FileLevel>();
            foreach (JsonElement level in document.RootElement.GetProperty("levels").EnumerateArray())
            {
                levels.Add(FileLevelOf(level));
            }

            return levels;
        }

        /// <summary>One level of presentation.json; a missing or mistyped field throws and fails the test.</summary>
        private static FileLevel FileLevelOf(JsonElement level) => new()
        {
            Id = level.GetProperty("levelId").GetString() ?? throw new InvalidDataException("A level of presentation.json has a null levelId."),
            Slot = level.GetProperty("slot").GetInt32(),
            PeakPar = level.GetProperty("plan").GetProperty("peakPar").GetInt32(),
        };

        /// <summary>A level as presentation.json lists it: its id, its slot and its plan's peakPar.</summary>
        private sealed record FileLevel
        {
            public required string Id { get; init; }

            public required int Slot { get; init; }

            public required int PeakPar { get; init; }
        }
    }
}
