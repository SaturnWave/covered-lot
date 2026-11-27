using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using Xunit;
using Xunit.Sdk;
using static CoveredLot.Core.Tests.Support.PictureDescriptions;
using static CoveredLot.Core.Tests.Support.SessionTaps;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// HudPicture on the Example, the brief's 6x6 board, as the catalogue loads it from the shipped texts (design sections
    /// 8.3 and 9): the known answers of the HUD's lines, bays, ledger, tags list and status. At the start it reads "4 bays
    /// free" and "Most bays used: 0 (par 3)" over four empty bays, the ledger R 1, B 1, Y 0 and the row "1: not R". After
    /// the taps 0 and 3 the ledger reads R 1, B 0, Y 0 and the status "Cover lifted: blue."; after 0, 3 and 4, "Three blue
    /// cleared."; after 0, 3 and 1 the bays hold B, B, R and an empty one, with "1 bay free"; and after 0, 3, 1 and 8, which
    /// fail, they hold B, B, R and Y, with "0 bays free". A bump reads "Something is in its way." and changes nothing else,
    /// and the next tap's status replaces it. Along the winning line the status names each lifted cover and cleared set,
    /// red and yellow included, joined by a space when one tap does both; on the Example with its blue vehicles made green,
    /// pink or orange it names those colours. Level 1, which has no covers, shows no ledger and no tags list, under its
    /// title "Level 1". The tests run under tr-TR and under sv-SE, and the lines are the same under both.
    /// </summary>
    public sealed class HudPictureTests
    {
        [Fact]
        public void At_the_start_the_Example_s_HUD_reads_4_bays_free_and_most_bays_used_0_par_3_with_the_ledger_R_1_B_1_Y_0()
        {
            HudPicture hud = HudPicture.Of(new GameSession(ShippedCatalogue.Example()));

            Assert.Equal("Example", hud.Title);
            Assert.Equal("4 bays free", hud.BaysFreeLine);
            Assert.Equal("Most bays used: 0 (par 3)", hud.PeakLine);
            Assert.Equal(string.Empty, hud.StatusLine);
            Assert.Equal(new[] { "empty", "empty", "empty", "empty" }, hud.Bays.Select(Bay));
            Assert.Equal(new[] { "R 1 Red", "B 1 Blue", "Y 0 Yellow" }, hud.Ledger.Select(Pill));
            Assert.Equal(new[] { "1: not R" }, TagRows(hud));
        }

        [Fact]
        public void After_the_taps_0_and_3_the_ledger_reads_R_1_B_0_Y_0_and_the_status_Cover_lifted_blue()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3);
            HudPicture hud = HudPicture.Of(session);

            Assert.Empty(notMoved);
            Assert.Equal(new[] { "R 1 Red", "B 0 Blue", "Y 0 Yellow" }, hud.Ledger.Select(Pill));
            Assert.Equal("Cover lifted: blue.", hud.StatusLine);
            Assert.Equal(new[] { "B Blue", "B Blue", "empty", "empty" }, hud.Bays.Select(Bay));
            Assert.Equal("2 bays free", hud.BaysFreeLine);
            Assert.Equal("Most bays used: 2 (par 3)", hud.PeakLine);
            Assert.Equal(new[] { "1: not R" }, TagRows(hud));
        }

        [Fact]
        public void After_the_taps_0_3_and_4_the_status_reads_Three_blue_cleared_and_every_bay_is_empty_again()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 4);
            HudPicture hud = HudPicture.Of(session);

            Assert.Empty(notMoved);
            Assert.Equal("Three blue cleared.", hud.StatusLine);
            Assert.Equal(new[] { "empty", "empty", "empty", "empty" }, hud.Bays.Select(Bay));
            Assert.Equal("4 bays free", hud.BaysFreeLine);
            Assert.Equal("Most bays used: 3 (par 3)", hud.PeakLine);
        }

        [Fact]
        public void After_the_taps_0_3_and_1_the_bays_hold_B_B_R_and_an_empty_one_and_1_bay_is_free()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 1);
            HudPicture hud = HudPicture.Of(session);

            Assert.Empty(notMoved);
            Assert.Equal(new[] { "B Blue", "B Blue", "R Red", "empty" }, hud.Bays.Select(Bay));
            Assert.Equal("1 bay free", hud.BaysFreeLine);
            Assert.Equal("Most bays used: 3 (par 3)", hud.PeakLine);
            Assert.Equal(string.Empty, hud.StatusLine);
        }

        /// <summary>The fail with every bay full is the one state with no bay free, and the HUD is still shown beside the fail card.</summary>
        [Fact]
        public void After_the_taps_0_3_1_and_8_the_bays_hold_B_B_R_Y_and_0_bays_are_free()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            IReadOnlyList<string> notMoved = TapAll(session, 0, 3, 1, 8);
            HudPicture hud = HudPicture.Of(session);

            Assert.Empty(notMoved);
            Assert.Equal(new[] { "B Blue", "B Blue", "R Red", "Y Yellow" }, hud.Bays.Select(Bay));
            Assert.Equal("0 bays free", hud.BaysFreeLine);
            Assert.Equal("Most bays used: 4 (par 3)", hud.PeakLine);
        }

        /// <summary>A bump changes nothing but the status; the next tap, here an ignored one on an id the Example lacks, empties it.</summary>
        [Fact]
        public void A_bump_reads_Something_is_in_its_way_and_the_next_tap_s_status_replaces_it()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            HudPicture start = HudPicture.Of(session);
            TapFeedback bump = session.Tap(new PieceId(5));
            HudPicture bumped = HudPicture.Of(session);
            TapFeedback ignored = session.Tap(new PieceId(9));
            HudPicture after = HudPicture.Of(session);

            Assert.IsType<TapFeedback.Bumped>(bump);
            Assert.Equal("Something is in its way.", bumped.StatusLine);
            Assert.Equal(Lines(start).Skip(1), Lines(bumped).Skip(1));
            Assert.IsType<TapFeedback.Ignored>(ignored);
            Assert.Equal(string.Empty, after.StatusLine);
        }

        [Fact]
        public void Along_the_winning_line_the_status_names_each_lifted_cover_and_cleared_set_joined_by_a_space_when_one_tap_does_both()
        {
            var session = new GameSession(ShippedCatalogue.Example());

            var statuses = new List<string>();
            var notMoved = new List<string>();
            foreach (byte piece in new byte[] { 0, 3, 4, 2, 8, 5, 1, 6, 7 })
            {
                notMoved.AddRange(TapAll(session, piece));
                statuses.Add(HudPicture.Of(session).StatusLine);
            }

            Assert.Empty(notMoved);
            Assert.Equal(
                new[]
                {
                    string.Empty, "Cover lifted: blue.", "Three blue cleared.", string.Empty, string.Empty, "Three yellow cleared.",
                    string.Empty, string.Empty, "Cover lifted: red. Three red cleared.",
                },
                statuses);
        }

        /// <summary>
        /// The Example's blue vehicles are 0, 3 and 4, and only their colour changes, so the tag on vehicle 3 (not R) stays
        /// true and the taps 0, 3 and 4 still lift its cover and clear the set.
        /// </summary>
        [Theory]
        [InlineData("G", "green")]
        [InlineData("P", "pink")]
        [InlineData("O", "orange")]
        public void On_the_Example_with_its_blue_vehicles_in_another_colour_the_status_names_that_colour(string letter, string name)
        {
            CatalogueEntry entry = SixBySixVariants.Entry(WithBlueVehiclesIn(letter));
            var session = new GameSession(entry);

            var statuses = new List<string>();
            var notMoved = new List<string>();
            foreach (byte piece in new byte[] { 0, 3, 4 })
            {
                notMoved.AddRange(TapAll(session, piece));
                statuses.Add(HudPicture.Of(session).StatusLine);
            }

            // The premise, from the engine: vehicles 0, 3 and 4 have the colour of the letter, and no other vehicle has it.
            Assert.Equal(new[] { 0, 3, 4 }, VehiclesWithLetter(entry.Level, letter));
            Assert.Empty(notMoved);
            Assert.Equal(new[] { string.Empty, "Cover lifted: " + name + ".", "Three " + name + " cleared." }, statuses);
        }

        /// <summary>The premise, from the engine: the first level presentation.json lists starts with no vehicle covered.</summary>
        [Fact]
        public void Level_1_which_has_no_covers_shows_no_ledger_and_no_tags_list()
        {
            CatalogueEntry levelOne = ShippedCatalogue.Entries()[1];

            HudPicture hud = HudPicture.Of(new GameSession(levelOne));

            Assert.False(levelOne.Level.IsCoveredLevel, "Level 1 is a covered level.");
            Assert.Equal("Level 1", hud.Title);
            Assert.Empty(hud.Ledger);
            Assert.False(hud.Tags.HasValue, "Level 1's HUD has a tags list.");
            Assert.Equal(levelOne.Level.Bays.Value, hud.Bays.Count);
        }

        /// <summary>The rows of the HUD's tags list, "1: not R"; a HUD without a tags list fails the test.</summary>
        private static IReadOnlyList<string> TagRows(HudPicture hud) =>
            hud.Tags.TryGet(out CluePicture? tags) ? tags.Rows.Select(Row).ToList() : throw new XunitException("The HUD has no tags list.");

        /// <summary>Every line of the HUD, the status first: the status, the title, the free bays, the peak, each bay, each pill and each row of the tags list.</summary>
        private static IReadOnlyList<string> Lines(HudPicture hud) =>
            new[] { hud.StatusLine, hud.Title, hud.BaysFreeLine, hud.PeakLine }
                .Concat(hud.Bays.Select(Bay))
                .Concat(hud.Ledger.Select(Pill))
                .Concat(TagRows(hud))
                .ToList();

        /// <summary>
        /// The Example's text with its blue vehicles in the colour of the letter: the vehicles line lists each vehicle as its
        /// colour's letter, a colon and the rest, the first after "vehicles=" and each other after a semicolon.
        /// </summary>
        private static string WithBlueVehiclesIn(string letter) => ShippedLevels.Text(ShippedLevels.ExampleName)
            .Replace("vehicles=B:", "vehicles=" + letter + ":", StringComparison.Ordinal)
            .Replace(";B:", ";" + letter + ":", StringComparison.Ordinal);

        /// <summary>The level's vehicles whose colour has the letter, as the engine's level texts write it (LotText.ColourLetters).</summary>
        private static IReadOnlyList<int> VehiclesWithLetter(LotLevel level, string letter)
        {
            var vehicles = new List<int>();
            foreach (Vehicle vehicle in level.Vehicles)
            {
                if (LotText.ColourLetters.Substring(vehicle.Colour.Value - 1, 1) == letter)
                {
                    vehicles.Add(vehicle.Id.Value);
                }
            }

            return vehicles;
        }
    }
}
