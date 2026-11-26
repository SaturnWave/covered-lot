using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using PuzzleCore.Play;
using PuzzleCore.Rules;
using UnityEngine;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// The engine and the shipped level texts inside Unity's runtime (design section 10.3), as the .NET tests check them on
    /// .NET: presentation.json, every level it lists and the Example load from Resources as text assets, and read and load
    /// through the engine DLLs the game ships; writing each level back gives its asset text exactly; every listed level has
    /// its presentation fingerprint; and the Example's known answers hold through the engine's Attempt in Campaign mode.
    /// The counts are asserted, so the checks cannot pass on an empty list. Core has no catalogue or session yet: pieces 4
    /// and 5 move these checks onto LevelCatalogue and GameSession.
    /// </summary>
    [HostileCulture]
    public sealed class CoreInUnityTests
    {
        /// <summary>The folder under Resources that holds the level texts (Assets/Generated/Resources/Levels).</summary>
        private const string LevelsFolder = "Levels";
        private const string PresentationName = "presentation";
        private const string ExampleName = "six-by-six";

        /// <summary>The levels engine-sync.txt records for the synced commit; a sync that changes the count changes this.</summary>
        private const int ListedLevelCount = 98;

        [Test]
        public void Every_listed_level_and_the_example_load_and_write_back_to_their_asset_text()
        {
            IReadOnlyList<string> names = ListedLevels().Select(level => level.Level.Value).Append(ExampleName).ToList();
            var differing = new List<string>();
            foreach (string name in names)
            {
                string text = AssetText(name);
                if (LotText.Write(Loaded(name, text)) != text)
                {
                    differing.Add(name);
                }
            }

            Assert.That(names.Count, Is.EqualTo(ListedLevelCount + 1), "the listed levels and the Example");
            Assert.That(differing, Is.Empty, "LotText.Write gives another text than the asset for these levels");
        }

        [Test]
        public void Every_listed_level_has_the_fingerprint_its_presentation_entry_gives()
        {
            IReadOnlyList<PresentationLevel> listed = ListedLevels();

            IReadOnlyList<string> mismatched = listed
                .Where(entry => LotText.Fingerprint(Loaded(entry.Level.Value, AssetText(entry.Level.Value))) != entry.Fingerprint.Hex)
                .Select(entry => entry.Level.Value)
                .ToList();

            Assert.That(listed.Count, Is.EqualTo(ListedLevelCount), "the levels presentation.json lists");
            Assert.That(mismatched, Is.Empty, "these levels' fingerprints differ from presentation.json's");
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

        /// <summary>The text of the TextAsset Resources holds under Levels/<paramref name="name"/>, as the game reads it, unloaded after reading.</summary>
        private static string AssetText(string name)
        {
            string path = LevelsFolder + "/" + name;
            TextAsset? asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                throw new AssertionException("Resources holds no text asset at " + path + ".");
            }

            string text = asset.text;
            Resources.UnloadAsset(asset);
            return text;
        }

        private static IReadOnlyList<PresentationLevel> ListedLevels() =>
            PresentationJson.Read(AssetText(PresentationName)) switch
            {
                PresentationReadResult.Read read => read.File.Levels,
                var unreadable => throw new AssertionException(PresentationName + " does not read: " + unreadable),
            };

        private static LotLevel Loaded(string name, string text)
        {
            LotDefinition definition = LotText.Read(text) switch
            {
                LotTextResult.Read read => read.Definition,
                var malformed => throw new AssertionException(name + " is not a pc-lot text: " + malformed),
            };
            return LotLoader.Load(definition) switch
            {
                LoadResult.Loaded loaded => loaded.Level,
                var rejected => throw new AssertionException(name + " is refused by LotLoader: " + rejected),
            };
        }

        private static JamRules ExampleRules() => new JamRules(Loaded(ExampleName, AssetText(ExampleName)));

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
    }
}
