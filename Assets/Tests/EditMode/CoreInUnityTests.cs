using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Core;
using CoveredLot.Core.Catalogue;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using PuzzleCore.Play;
using PuzzleCore.Records;
using PuzzleCore.Rules;
using UnityEngine;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// Core's level catalogue inside Unity's runtime (design section 10.3), under the culture the run names, as the .NET
    /// tests check it on .NET. A test-only host gives LevelCatalogue.Load the texts that Resources holds, each loaded as a
    /// TextAsset by its Resources path and unloaded after reading, and the catalogue gives Ready: the Example first with
    /// par 3, then Level 1 to 98 in the order presentation.json lists them, each with the slot and the par that file gives.
    /// Each level it loaded writes back to its asset's text, and each listed level has its presentation fingerprint. The
    /// Example's known answers hold through the engine's Attempt in Campaign mode, on the catalogue's Example, until piece 5
    /// moves them onto GameSession. A level text that Resources lacks gives Broken with Copy's development line for it. The
    /// counts are asserted, so no check can pass on an empty list.
    /// </summary>
    [HostileCulture]
    public sealed class CoreInUnityTests
    {
        /// <summary>The folder under Resources that holds the level texts (Assets/Generated/Resources/Levels).</summary>
        private const string LevelsFolder = "Levels";
        private const string PresentationAsset = "presentation";
        private const string ExampleAsset = "six-by-six";

        /// <summary>The levels engine-sync.txt records for the synced commit; a sync that changes the count changes this.</summary>
        private const int ListedLevelCount = 98;

        /// <summary>The par the engine's solver finds for the Example, the brief's 6x6 board, as the .NET tests pin it.</summary>
        private const int ExamplePeakPar = 3;

        [Test]
        public void The_catalogue_loads_from_Resources_with_the_Example_first_then_each_listed_level_numbered_in_file_order_with_its_slot_and_par()
        {
            IReadOnlyList<PresentationLevel> listed = ListedLevels();

            IReadOnlyList<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()));

            var expected = new List<string> { "Example, no slot, par " + Number(ExamplePeakPar) };
            expected.AddRange(listed.Select((level, position) =>
                "Level " + Number(position + 1) + ", slot " + Number(level.Slot.Value) + ", par " + Number(ParOf(level))));
            Assert.That(listed.Count, Is.EqualTo(ListedLevelCount), "the levels presentation.json lists");
            Assert.That(entries.Select(Row).ToList(), Is.EqualTo(expected), "the catalogue's entries");
        }

        [Test]
        public void Each_level_of_the_catalogue_writes_back_to_its_asset_text()
        {
            IReadOnlyList<string> assets = new[] { ExampleAsset }.Concat(ListedLevels().Select(level => level.Level.Value)).ToList();

            IReadOnlyList<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()));

            Assert.That(entries.Count, Is.EqualTo(ListedLevelCount + 1), "the catalogue's entries: the Example and the listed levels");
            Assert.That(assets.Count, Is.EqualTo(entries.Count), "the asset texts to compare");
            IReadOnlyList<string> differing = assets.Where((asset, index) => LotText.Write(entries[index].Level) != AssetText(asset)).ToList();
            Assert.That(differing, Is.Empty, "LotText.Write gives another text than the asset for these entries");
        }

        [Test]
        public void Each_listed_level_of_the_catalogue_has_the_fingerprint_its_presentation_entry_gives()
        {
            IReadOnlyList<PresentationLevel> listed = ListedLevels();

            IReadOnlyList<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()));

            Assert.That(listed.Count, Is.EqualTo(ListedLevelCount), "the levels presentation.json lists");
            Assert.That(entries.Count, Is.EqualTo(listed.Count + 1), "the catalogue's entries: the Example and the listed levels");
            IReadOnlyList<string> mismatched = listed
                .Where((level, position) => LotText.Fingerprint(entries[position + 1].Level) != level.Fingerprint.Hex)
                .Select(level => level.Level.Value)
                .ToList();
            Assert.That(mismatched, Is.Empty, "these levels' fingerprints differ from presentation.json's");
        }

        [Test]
        public void A_level_text_that_Resources_lacks_gives_Broken_with_its_development_line()
        {
            PresentationLevel hidden = ListedLevels()[3];

            CatalogueResult result = LevelCatalogue.Load(new ResourcesTextsWithout(hidden.Level));

            Assert.That(hidden.Slot.Value, Is.EqualTo(3), "the slot of the fourth level presentation.json lists");
            if (!(result is CatalogueResult.Broken broken))
            {
                throw new AssertionException("The catalogue loaded without the text of " + hidden.Level.Value + ".");
            }

            Assert.That(broken.Problems.Select(Copy.ProblemLine).ToList(), Is.EqualTo(new[] { "slot 0003: text missing" }), "the development lines");
        }

        [Test]
        public void Tapping_vehicle_5_first_is_rejected_as_blocked_by_vehicle_4_and_changes_nothing()
        {
            JamRules rules = ExampleRules();
            Attempt<LotState, Exit, ExitRejection> attempt = CampaignAttempt(rules);

            StepResult<ExitRejection> result = attempt.Move(new Exit(new PieceId(5)));

            var blockedByFour = new StepResult<ExitRejection>.Illegal(new ExitRejection.Blocked(new Blocker.Piece(new PieceId(4))));
            Assert.That(result, Is.EqualTo(blockedByFour), "the result of tapping vehicle 5");
            Assert.That(attempt.Current, Is.EqualTo(rules.Start), "the state after the rejected tap");
            Assert.That(attempt.Moves.Value, Is.EqualTo(0), "the moves counted after the rejected tap");
            Assert.That(attempt.Outcome, Is.EqualTo(new Outcome.InPlay()), "the outcome after the rejected tap");
        }

        [Test]
        public void The_taps_0_3_4_2_8_5_1_6_7_win()
        {
            Attempt<LotState, Exit, ExitRejection> attempt = CampaignAttempt(ExampleRules());

            IReadOnlyList<string> notDone = TapAll(attempt, 0, 3, 4, 2, 8, 5, 1, 6, 7);

            Assert.That(notDone, Is.Empty, "taps that did not move their vehicle");
            Assert.That(attempt.Outcome, Is.EqualTo(new Outcome.Won()), "the outcome after the nine taps");
        }

        [Test]
        public void The_taps_0_3_1_8_fail_because_the_bays_are_full()
        {
            Attempt<LotState, Exit, ExitRejection> attempt = CampaignAttempt(ExampleRules());

            IReadOnlyList<string> notDone = TapAll(attempt, 0, 3, 1, 8);

            Assert.That(notDone, Is.Empty, "taps that did not move their vehicle");
            Assert.That(attempt.Outcome, Is.EqualTo(new Outcome.Failed(FailKind.SlotsFull)), "the outcome after the four taps");
        }

        /// <summary>The text of the TextAsset Resources holds under Levels/<paramref name="asset"/>, read apart from the catalogue's host.</summary>
        private static string AssetText(string asset)
        {
            string path = LevelsFolder + "/" + asset;
            TextAsset? loaded = Resources.Load<TextAsset>(path);
            if (loaded == null)
            {
                throw new AssertionException("Resources holds no text asset at " + path + ".");
            }

            try
            {
                return loaded.text;
            }
            finally
            {
                Resources.UnloadAsset(loaded);
            }
        }

        /// <summary>The levels presentation.json lists, read by the engine's reader from the asset, apart from the catalogue.</summary>
        private static IReadOnlyList<PresentationLevel> ListedLevels() =>
            PresentationJson.Read(AssetText(PresentationAsset)) switch
            {
                PresentationReadResult.Read read => read.File.Levels,
                var unreadable => throw new AssertionException(PresentationAsset + " does not read: " + unreadable),
            };

        private static int ParOf(PresentationLevel level) =>
            level.Grade.Plan.PeakPar.TryGet(out CapacityCount par)
                ? par.Value
                : throw new AssertionException(level.Level.Value + " has no par in " + PresentationAsset + ".");

        private static IReadOnlyList<CatalogueEntry> EntriesOf(CatalogueResult result) => result switch
        {
            CatalogueResult.Ready ready => ready.Entries,
            CatalogueResult.Broken broken => throw new AssertionException(
                "The catalogue is broken: " + string.Join("; ", broken.Problems.Select(Copy.ProblemLine)) + "."),
            _ => throw new AssertionException("The catalogue gave a result of the unknown kind " + result.GetType().Name + "."),
        };

        /// <summary>"Example, no slot, par 3", or "Level 1, slot 0, par 3".</summary>
        private static string Row(CatalogueEntry entry) =>
            TitleOf(entry.Title) + ", " + (entry.Slot.TryGet(out LevelIndex slot) ? "slot " + Number(slot.Value) : "no slot")
            + ", par " + Number(entry.PeakPar.Bays.Value);

        private static string TitleOf(EntryTitle title) => title switch
        {
            EntryTitle.Example => "Example",
            EntryTitle.Numbered numbered => "Level " + Number(numbered.Number.Value),
            _ => throw new AssertionException("An entry has a title of the unknown kind " + title.GetType().Name + "."),
        };

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>The rules of the catalogue's first entry, which must be the Example.</summary>
        private static JamRules ExampleRules()
        {
            CatalogueEntry first = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()))[0];
            Assert.That(first.Title, Is.InstanceOf<EntryTitle.Example>(), "the title of the catalogue's first entry");
            return new JamRules(first.Level);
        }

        /// <summary>UnityEngine has a PlayMode of its own (the legacy Animation's), so the engine's is named in full.</summary>
        private static Attempt<LotState, Exit, ExitRejection> CampaignAttempt(JamRules rules) =>
            new Attempt<LotState, Exit, ExitRejection>(rules, rules.Start, PuzzleCore.Records.PlayMode.Campaign);

        /// <summary>Taps the vehicles in order; returns each tap that did not move its vehicle, with what the attempt answered.</summary>
        private static IReadOnlyList<string> TapAll(Attempt<LotState, Exit, ExitRejection> attempt, params byte[] pieces)
        {
            var notDone = new List<string>();
            foreach (byte piece in pieces)
            {
                StepResult<ExitRejection> result = attempt.Move(new Exit(new PieceId(piece)));
                if (!(result is StepResult<ExitRejection>.Done))
                {
                    notDone.Add("vehicle " + piece.ToString(CultureInfo.InvariantCulture) + ": " + result);
                }
            }

            return notDone;
        }

        /// <summary>
        /// The level texts as Unity's Resources hold them, a test-only host such as the Client's will be (piece 11): each text
        /// is loaded as a TextAsset by its Resources path, a missing asset gives None, and the asset is unloaded once its text
        /// is read.
        /// </summary>
        private sealed class ResourcesTexts : ILevelTexts
        {
            public Option<string> Read(LevelTextName name)
            {
                TextAsset? asset = Resources.Load<TextAsset>(name.ResourcesPath);
                if (asset == null)
                {
                    return Option.None<string>();
                }

                try
                {
                    return Option.Some(asset.text);
                }
                finally
                {
                    Resources.UnloadAsset(asset);
                }
            }
        }

        /// <summary>The texts Resources holds, but for the one level's, which this host lacks.</summary>
        private sealed class ResourcesTextsWithout : ILevelTexts
        {
            private readonly ILevelTexts _texts = new ResourcesTexts();
            private readonly LevelId _lacking;

            public ResourcesTextsWithout(LevelId lacking) => _lacking = lacking;

            public Option<string> Read(LevelTextName name) =>
                name is LevelTextName.Level level && level.Id == _lacking ? Option.None<string>() : _texts.Read(name);
        }
    }
}
