using System;
using System.Collections.Generic;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Development;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using Xunit;
using static CoveredLot.Core.Tests.Support.Units;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The development line and the culture button (design section 9), exact, on a 1080x2400 screen whose safe area starts
    /// 63 pixels up and is 2247 high, with the board fitted to the phone frame, 344x420. In Level 25, slot 24 with a par of
    /// 4 on a 7x7 lot, it reads "slot 0024 | par 4 | tr-TR | 1080x2400 | safe 0 63 1080 2247 | cell 47"; the Example names
    /// itself "example"; on the level list the line starts at the culture, before the board's first layout it has no cell,
    /// and an empty culture name, the invariant culture's, reads "invariant". The button cycles the device's culture, tr-TR
    /// and sv-SE, and names each. The tests run under tr-TR and under sv-SE, and the lines are the same under both.
    /// </summary>
    public sealed class DevLineTests
    {
        private static readonly PixelSize Screen1080x2400 = new(PixelsAcross(1080), PixelsUp(2400));

        private static readonly PixelRect SafeArea = new(PixelsAcross(0), PixelsUp(63), new PixelSize(PixelsAcross(1080), PixelsUp(2247)));

        private static readonly PanelSize PhoneFrame = new(Across(344), Down(420));

        [Fact]
        public void In_Level_25_the_line_reads_its_slot_its_par_the_culture_the_screen_the_safe_area_and_the_cell()
        {
            AppScreen.Playing level = Opened(25);
            CatalogueEntry entry = level.Session.Entry;

            string line = Copy.DevLine(new DevFacts(level, BoardGeometry.Fit(PhoneFrame, entry.Level.Size), "tr-TR", Screen1080x2400, SafeArea));

            Assert.Equal("Level 25, slot 24, par 4", CatalogueDescriptions.Entry(entry));
            Assert.Equal("7x7", entry.Level.Size.ToString());
            Assert.Equal("slot 0024 | par 4 | tr-TR | 1080x2400 | safe 0 63 1080 2247 | cell 47", line);
        }

        [Fact]
        public void In_the_Example_the_line_names_the_example_and_its_par()
        {
            AppScreen.Playing example = Opened(0);
            CatalogueEntry entry = example.Session.Entry;

            string line = Copy.DevLine(new DevFacts(example, BoardGeometry.Fit(PhoneFrame, entry.Level.Size), "sv-SE", Screen1080x2400, SafeArea));

            Assert.Equal("Example, no slot, par 3", CatalogueDescriptions.Entry(entry));
            Assert.Equal("example | par 3 | sv-SE | 1080x2400 | safe 0 63 1080 2247 | cell 55", line);
        }

        [Fact]
        public void On_the_level_list_the_line_starts_at_the_culture()
        {
            var navigator = new Navigator(LevelCatalogue.Load(new DiskLevelTexts()));

            string line = Copy.DevLine(new DevFacts(navigator.Screen, Option.None<BoardGeometry>(), "tr-TR", Screen1080x2400, SafeArea));

            Assert.IsType<AppScreen.LevelList>(navigator.Screen);
            Assert.Equal("tr-TR | 1080x2400 | safe 0 63 1080 2247", line);
        }

        [Fact]
        public void Before_the_board_s_first_layout_the_line_has_no_cell_and_an_empty_culture_name_reads_invariant()
        {
            AppScreen.Playing example = Opened(0);

            string line = Copy.DevLine(new DevFacts(example, Option.None<BoardGeometry>(), string.Empty, Screen1080x2400, SafeArea));

            Assert.Equal("example | par 3 | invariant | 1080x2400 | safe 0 63 1080 2247", line);
        }

        /// <summary>Three steps from the device's culture walk the cycle once and come back; each step's button and name are read in turn.</summary>
        [Fact]
        public void The_culture_button_cycles_the_device_s_culture_tr_TR_and_sv_SE_and_names_each()
        {
            var cycle = new List<DevCulture>();
            var buttons = new List<string>();
            var names = new List<Option<string>>();
            DevCulture culture = DevCulture.Device;
            for (int step = 0; step < 3; step++)
            {
                cycle.Add(culture);
                buttons.Add(Copy.CultureButton(culture));
                names.Add(DevCultures.NameOf(culture));
                culture = DevCultures.Next(culture);
            }

            Assert.Equal(Enum.GetValues<DevCulture>(), cycle);
            Assert.Equal(DevCulture.Device, culture);
            Assert.Equal(new[] { "Culture: device", "Culture: tr-TR", "Culture: sv-SE" }, buttons);
            Assert.Equal(new[] { Option.None<string>(), Option.Some("tr-TR"), Option.Some("sv-SE") }, names);
        }

        /// <summary>The level at this index of the shipped catalogue's list, opened by the navigator as the list opens it.</summary>
        private static AppScreen.Playing Opened(int index)
        {
            var navigator = new Navigator(LevelCatalogue.Load(new DiskLevelTexts()));
            navigator.Open(index);
            return Assert.IsType<AppScreen.Playing>(navigator.Screen);
        }
    }
}
