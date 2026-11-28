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
    /// (one test's is 360 x 373, as short as piece 12's phone's inner screen) with the game's own theme, under the culture
    /// the run names. The screens start where GameRoot starts them: on the
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

        /// <summary>The width of the scrolls' bar in CoveredLot.uss.</summary>
        private const float ScrollBarWidth = 4f;

        /// <summary>The least length of the scrolls' thumb in CoveredLot.uss, however many entries the list holds.</summary>
        private const float ThumbLeastLength = 24f;

        /// <summary>How far apart two centres or edges may lie and count as one: the panel lays each edge out on its pixel grid.</summary>
        private const float SameSpot = 0.5f;

        private static readonly Vector2 PhonePanel = new Vector2(360, 800);

        /// <summary>The panel of piece 12's phone's inner screen: as wide as every portrait phone's panel, and about 373 units high.</summary>
        private static readonly Vector2 InnerScreenPanel = new Vector2(360, 373);

        /// <summary>The fill of the scrolls' thumb: CoveredLot.uss's --cl-scroll-thumb, #8F887A.</summary>
        private static readonly Color32 ScrollThumb = new Color32(0x8F, 0x88, 0x7A, 0xFF);

        /// <summary>A button's fill, CoveredLot.uss's --cl-button, #E2DCCF, and its fill while pressed, --cl-button-pressed, #CFC8B8.</summary>
        private static readonly Color32 ButtonFill = new Color32(0xE2, 0xDC, 0xCF, 0xFF);

        private static readonly Color32 PressedButtonFill = new Color32(0xCF, 0xC8, 0xB8, 0xFF);

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

        /// <summary>
        /// Piece 13's phone showed Unity's desktop scroller beside the entries: 24 units wide, with arrows and a grey track,
        /// it pushed their buttons 12 units left of the title. The bar now lies over the scroll's right edge as a thin thumb,
        /// so the entries' buttons centre under the title.
        /// </summary>
        [Test]
        public void The_entries_buttons_centre_under_the_title_and_scroll_under_a_thin_bar_on_the_right_edge()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            float titleCentre = TheShownElement(host, StyleNames.Title).worldBound.center.x;
            List<Button> entries = ShownEntryButtons(host);
            var offCentre = new List<string>();
            foreach (Button entry in entries)
            {
                float centre = entry.worldBound.center.x;
                if (!(Mathf.Abs(centre - titleCentre) <= SameSpot))
                {
                    offCentre.Add(entry.text + " at " + Size(centre));
                }
            }

            Assert.That(entries.Count, Is.EqualTo(ListedLevelCount + 1), "the entries' buttons shown");
            Assert.That(offCentre, Is.Empty, "the centres of the entries' buttons that miss the title's, " + Size(titleCentre));
            AssertAThinBarOnTheRightEdge(TheShownScroll(host, StyleNames.LevelScroll), "the list's");
        }

        /// <summary>
        /// On a display as short as piece 12's phone's inner screen, the rules need more height than they have: their lines
        /// scroll under the same thin bar as the entries, and Back stays in view under them.
        /// </summary>
        [Test]
        public void On_a_display_as_short_as_the_inner_screen_the_rules_scroll_under_the_same_thin_bar_and_Back_stays_in_view()
        {
            panelSize = InnerScreenPanel;
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);

            ClickButton(host, Copy.RulesButton);

            Assert.That(TheShownButton(host, Copy.Back).worldBound.yMax, Is.LessThanOrEqualTo(InnerScreenPanel.y), "the foot of Back against the panel's");
            AssertAThinBarOnTheRightEdge(TheShownScroll(host, StyleNames.RulesScroll), "the rules'");
        }

        /// <summary>
        /// Piece 13's review: the thumb's length is the share of the entries in view, so on a display as short as the inner
        /// screen the 99 entries alone would leave a thumb about 6 units long, and more levels a shorter one. It keeps at
        /// least 24 units.
        /// </summary>
        [Test]
        public void On_a_display_as_short_as_the_inner_screen_the_list_s_thumb_keeps_at_least_24_units()
        {
            panelSize = InnerScreenPanel;
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);

            AssertAThinBarOnTheRightEdge(TheShownScroll(host, StyleNames.LevelScroll), "the list's");
        }

        /// <summary>
        /// Piece 13's review: Unity's theme greys a slider's track, and lightens its thumb, while the pointer is over them or
        /// presses them. On the list's bar the track stays clear and the thumb keeps its fill, so a finger on the bar does
        /// not hide the thumb. A press on the track pages the list, so the thumb is found again before it is pressed.
        /// </summary>
        [Test]
        public void A_pointer_over_or_pressing_the_list_s_bar_keeps_its_track_clear_and_its_thumb_s_fill()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            Scroller bar = TheShownScroll(host, StyleNames.LevelScroll).verticalScroller;
            VisualElement track = bar.Q(className: BaseSlider<float>.trackerUssClassName);
            VisualElement thumb = bar.Q(className: BaseSlider<float>.draggerUssClassName);
            var seen = new List<string>();

            var onTheTrack = new Vector2(bar.worldBound.center.x, thumb.worldBound.yMax + ThumbLeastLength);
            simulate.MouseMove(onTheTrack);
            Settle();
            seen.Add(BarFills("over the track", track, thumb));
            simulate.MouseDown(onTheTrack);
            Settle();
            seen.Add(BarFills("pressing the track", track, thumb));
            simulate.MouseUp(onTheTrack);
            Settle();
            Vector2 onTheThumb = thumb.worldBound.center;
            simulate.MouseMove(onTheThumb);
            Settle();
            seen.Add(BarFills("over the thumb", track, thumb));
            simulate.MouseDown(onTheThumb);
            Settle();
            seen.Add(BarFills("pressing the thumb", track, thumb));
            simulate.MouseUp(onTheThumb);
            Settle();

            string clear = ": track " + ColourText(new Color32(0, 0, 0, 0)) + ", thumb " + ColourText(ScrollThumb);
            Assert.That(seen, Is.EqualTo(new[] { "over the track" + clear, "pressing the track" + clear, "over the thumb" + clear, "pressing the thumb" + clear }),
                "the fills of the list's track and thumb");
        }

        /// <summary>
        /// Piece 13's review: Unity's theme fills a button of its own while the pointer is over it or presses it, and its
        /// rules outranked CoveredLot.uss's, so a pressed button showed the theme's grey. A button keeps --cl-button under
        /// the pointer and shows --cl-button-pressed while it is pressed.
        /// </summary>
        [Test]
        public void A_button_keeps_its_fill_under_the_pointer_and_shows_the_pressed_fill_while_pressed()
        {
            ScreenHost host = ShowShippedCatalogue(BuildKind.Development);
            Button rules = TheShownButton(host, Copy.RulesButton);
            Vector2 centre = rules.worldBound.center;
            var fills = new List<Color32> { rules.resolvedStyle.backgroundColor };

            simulate.MouseMove(centre);
            Settle();
            fills.Add(rules.resolvedStyle.backgroundColor);
            simulate.MouseDown(centre);
            Settle();
            fills.Add(rules.resolvedStyle.backgroundColor);
            simulate.MouseUp(centre);
            Settle();

            Assert.That(fills, Is.EqualTo(new[] { ButtonFill, ButtonFill, PressedButtonFill }), "the Rules button's fill at rest, under the pointer and pressed");
        }

        /// <summary>
        /// The scroll's vertical bar shows, as what it holds is taller than it, and the scroll is at its start: a strip as
        /// wide as CoveredLot.uss makes it, on the scroll's right edge and over what scrolls, which keeps the scroll's whole
        /// width; no arrow button, and a track that is clear and has no border; a slider that fills the strip, and in it a
        /// thumb in --cl-scroll-thumb at the strip's top, at least the thumb's least length and shorter than the strip.
        /// </summary>
        private static void AssertAThinBarOnTheRightEdge(ScrollView scroll, string whose)
        {
            Scroller bar = scroll.verticalScroller;
            Rect strip = bar.worldBound;
            Rect slider = bar.slider.worldBound;
            VisualElement track = bar.Q(className: BaseSlider<float>.trackerUssClassName);
            VisualElement thumb = bar.Q(className: BaseSlider<float>.draggerUssClassName);
            Rect drawn = thumb.worldBound;

            Assert.That(ShownElements.IsShown(bar), Is.True, "whether " + whose + " bar shows");
            Assert.That(strip.width, Is.EqualTo(ScrollBarWidth).Within(SameSpot), whose + " bar's width");
            Assert.That(strip.xMax, Is.EqualTo(scroll.worldBound.xMax).Within(SameSpot), whose + " bar's right edge, against the scroll's");
            Assert.That(scroll.contentViewport.worldBound.width, Is.EqualTo(scroll.worldBound.width).Within(SameSpot), "the width " + whose + " scroll leaves what scrolls, against its own");
            Assert.That(ShownElements.IsShown(bar.lowButton) || ShownElements.IsShown(bar.highButton), Is.False, "whether " + whose + " bar shows an arrow button");
            Assert.That(((Color32)track.resolvedStyle.backgroundColor).a, Is.EqualTo(0), "the alpha of " + whose + " bar's track");
            Assert.That(BorderWidths(track), Is.EqualTo(0f), "the border widths of " + whose + " bar's track");
            Assert.That(SameBounds(slider, strip), Is.True, whose + " bar's slider, " + BoundsText(slider) + ", against the strip, " + BoundsText(strip));
            Assert.That(thumb.resolvedStyle.visibility, Is.EqualTo(Visibility.Visible), "the visibility of " + whose + " thumb");
            Assert.That(drawn.xMin >= strip.xMin - SameSpot && drawn.xMax <= strip.xMax + SameSpot, Is.True,
                whose + " thumb's sides, " + Size(drawn.xMin) + " to " + Size(drawn.xMax) + ", within the strip's, " + Size(strip.xMin) + " to " + Size(strip.xMax));
            Assert.That(drawn.yMin, Is.EqualTo(strip.yMin).Within(SameSpot), whose + " thumb's top with the scroll at its start, against the strip's");
            Assert.That(drawn.height, Is.GreaterThanOrEqualTo(ThumbLeastLength).And.LessThan(strip.height), whose + " thumb's length, against the strip's " + Size(strip.height));
            Assert.That((Color32)thumb.resolvedStyle.backgroundColor, Is.EqualTo(ScrollThumb), whose + " thumb's fill");
        }

        /// <summary>The track's and the thumb's fills at one moment of the pointer test: "over the track: track RGBA(...), thumb RGBA(...)".</summary>
        private static string BarFills(string moment, VisualElement track, VisualElement thumb) =>
            moment + ": track " + ColourText(track.resolvedStyle.backgroundColor) + ", thumb " + ColourText(thumb.resolvedStyle.backgroundColor);

        private static float BorderWidths(VisualElement element) =>
            element.resolvedStyle.borderLeftWidth + element.resolvedStyle.borderRightWidth + element.resolvedStyle.borderTopWidth + element.resolvedStyle.borderBottomWidth;

        /// <summary>Whether the two rectangles' edges lie within SameSpot of each other.</summary>
        private static bool SameBounds(Rect one, Rect other) =>
            Mathf.Abs(one.xMin - other.xMin) <= SameSpot && Mathf.Abs(one.xMax - other.xMax) <= SameSpot
            && Mathf.Abs(one.yMin - other.yMin) <= SameSpot && Mathf.Abs(one.yMax - other.yMax) <= SameSpot;

        private static string BoundsText(Rect bounds) => "x " + Size(bounds.xMin) + " to " + Size(bounds.xMax) + ", y " + Size(bounds.yMin) + " to " + Size(bounds.yMax);

        private static string ColourText(Color32 colour) =>
            "RGBA(" + Number(colour.r) + ", " + Number(colour.g) + ", " + Number(colour.b) + ", " + Number(colour.a) + ")";

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Size(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>A culture's name, or "none" for a default-thread culture that is unset.</summary>
        private static string NameOf(CultureInfo? culture) => culture is null ? "none" : culture.Name;

        /// <summary>The texts of the shown entries' buttons, in the list's order.</summary>
        private static List<string> EntryButtons(ScreenHost host)
        {
            var texts = new List<string>();
            foreach (Button button in ShownEntryButtons(host))
            {
                texts.Add(button.text);
            }

            return texts;
        }

        /// <summary>The shown entries' buttons, in the list's order.</summary>
        private static List<Button> ShownEntryButtons(ScreenHost host)
        {
            var entries = new List<Button>();
            foreach (Button button in ShownElements.OfType<Button>(host))
            {
                if (button.ClassListContains(StyleNames.LevelButton))
                {
                    entries.Add(button);
                }
            }

            return entries;
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

        /// <summary>The one shown element with the class under the root.</summary>
        private static VisualElement TheShownElement(VisualElement root, string className)
        {
            List<VisualElement> shown = ShownElements.WithClass(root, className);
            Assert.That(shown.Count, Is.EqualTo(1), "the elements shown with the class " + className);
            return shown[0];
        }

        /// <summary>The one shown scroll with the class under the root.</summary>
        private static ScrollView TheShownScroll(VisualElement root, string className) =>
            TheShownElement(root, className) as ScrollView ?? throw new AssertionException("The element shown with the class " + className + " is no ScrollView.");

        /// <summary>The one shown button with the text under the host.</summary>
        private static Button TheShownButton(ScreenHost host, string text)
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
            simulate.Click(TheShownButton(host, text));
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
