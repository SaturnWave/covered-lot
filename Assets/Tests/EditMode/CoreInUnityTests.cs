using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoveredLot.Core;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using PuzzleCore.Records;
using PuzzleCore.Rules;
using UnityEngine;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// Core's level catalogue and play session inside Unity's runtime (design section 10.3), under the culture the run
    /// names, as the .NET tests check them on .NET. A test-only host gives LevelCatalogue.Load the texts that Resources
    /// holds, each loaded as a TextAsset by its Resources path and unloaded after reading, and the catalogue gives Ready:
    /// the Example first with par 3, then Level 1 to 98 in the order presentation.json lists them, each with the slot and
    /// the par that file gives. Each level it loaded writes back to its asset's text, and each listed level has its
    /// presentation fingerprint. A level text that Resources lacks gives Broken with Copy's development line for it. On the
    /// catalogue's Example, GameSession gives the known answers that the .NET tests pin: a new session shows vehicles 3
    /// and 7 covered with a peak of 0 and no tap yet; vehicle 5 bumps into vehicle 4 and leaves the state, the vehicles
    /// shown covered, the peak and the outcome as they were; the taps 0,3,4 reach a peak of 3 and leave every bay empty;
    /// the taps 0,3,4,2,8,5,1,6,7 win with a peak of 3 for three stars; and the taps 0,3,1,8 fill the bays and fail, after
    /// which a tap is ignored as the level is over, again with all four as they were. The counts are asserted, so no check
    /// can pass on an empty list.
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

        /// <summary>The vehicles of the Example, ids 0 to 8.</summary>
        private const int ExampleVehicleCount = 9;

        [Test]
        public void The_catalogue_loads_from_Resources_with_the_Example_first_then_each_listed_level_numbered_in_file_order_with_its_slot_and_par()
        {
            IReadOnlyList<PresentationLevel> listed = ListedLevels();

            IReadOnlyList<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()));

            var expected = new List<string> { "Example, no slot, par " + Number(ExamplePeakPar) };
            for (int position = 0; position < listed.Count; position++)
            {
                PresentationLevel level = listed[position];
                expected.Add("Level " + Number(position + 1) + ", slot " + Number(level.Slot.Value) + ", par " + Number(ParOf(level)));
            }

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
            var differing = new List<string>();
            for (int index = 0; index < assets.Count; index++)
            {
                if (LotText.Write(entries[index].Level) != AssetText(assets[index]))
                {
                    differing.Add(assets[index]);
                }
            }

            Assert.That(differing, Is.Empty, "LotText.Write gives another text than the asset for these entries");
        }

        [Test]
        public void Each_listed_level_of_the_catalogue_has_the_fingerprint_its_presentation_entry_gives()
        {
            IReadOnlyList<PresentationLevel> listed = ListedLevels();

            IReadOnlyList<CatalogueEntry> entries = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()));

            Assert.That(listed.Count, Is.EqualTo(ListedLevelCount), "the levels presentation.json lists");
            Assert.That(entries.Count, Is.EqualTo(listed.Count + 1), "the catalogue's entries: the Example and the listed levels");
            var mismatched = new List<string>();
            for (int position = 0; position < listed.Count; position++)
            {
                if (LotText.Fingerprint(entries[position + 1].Level) != listed[position].Fingerprint.Hex)
                {
                    mismatched.Add(listed[position].Level.Value);
                }
            }

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
        public void A_new_session_on_the_Example_shows_vehicles_3_and_7_covered_with_a_peak_of_0_and_no_tap_yet()
        {
            CatalogueEntry example = ExampleEntry();

            var session = new GameSession(example);

            Assert.That(session.Entry, Is.SameAs(example), "the entry the session plays");
            Assert.That(session.State, Is.EqualTo(new JamRules(example.Level).Start), "the state of a new session");
            Assert.That(ShownCovered(session), Is.EqualTo(new[] { 3, 7 }), "the vehicles a new session shows covered");
            Assert.That(session.Peak.Bays.Value, Is.EqualTo(0), "the peak of a new session");
            Assert.That(session.LastTap, Is.InstanceOf<TapFeedback.None>(), "the last tap of a new session");
            Assert.That(session.Outcome, Is.EqualTo(new Outcome.InPlay()), "the outcome of a new session");
        }

        /// <summary>
        /// The session shows neither the attempt's seen covers nor its count of moves, so a tap that leaves the attempt as it
        /// was is checked on what the session shows: the state, whose exited vehicles count the moves, and the vehicles shown
        /// covered, where the seen covers show.
        /// </summary>
        [Test]
        public void Tapping_vehicle_5_first_bumps_it_into_vehicle_4_and_leaves_the_state_the_covers_the_peak_and_the_outcome_as_they_were()
        {
            var session = new GameSession(ExampleEntry());
            LotState start = session.State;

            TapFeedback feedback = session.Tap(new PieceId(5));

            Assert.That(session.LastTap, Is.SameAs(feedback), "the session's last tap and the feedback the tap returned");
            if (!(feedback is TapFeedback.Bumped bumped))
            {
                throw new AssertionException("Tapping vehicle 5 gave " + feedback.GetType().Name + ", not Bumped.");
            }

            Assert.That(bumped.Piece, Is.EqualTo(new PieceId(5)), "the vehicle that bumped");
            Assert.That(bumped.Blocker, Is.EqualTo(new Blocker.Piece(new PieceId(4))), "what vehicle 5 bumped into");
            Assert.That(session.State, Is.EqualTo(start), "the state after the bump");
            Assert.That(session.State.ExitedCount, Is.EqualTo(0), "the vehicles that have left after the bump");
            Assert.That(ShownCovered(session), Is.EqualTo(new[] { 3, 7 }), "the vehicles shown covered after the bump");
            Assert.That(session.Peak.Bays.Value, Is.EqualTo(0), "the peak after the bump");
            Assert.That(session.Outcome, Is.EqualTo(new Outcome.InPlay()), "the outcome after the bump");
        }

        [Test]
        public void The_taps_0_3_4_reach_a_peak_of_3_and_leave_every_bay_empty()
        {
            var session = new GameSession(ExampleEntry());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 4);

            Assert.That(notMoved, Is.Empty, "taps that did not move their vehicle");
            Assert.That(session.Peak.Bays.Value, Is.EqualTo(3), "the peak after the three taps");
            Assert.That(Bays(session.State), Is.EqualTo(new[] { "empty", "empty", "empty", "empty" }), "the bays after the three taps");
            Assert.That(session.Outcome, Is.EqualTo(new Outcome.InPlay()), "the outcome after the three taps");
        }

        [Test]
        public void The_taps_0_3_4_2_8_5_1_6_7_win_with_a_peak_of_3_for_three_stars()
        {
            CatalogueEntry example = ExampleEntry();
            var session = new GameSession(example);

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 4, 2, 8, 5, 1, 6, 7);

            Assert.That(notMoved, Is.Empty, "taps that did not move their vehicle");
            Assert.That(session.Outcome, Is.EqualTo(new Outcome.Won()), "the outcome after the nine taps");
            Assert.That(session.Peak.Bays.Value, Is.EqualTo(3), "the peak after the nine taps");
            Assert.That(example.PeakPar.Bays.Value, Is.EqualTo(ExamplePeakPar), "the Example's par");
            Assert.That(Stars.For(session.Peak, example.PeakPar), Is.EqualTo(StarRating.Three), "the stars of the win");
        }

        [Test]
        public void The_taps_0_3_1_8_fail_with_the_bays_full_and_one_more_tap_is_ignored_as_the_level_is_over()
        {
            CatalogueEntry example = ExampleEntry();
            var session = new GameSession(example);
            var four = new PieceId(4);

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 1, 8);
            Outcome failed = session.Outcome;
            LotState full = session.State;
            int peak = session.Peak.Bays.Value;
            IReadOnlyList<int> covered = ShownCovered(session);
            TapFeedback feedback = session.Tap(four);

            Assert.That(notMoved, Is.Empty, "taps that did not move their vehicle");
            Assert.That(failed, Is.EqualTo(new Outcome.Failed(FailKind.SlotsFull)), "the outcome after the four taps");
            Assert.That(peak, Is.EqualTo(4), "the peak after the four taps");
            Assert.That(covered, Is.EqualTo(new[] { 7 }), "the vehicles shown covered after the four taps");
            // The premise, from the engine: vehicle 4 is still in the lot with nothing on its path, so in play it would leave.
            Assert.That(full.HasExited(four), Is.False, "whether vehicle 4 has left the lot");
            Assert.That(example.Level.FirstBlocker(four, full.Exited).HasValue, Is.False, "whether something stands on vehicle 4's path");
            Assert.That(session.LastTap, Is.SameAs(feedback), "the session's last tap and the feedback the tap returned");
            if (!(feedback is TapFeedback.Ignored ignored))
            {
                throw new AssertionException("Tapping vehicle 4 after the fail gave " + feedback.GetType().Name + ", not Ignored.");
            }

            Assert.That(ignored.Piece, Is.EqualTo(four), "the vehicle of the ignored tap");
            Assert.That(ignored.Reason, Is.EqualTo(IgnoredTap.LevelOver), "why the tap was ignored");
            Assert.That(session.State, Is.EqualTo(full), "the state after the ignored tap");
            Assert.That(ShownCovered(session), Is.EqualTo(covered), "the vehicles shown covered after the ignored tap");
            Assert.That(session.Peak.Bays.Value, Is.EqualTo(peak), "the peak after the ignored tap");
            Assert.That(session.Outcome, Is.EqualTo(new Outcome.Failed(FailKind.SlotsFull)), "the outcome after the ignored tap");
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

        /// <summary>The catalogue's first entry, which must be the Example.</summary>
        private static CatalogueEntry ExampleEntry()
        {
            CatalogueEntry first = EntriesOf(LevelCatalogue.Load(new ResourcesTexts()))[0];
            Assert.That(first.Title, Is.InstanceOf<EntryTitle.Example>(), "the title of the catalogue's first entry");
            return first;
        }

        /// <summary>Taps the vehicles in order; returns each tap that did not move its vehicle, with the feedback it gave.</summary>
        private static IReadOnlyList<string> TapAll(GameSession session, params byte[] pieces)
        {
            var notMoved = new List<string>();
            foreach (byte piece in pieces)
            {
                TapFeedback feedback = session.Tap(new PieceId(piece));
                if (!(feedback is TapFeedback.Moved moved && moved.Piece.Value == piece))
                {
                    notMoved.Add("vehicle " + piece.ToString(CultureInfo.InvariantCulture) + ": " + feedback.GetType().Name);
                }
            }

            return notMoved;
        }

        /// <summary>The ids of the Example's vehicles that the session shows covered, in ascending order.</summary>
        private static IReadOnlyList<int> ShownCovered(GameSession session)
        {
            Assert.That(session.Entry.Level.VehicleCount, Is.EqualTo(ExampleVehicleCount), "the Example's vehicles");
            var covered = new List<int>();
            for (int piece = 0; piece < session.Entry.Level.VehicleCount; piece++)
            {
                if (session.IsShownCovered(new PieceId((byte)piece)))
                {
                    covered.Add(piece);
                }
            }

            return covered;
        }

        /// <summary>Each bay of the state from the left, as "empty" or the colour id waiting in it.</summary>
        private static IReadOnlyList<string> Bays(LotState state)
        {
            var bays = new List<string>();
            for (int bay = 0; bay < state.Capacity.Value; bay++)
            {
                bays.Add(state.ColourIn(new SlotIndex((byte)bay)).TryGet(out ColourId colour)
                    ? "colour " + colour.Value.ToString(CultureInfo.InvariantCulture)
                    : "empty");
            }

            return bays;
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
