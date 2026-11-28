using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Client;
using CoveredLot.Core;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using PuzzleCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// The level list, the problem screen, the rules opened from the list, and the development line with its culture
    /// button (design sections 7, 9 and 10.3; simple UI piece 13), clicked in a 360 x 800 panel of the UI Test Framework
    /// with the game's own theme, under the culture the run names. The screens start where GameRoot starts them: on the
    /// level list of the catalogue that ResourcesLevelTexts loads, or on the problem screen of a catalogue that did not
    /// load. The panel updates after every click. The counts are asserted, so no check passes on an empty list.
    /// </summary>
    [HostileCulture]
    public sealed class ScreensTests : UITestFixture
    {
        private const string ThemePath = "Assets/CoveredLot/UI/CoveredLotTheme.tss";

        /// <summary>The levels engine-sync.txt records for the synced commit, listed after the Example; a sync that changes the count changes this.</summary>
        private const int ListedLevelCount = 98;

        /// <summary>The panel updates a click needs: the repaint's styles, the layout, and the layout a geometry change makes.</summary>
        private const int UpdatesToSettle = 3;

        /// <summary>What every culture button's text starts with: "Culture: device", "Culture: tr-TR".</summary>
        private const string CultureButtonStart = "Culture: ";

        /// <summary>The Example's vehicle that bumps into vehicle 4 at the start.</summary>
        private const int ExampleBumper = 5;

        private static readonly Vector2 PhonePanel = new Vector2(360, 800);

        /// <summary>Loading the theme is no culture-sensitive work, so it may run here, outside [HostileCulture].</summary>
        [OneTimeSetUp]
        public void UseTheGamesTheme()
        {
            ThemeStyleSheet? theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                throw new AssertionException("There is no theme at " + ThemePath + ".");
            }

            themeStyleSheet = theme;
        }

        [SetUp]
        public void UseAPhonesPanel() => panelSize = PhonePanel;

        [Test]
        public void The_list_shows_the_title_Rules_the_culture_button_and_a_button_for_each_entry_the_Example_first()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            var expected = new List<string> { "Example" };
            for (int level = 1; level <= ListedLevelCount; level++)
            {
                expected.Add("Level " + Number(level));
            }

            Assert.That(ShownElements.WithClass(host, StyleNames.LevelList).Count, Is.EqualTo(1), "the level lists shown at the start");
            Assert.That(ShownElements.WithClass(host, StyleNames.Play), Is.Empty, "the play screens shown at the start");
            Assert.That(ShownTexts(host, StyleNames.Title), Is.EqualTo(new[] { Copy.GameTitle }), "the titles shown");
            Assert.That(OtherButtons(host), Is.EqualTo(new[] { Copy.RulesButton, "Culture: device" }), "the list's buttons other than the entries'");
            Assert.That(EntryButtons(host), Is.EqualTo(expected), "the entries' buttons");
        }

        [Test]
        public void Level_1_opens_slot_0_and_the_development_line_names_slot_0000()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);

            ClickButton(host, "Level 1");

            Assert.That(ShownTexts(host, StyleNames.Title), Is.EqualTo(new[] { "Level 1", }), "the titles shown after Level 1");
            Assert.That(DevLineOf(host), Does.StartWith("slot 0000 | par "), "the development line in Level 1");
            Assert.That(DevLineOf(host), Does.Match(@" \| cell [1-9][0-9]*$"), "the development line in Level 1, which ends at the cells' side once the board is laid out");
        }

        [Test]
        public void Levels_on_the_top_bar_returns_to_the_list()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            ClickButton(host, "Example");
            int playScreensInTheLevel = ShownElements.WithClass(host, StyleNames.Play).Count;

            ClickButton(host, Copy.Levels);

            Assert.That(playScreensInTheLevel, Is.EqualTo(1), "the premise: the play screens shown in the Example");
            Assert.That(ShownElements.WithClass(host, StyleNames.LevelList).Count, Is.EqualTo(1), "the level lists shown after Levels");
            Assert.That(ShownElements.WithClass(host, StyleNames.Play), Is.Empty, "the play screens shown after Levels");
            Assert.That(DevLineOf(host), Does.StartWith(CultureInfo.CurrentCulture.Name + " | "), "the development line on the list, which starts at the culture");
        }

        [Test]
        public void Opening_the_Example_again_after_a_bump_shows_no_blocker_outline()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            ClickButton(host, "Example");
            ClickTile(host, ExampleBumper);
            int outlinedAfterTheBump = host.Query(className: StyleNames.Blocker).ToList().Count;

            ClickButton(host, Copy.Levels);
            ClickButton(host, "Example");

            Assert.That(outlinedAfterTheBump, Is.EqualTo(1), "the premise: the elements outlined as the blocker after the bump");
            Assert.That(host.Query(className: StyleNames.Blocker).ToList(), Is.Empty, "the elements outlined as a blocker in the Example opened again");
        }

        /// <summary>The catalogue reports one problem per source in its reading order: the presentation file, then the Example.</summary>
        [Test]
        public void A_catalogue_that_did_not_load_shows_the_problem_screen_with_a_development_line_for_each_problem()
        {
            ScreenHost host = Show(LevelCatalogue.Load(new NoLevelTexts()), BuildKind.Development);

            Assert.That(ShownElements.WithClass(host, StyleNames.Problems).Count, Is.EqualTo(1), "the problem screens shown");
            Assert.That(ShownElements.WithClass(host, StyleNames.LevelList), Is.Empty, "the level lists shown");
            Assert.That(ShownTexts(host, StyleNames.Message), Is.EqualTo(new[] { Copy.LevelsNotLoaded }), "the problem screen's sentence");
            Assert.That(ShownTexts(host, StyleNames.ProblemLine), Is.EqualTo(new[] { "presentation: missing or unreadable", "example: text missing" }),
                "the problem screen's development lines");
        }

        [Test]
        public void A_release_build_shows_no_development_line_no_culture_button_and_no_problem_lines()
        {
            ScreenHost list = ShowShippedCatalogue(BuildKind.Release);
            List<string> listButtons = OtherButtons(list);
            List<VisualElement> listDevLines = ShownElements.WithClass(list, StyleNames.DevLine);
            list.RemoveFromHierarchy();

            ScreenHost problems = Show(LevelCatalogue.Load(new NoLevelTexts()), BuildKind.Release);

            Assert.That(listButtons, Is.EqualTo(new[] { Copy.RulesButton }), "the list's buttons other than the entries' in a release build");
            Assert.That(listDevLines, Is.Empty, "the development lines on the list of a release build");
            Assert.That(ShownElements.LabelTexts(problems), Is.EqualTo(new[] { Copy.LevelsNotLoaded }), "the problem screen's lines in a release build");
        }

        /// <summary>
        /// The button sets the current, UI and default-thread cultures together; its third step puts back the four it found,
        /// which [HostileCulture] set to the run's culture before the screens were made.
        /// </summary>
        [Test]
        public void The_culture_button_steps_tr_TR_sv_SE_and_the_device_s_culture_and_the_development_line_names_each()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            string device = CultureInfo.CurrentCulture.Name;
            var buttons = new List<string>();
            var cultures = new List<string>();
            var lines = new List<string>();

            for (int step = 0; step < 3; step++)
            {
                ClickButton(host, CultureButtonOf(host).text);
                buttons.Add(CultureButtonOf(host).text);
                cultures.Add(string.Join(" ", CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name,
                    NameOf(CultureInfo.DefaultThreadCurrentCulture), NameOf(CultureInfo.DefaultThreadCurrentUICulture)));
                lines.Add(DevLineOf(host));
            }

            Assert.That(buttons, Is.EqualTo(new[] { "Culture: tr-TR", "Culture: sv-SE", "Culture: device" }), "the culture button after each click");
            Assert.That(cultures, Is.EqualTo(new[] { "tr-TR tr-TR tr-TR tr-TR", "sv-SE sv-SE sv-SE sv-SE", string.Join(" ", device, device, device, device) }),
                "the current, UI, default-thread and default-thread UI cultures after each click");
            Assert.That(lines[0], Does.StartWith("tr-TR | "), "the development line after the first click");
            Assert.That(lines[1], Does.StartWith("sv-SE | "), "the development line after the second click");
            Assert.That(lines[2], Does.StartWith(device + " | "), "the development line after the third click");
        }

        /// <summary>
        /// A device's four cultures need not agree, and its default-thread cultures are often unset. Each of the four comes
        /// back to its own place, through the device's step and through RestoreCultures, which GameRoot calls at its end.
        /// [HostileCulture] puts the run's cultures back after the test.
        /// </summary>
        [Test]
        public void The_device_s_step_and_RestoreCultures_put_each_of_four_different_cultures_back_in_its_own_place()
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("sv-SE");
            CultureInfo.DefaultThreadCurrentCulture = null;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            const string device = "tr-TR sv-SE none de-DE";
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);

            for (int step = 0; step < 3; step++)
            {
                ClickButton(host, CultureButtonOf(host).text);
            }

            string afterTheDeviceStep = FourCultures();
            ClickButton(host, CultureButtonOf(host).text);
            string afterOneMoreStep = FourCultures();
            host.RestoreCultures();
            Settle();

            Assert.That(afterTheDeviceStep, Is.EqualTo(device), "the current, UI, default-thread and default-thread UI cultures after the device's step");
            Assert.That(afterOneMoreStep, Is.EqualTo("tr-TR tr-TR tr-TR tr-TR"), "the four cultures after the next click");
            Assert.That(FourCultures(), Is.EqualTo(device), "the four cultures after RestoreCultures");
            Assert.That(CultureButtonOf(host).text, Is.EqualTo("Culture: device"), "the culture button after RestoreCultures");
        }

        /// <summary>The current, UI, default-thread and default-thread UI cultures' names, "none" for an unset one.</summary>
        private static string FourCultures() => string.Join(" ", CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name,
            NameOf(CultureInfo.DefaultThreadCurrentCulture), NameOf(CultureInfo.DefaultThreadCurrentUICulture));

        [Test]
        public void Rules_on_the_list_shows_the_rules_and_Back_returns_to_the_list()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);

            ClickButton(host, Copy.RulesButton);
            List<string> lines = ShownTexts(host, StyleNames.RulesLine);
            int listsWithTheRules = ShownElements.WithClass(host, StyleNames.LevelList).Count;
            ClickButton(host, Copy.Back);

            Assert.That(lines, Is.EqualTo(Copy.RulesLines), "the rules' lines");
            Assert.That(listsWithTheRules, Is.EqualTo(0), "the level lists shown with the rules");
            Assert.That(ShownElements.WithClass(host, StyleNames.LevelList).Count, Is.EqualTo(1), "the level lists shown after Back");
            Assert.That(ShownElements.WithClass(host, StyleNames.Rules), Is.Empty, "the rules shown after Back");
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>A culture's name, or "none" for a default-thread culture that is unset.</summary>
        private static string NameOf(CultureInfo? culture) => culture is null ? "none" : culture.Name;

        /// <summary>The texts of the shown entries' buttons, in the list's order.</summary>
        private static List<string> EntryButtons(ScreenHost host)
        {
            var texts = new List<string>();
            foreach (Button button in ShownElements.OfType<Button>(host))
            {
                if (button.ClassListContains(StyleNames.LevelButton))
                {
                    texts.Add(button.text);
                }
            }

            return texts;
        }

        /// <summary>The texts of the shown buttons that are no entry's, in the tree's order.</summary>
        private static List<string> OtherButtons(ScreenHost host)
        {
            var texts = new List<string>();
            foreach (Button button in ShownElements.OfType<Button>(host))
            {
                if (!button.ClassListContains(StyleNames.LevelButton))
                {
                    texts.Add(button.text);
                }
            }

            return texts;
        }

        /// <summary>The one shown button whose text starts as the culture button's does.</summary>
        private static Button CultureButtonOf(ScreenHost host)
        {
            var buttons = new List<Button>();
            foreach (Button button in ShownElements.OfType<Button>(host))
            {
                if (button.text.StartsWith(CultureButtonStart, System.StringComparison.Ordinal))
                {
                    buttons.Add(button);
                }
            }

            Assert.That(buttons.Count, Is.EqualTo(1), "the culture buttons shown");
            return buttons[0];
        }

        private static string DevLineOf(ScreenHost host)
        {
            List<string> lines = ShownTexts(host, StyleNames.DevLine);
            Assert.That(lines.Count, Is.EqualTo(1), "the development lines shown");
            return lines[0];
        }

        /// <summary>The texts of the shown labels with the class under the root.</summary>
        private static List<string> ShownTexts(VisualElement root, string className)
        {
            var texts = new List<string>();
            foreach (Label label in ShownElements.OfType<Label>(root))
            {
                if (label.ClassListContains(className))
                {
                    texts.Add(label.text);
                }
            }

            return texts;
        }

        private ScreenHost ShowShippedCatalogue(BuildKind build) => Show(LevelCatalogue.Load(new ResourcesLevelTexts()), build);

        /// <summary>The screens over the catalogue, where GameRoot starts them, in the panel, laid out.</summary>
        private ScreenHost Show(CatalogueResult catalogue, BuildKind build)
        {
            var host = new ScreenHost(new Navigator(catalogue), build);
            rootVisualElement.Add(host);
            Settle();
            return host;
        }

        private void Settle()
        {
            for (int update = 0; update < UpdatesToSettle; update++)
            {
                simulate.FrameUpdate();
            }
        }

        private void ClickButton(ScreenHost host, string text)
        {
            var buttons = new List<Button>();
            foreach (Button button in ShownElements.OfType<Button>(host))
            {
                if (button.text == text)
                {
                    buttons.Add(button);
                }
            }

            Assert.That(buttons.Count, Is.EqualTo(1), "the buttons shown with the text " + text);
            simulate.Click(buttons[0]);
            Settle();
        }

        /// <summary>Clicks the tile of the vehicle: the tile layer holds one for each vehicle, in PieceId order.</summary>
        private void ClickTile(ScreenHost host, int piece)
        {
            List<VisualElement> tiles = host.Query(className: StyleNames.Tile).ToList();
            Assert.That(tiles.Count, Is.GreaterThan(piece), "the tiles the tile layer holds");
            Assert.That(ShownElements.IsShown(tiles[piece]), Is.True, "whether tile " + Number(piece) + " is shown to be clicked");
            simulate.Click(tiles[piece]);
            Settle();
        }

        /// <summary>A host that holds no text at all, so the catalogue cannot read its presentation file.</summary>
        private sealed class NoLevelTexts : ILevelTexts
        {
            public Option<string> Read(LevelTextName name) => Option.None<string>();
        }
    }
}
