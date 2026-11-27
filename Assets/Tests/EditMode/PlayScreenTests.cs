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
    /// The play screen, clicked as a player clicks it (design sections 9 and 10.3), in a panel of the UI Test Framework
    /// that no window shows: 360 x 800 units, a phone's panel, with the game's own theme (CoveredLotTheme.tss: Unity's
    /// default runtime theme, then CoveredLot.uss), under the culture the run names. Each test but the variant's builds the
    /// screens over the catalogue that the Client's ResourcesLevelTexts loads, with the Example open, as GameRoot does, and
    /// lets the panel update after every click. The Example's tiles stand in PieceId order in the tile layer, and its ghosts
    /// in the ghost layer, so tile n is vehicle n's; Level 1, which the win card's Next level opens, has nine vehicles too.
    /// The counts are asserted, so no check passes on an empty list.
    /// </summary>
    [HostileCulture]
    public sealed class PlayScreenTests : UITestFixture
    {
        private const string ThemePath = "Assets/CoveredLot/UI/CoveredLotTheme.tss";
        private const string ExampleAsset = "Levels/six-by-six";

        /// <summary>The Example's last line, its tags line, which has no newline after it.</summary>
        private const string ExampleTagsLine = "clues=Not(3,R)";

        /// <summary>A tags line that is true of the Example's lot, the design's variant board (design section 15).</summary>
        private const string VariantTagsLine = "clues=Not(3,R);Not(3,Y);Ahead(7,Same);Touch(7,Y,2)";

        /// <summary>A Jam pack that lists no level, so the catalogue holds the Example alone.</summary>
        private const string EmptyJamPack = "{\"format\":\"pc-presentation\",\"formatVersion\":1,\"game\":\"Jam\",\"levels\":[],\"packId\":\"campaign-jam\"}";

        /// <summary>The Example's vehicles, ids 0 to 8.</summary>
        private const int ExampleVehicles = 9;

        /// <summary>The panel updates a click needs: the repaint's styles, the layout, and the layout a geometry change makes.</summary>
        private const int UpdatesToSettle = 3;

        private const float SmallestButtonSide = 48f;
        private const float SmallestFontSize = 14f;
        private const float SmallestBadgeFontSize = 12f;

        /// <summary>The ink of CoveredLot.uss, --cl-ink, which the lines and headings show.</summary>
        private const string InkColour = "#1D1D1D";

        /// <summary>The page colour of CoveredLot.uss, --cl-page, which fills the end card.</summary>
        private const string PageColour = "#F4F1EA";

        /// <summary>A bay with neither a letter nor a swatch, as BaysOf describes it.</summary>
        private const string EmptyBay = "empty";

        /// <summary>Level 1's vehicle 3, the yellow one at the left end of row 2, which faces west and drives straight out.</summary>
        private const int LevelOneLeaver = 3;

        private static readonly Vector2 PhonePanel = new Vector2(360, 800);
        private static readonly int[] WinningLine = { 0, 3, 4, 2, 8, 5, 1, 6, 7 };
        private static readonly int[] FailingLine = { 0, 3, 1, 8 };
        private static readonly Swatch[] Swatches = (Swatch[])System.Enum.GetValues(typeof(Swatch));

        /// <summary>The classes of the labels in the ink colour: the title, the status line, the HUD's lines, the headings, the pills and the tags.</summary>
        private static readonly string[] InkClasses =
        {
            StyleNames.Title, StyleNames.Status, StyleNames.HudLine, StyleNames.Heading, StyleNames.Pill, StyleNames.TagLine,
        };

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
        public void Clicking_tile_5_outlines_tile_4_alone_and_says_something_is_in_its_way_until_the_next_tap()
        {
            ScreenHost host = ShowExample();

            Click(host, 5);
            List<VisualElement> outlined = Everything(host, StyleNames.Blocker);
            VisualElement four = TileOf(host, 4);
            string status = StatusOf(host);
            Click(host, 0);

            Assert.That(outlined, Is.EqualTo(new[] { four }), "the elements outlined as the blocker after tapping vehicle 5: tile 4's alone");
            Assert.That(status, Is.EqualTo("Something is in its way."), "the status line after the bump");
            Assert.That(Everything(host, StyleNames.Blocker), Is.Empty, "the elements outlined as a blocker after the next tap, vehicle 0's");
            Assert.That(StatusOf(host), Is.Empty, "the status line after the next tap, vehicle 0's, which lifts no cover and clears no set");
        }

        [Test]
        public void The_winning_line_shows_the_win_card_with_Cleared_3_stars_the_peak_against_the_par_and_its_three_buttons()
        {
            ScreenHost host = ShowExample();

            ClickAll(host, WinningLine);
            VisualElement card = TheShownCard(host);

            Assert.That(ShownElements.LabelTexts(card), Is.EqualTo(new[] { Copy.ClearedHeading, "3 stars", "Most bays used: 3 (par 3)" }), "the win card's lines");
            Assert.That(ShownElements.ButtonTexts(card), Is.EqualTo(new[] { Copy.PlayAgain, Copy.NextLevel, Copy.Levels }), "the win card's buttons");
            Assert.That(ShownTiles(host), Is.Empty, "the tiles shown after the win");
        }

        /// <summary>
        /// Level 1, which the win card's Next level opens, is a plain level: a 5x5 lot of nine vehicles with 5 bays, no
        /// covers and no tags. The play screen grows its bays row from the Example's 4 to 5 and shows no ledger, no tags
        /// list, no badge and no covered tile; vehicle 3, which leaves first, leaves no ghost behind.
        /// </summary>
        [Test]
        public void Next_level_after_the_win_opens_Level_1_a_plain_level_with_5_bays_and_no_ledger_tags_badge_cover_or_ghost()
        {
            ScreenHost host = ShowExample();
            ClickAll(host, WinningLine);

            ClickButton(host, Copy.NextLevel);
            List<string> title = ShownTexts(host, StyleNames.Title);
            int tilesAtTheStart = ShownTiles(host).Count;
            Click(host, LevelOneLeaver);

            Assert.That(title, Is.EqualTo(new[] { "Level 1" }), "the title after Next level");
            Assert.That(tilesAtTheStart, Is.EqualTo(9), "the tiles shown at Level 1's start");
            Assert.That(ShownTiles(host).Count, Is.EqualTo(8), "the tiles shown after vehicle 3 has left");
            Assert.That(BaysOf(host), Is.EqualTo(new[] { Filled("Y", Swatch.Yellow), EmptyBay, EmptyBay, EmptyBay, EmptyBay }), "Level 1's bays after vehicle 3 has left");
            Assert.That(ShownTexts(host, StyleNames.HudLine), Is.EqualTo(new[] { "4 bays free", "Most bays used: 1 (par 3)" }), "Level 1's HUD lines after vehicle 3 has left");
            Assert.That(ShownElements.WithClass(host, StyleNames.Ledger), Is.Empty, "the ledgers shown on a plain level");
            Assert.That(ShownElements.WithClass(host, StyleNames.Tags), Is.Empty, "the tags lists shown on a plain level");
            Assert.That(ShownElements.WithClass(host, StyleNames.Badge), Is.Empty, "the badges shown on a plain level");
            Assert.That(Everything(host, StyleNames.Covered), Is.Empty, "the covered tiles on a plain level, shown or not");
            Assert.That(ShownElements.WithClass(host, StyleNames.Ghost), Is.Empty, "the ghosts shown on a plain level after vehicle 3 has left");
            Assert.That(ShownElements.WithClass(host, StyleNames.Card), Is.Empty, "the end cards shown after Next level");
        }

        /// <summary>
        /// The card is filled with the page colour at full alpha, so nothing under it shows through. A tap under the card
        /// would reach the tile only if a click at its centre picked the tile, not the card.
        /// </summary>
        [Test]
        public void The_taps_0_3_1_8_show_the_opaque_fail_card_over_the_lot_so_a_click_on_a_tile_left_reaches_the_card()
        {
            ScreenHost host = ShowExample();
            Color page = ColourOf(PageColour);

            ClickAll(host, FailingLine);
            VisualElement card = TheShownCard(host);
            IReadOnlyList<VisualElement> tiles = ShownTiles(host);
            var pickedPastTheCard = new List<string>();
            foreach (VisualElement tile in tiles)
            {
                VisualElement? picked = panel.Pick(tile.worldBound.center);
                if (picked is null || (picked != card && !card.Contains(picked)))
                {
                    pickedPastTheCard.Add(LabelOf(tile) + " picks " + (picked is null ? "nothing" : picked.GetType().Name + " " + string.Join(" ", picked.GetClasses())));
                }
            }

            Assert.That(ShownElements.LabelTexts(card), Is.EqualTo(new[] { "All bays are full." }), "the fail card's lines");
            Assert.That(ShownElements.ButtonTexts(card), Is.EqualTo(new[] { Copy.Retry, Copy.Levels }), "the fail card's buttons");
            Assert.That(tiles.Count, Is.EqualTo(5), "the tiles left under the card: vehicles 2, 4, 5, 6 and 7");
            Assert.That(pickedPastTheCard, Is.Empty, "the tiles a click at their centre reaches past the card");
            Assert.That(ColourText(card.resolvedStyle.backgroundColor), Is.EqualTo(ColourText(page)), "the fail card's fill: the page colour " + PageColour + " at full alpha");
        }

        /// <summary>
        /// The play screen keeps its bays and HUD lines from one session to the next and repaints them, so the full bays of
        /// the fail must not outlive it: before Retry they hold B, B, R and Y, after it they are empty again.
        /// </summary>
        [Test]
        public void After_the_fail_Retry_shows_nine_tiles_tile_3_covered_as_q_v_the_ledger_R_1_B_1_Y_0_four_empty_bays_and_no_blocker()
        {
            ScreenHost host = ShowExample();
            ClickAll(host, FailingLine);
            List<string> baysBeforeRetry = BaysOf(host);
            List<string> hudBeforeRetry = ShownTexts(host, StyleNames.HudLine);

            ClickButton(host, Copy.Retry);
            VisualElement three = TileOf(host, 3);

            Assert.That(baysBeforeRetry, Is.EqualTo(new[] { Filled("B", Swatch.Blue), Filled("B", Swatch.Blue), Filled("R", Swatch.Red), Filled("Y", Swatch.Yellow) }), "the bays before Retry");
            Assert.That(hudBeforeRetry, Is.EqualTo(new[] { "0 bays free", "Most bays used: 4 (par 3)" }), "the HUD's lines before Retry");
            Assert.That(BaysOf(host), Is.EqualTo(new[] { EmptyBay, EmptyBay, EmptyBay, EmptyBay }), "the bays after Retry");
            Assert.That(ShownTexts(host, StyleNames.HudLine), Is.EqualTo(new[] { "4 bays free", "Most bays used: 0 (par 3)" }), "the HUD's lines after Retry");
            Assert.That(ShownTiles(host).Count, Is.EqualTo(ExampleVehicles), "the tiles shown after Retry");
            Assert.That(three.ClassListContains(StyleNames.Covered), Is.True, "whether tile 3 is shown covered after Retry");
            Assert.That(LabelOf(three), Is.EqualTo("?v"), "tile 3's label after Retry");
            Assert.That(ShownTexts(host, StyleNames.Pill), Is.EqualTo(new[] { "R 1", "B 1", "Y 0" }), "the ledger's pills after Retry");
            Assert.That(Everything(host, StyleNames.Blocker), Is.Empty, "the elements outlined as a blocker after Retry");
            Assert.That(ShownElements.WithClass(host, StyleNames.Card), Is.Empty, "the end cards shown after Retry");
        }

        /// <summary>
        /// The playtest's P1 bug: a bump's outline survived a retry. A bump never ends a level, so the fail card's Retry can
        /// never follow one; the top bar's Restart can, and it plays the level again through the same Again as Retry and
        /// Play again.
        /// </summary>
        [Test]
        public void A_bump_followed_by_Restart_leaves_no_blocker_outline_and_an_empty_status_line()
        {
            ScreenHost host = ShowExample();
            Click(host, 5);
            IReadOnlyList<int> outlined = TilesWithClass(host, StyleNames.Blocker);

            ClickButton(host, Copy.Restart);

            Assert.That(outlined, Is.EqualTo(new[] { 4 }), "the tiles outlined as the blocker after tapping vehicle 5, before Restart");
            Assert.That(Everything(host, StyleNames.Blocker), Is.Empty, "the elements outlined as a blocker after Restart");
            Assert.That(StatusOf(host), Is.Empty, "the status line after Restart");
            Assert.That(ShownTiles(host).Count, Is.EqualTo(ExampleVehicles), "the tiles shown after Restart");
        }

        [Test]
        public void After_the_taps_0_and_3_the_ghost_of_3_reads_Bv_with_badge_1_and_the_tags_row_remains()
        {
            ScreenHost host = ShowExample();

            ClickAll(host, 0, 3);
            List<VisualElement> ghosts = host.Query(className: StyleNames.Ghost).ToList();
            var shownGhosts = new List<int>();
            for (int piece = 0; piece < ghosts.Count; piece++)
            {
                if (ShownElements.IsShown(ghosts[piece]))
                {
                    shownGhosts.Add(piece);
                }
            }

            List<VisualElement> rows = ShownElements.WithClass(host, StyleNames.TagRow);

            Assert.That(ghosts.Count, Is.EqualTo(ExampleVehicles), "the ghosts the ghost layer holds, one for each vehicle");
            Assert.That(shownGhosts, Is.EqualTo(new[] { 0, 3 }), "the ghosts shown after the taps 0 and 3");
            Assert.That(LabelOf(ghosts[3]), Is.EqualTo("Bv"), "the label of the ghost of vehicle 3");
            Assert.That(ShownTexts(ghosts[3], StyleNames.Badge), Is.EqualTo(new[] { "1" }), "the badge of the ghost of vehicle 3");
            Assert.That(rows.Count, Is.EqualTo(1), "the rows of the tags list after the taps 0 and 3");
            Assert.That(ShownElements.LabelTexts(rows[0]), Is.EqualTo(new[] { "1", "not R" }), "the row of the tags list: its badge, then its tags");
        }

        [Test]
        public void An_entry_made_from_the_variant_text_shows_one_link_from_cell_27_to_cell_24_and_draws_its_arrow()
        {
            ScreenHost host = Show(LevelCatalogue.Load(new VariantTexts(VariantText())));

            AheadLinksElement links = host.Play.Board.Links;
            ReadOnlyArray<AheadLinkPicture> shown = links.Links;

            Assert.That(shown.Count, Is.EqualTo(1), "the links of the variant's one Ahead tag");
            Assert.That(CellOf(shown[0].From), Is.EqualTo("row 4, column 3"), "where the link starts: cell 27, vehicle 7's front cell");
            Assert.That(CellOf(shown[0].To), Is.EqualTo("row 4, column 0"), "where the link ends: cell 24, the first cell of vehicle 6 on vehicle 7's path");
            Assert.That(ShownElements.IsShown(links), Is.True, "whether the links layer is shown");
            Assert.That(links.ArrowsDrawn, Is.EqualTo(1), "the arrows the links layer drew");
        }

        /// <summary>
        /// The screen in play, with the win card and with the fail card, so every button of both cards is measured. What
        /// shows (design section 9) adds up to these counts on the Example: in play, the 2 buttons of the top bar and 26
        /// labels (the title, the status line, the pillar's glyph, 9 tile labels and tile 3's badge, 4 bay letters, the 2
        /// HUD lines, the ledger's heading and 3 pills, and the tags list's heading, badge and line); with the win card, 5
        /// buttons and 29 labels (the 9 ghosts' labels and ghost 3's badge in place of the tiles', and the card's 3 lines);
        /// with the fail card, 4 buttons and 27 labels (5 tiles left, without a badge, the 4 ghosts of the vehicles gone,
        /// with ghost 3's badge, and the card's 1 line).
        /// </summary>
        [Test]
        public void Each_button_is_at_least_48_by_48_and_each_label_at_least_14_or_12_for_a_badge_in_play_and_on_both_cards()
        {
            ScreenHost host = ShowExample();
            Measured inPlay = Measure(host);
            ClickAll(host, WinningLine);
            Measured onTheWinCard = Measure(host);
            ClickButton(host, Copy.PlayAgain);
            ClickAll(host, FailingLine);
            Measured onTheFailCard = Measure(host);

            Assert.That(new[] { inPlay.Buttons, onTheWinCard.Buttons, onTheFailCard.Buttons }, Is.EqualTo(new[] { 2, 5, 4 }), "the buttons shown in play, with the win card and with the fail card");
            Assert.That(new[] { inPlay.Labels, onTheWinCard.Labels, onTheFailCard.Labels }, Is.EqualTo(new[] { 26, 29, 27 }), "the labels shown in play, with the win card and with the fail card");
            Assert.That(inPlay.TooSmall, Is.Empty, "what is too small in play");
            Assert.That(onTheWinCard.TooSmall, Is.Empty, "what is too small with the win card");
            Assert.That(onTheFailCard.TooSmall, Is.Empty, "what is too small with the fail card");
        }

        /// <summary>
        /// Unity compiles its default runtime theme into the editor, so whether that theme colours labels of its own cannot
        /// be read from a file: the labels in the game's theme show whether CoveredLot.uss's ink reaches them.
        /// </summary>
        [Test]
        public void The_title_status_HUD_lines_headings_pills_and_tags_show_the_ink_of_CoveredLot_uss()
        {
            ScreenHost host = ShowExample();
            Color ink = ColourOf(InkColour);

            var inked = new List<Label>();
            foreach (Label label in ShownElements.OfType<Label>(host))
            {
                if (HasAnyClass(label, InkClasses))
                {
                    inked.Add(label);
                }
            }

            var otherColours = new List<string>();
            foreach (Label label in inked)
            {
                if (!SameColour(label.resolvedStyle.color, ink))
                {
                    otherColours.Add("'" + label.text + "' (" + string.Join(" ", label.GetClasses()) + ") is " + ColourText(label.resolvedStyle.color));
                }
            }

            Assert.That(inked.Count, Is.EqualTo(10), "the labels in the ink classes: the title, the status line, 2 HUD lines, 2 headings, 3 pills and 1 row's tags");
            Assert.That(otherColours, Is.Empty, "the labels that do not show " + InkColour);
        }

        private static bool HasAnyClass(VisualElement element, IEnumerable<string> classNames)
        {
            foreach (string className in classNames)
            {
                if (element.ClassListContains(className))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the two colours have the same bytes, so a float's last bit does not count; the order does not matter.</summary>
        private static bool SameColour(Color shown, Color expected)
        {
            Color32 one = shown;
            Color32 other = expected;
            return one.r == other.r && one.g == other.g && one.b == other.b && one.a == other.a;
        }

        /// <summary>The colour the hex text names; a constant that names no colour fails the test here.</summary>
        private static Color ColourOf(string html) =>
            ColorUtility.TryParseHtmlString(html, out Color colour) ? colour : throw new AssertionException(html + " is no colour.");

        private static string ColourText(Color colour)
        {
            Color32 bytes = colour;
            return "RGBA(" + Number(bytes.r) + ", " + Number(bytes.g) + ", " + Number(bytes.b) + ", " + Number(bytes.a) + ")";
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string CellOf(GridCell cell) => "row " + Number(cell.Row.Value) + ", column " + Number(cell.Column.Value);

        /// <summary>The Example's text from Resources with the variant's tags line in place of its own.</summary>
        private static string VariantText()
        {
            TextAsset? asset = Resources.Load<TextAsset>(ExampleAsset);
            if (asset == null)
            {
                throw new AssertionException("Resources holds no text asset at " + ExampleAsset + ".");
            }

            string shipped;
            try
            {
                shipped = asset.text;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }

            if (!shipped.EndsWith("\n" + ExampleTagsLine, System.StringComparison.Ordinal))
            {
                throw new AssertionException(ExampleAsset + " does not end with the line " + ExampleTagsLine + ".");
            }

            return shipped.Substring(0, shipped.Length - ExampleTagsLine.Length) + VariantTagsLine;
        }

        private ScreenHost ShowExample() => Show(LevelCatalogue.Load(new ResourcesLevelTexts()));

        /// <summary>The screens over the catalogue, with its first entry open, in the panel, laid out.</summary>
        private ScreenHost Show(CatalogueResult catalogue)
        {
            var navigator = new Navigator(catalogue);
            navigator.Open(0);
            Assert.That(navigator.Screen, Is.InstanceOf<AppScreen.Playing>(), "the screen after opening the catalogue's first entry");
            var host = new ScreenHost(navigator);
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

        private void Click(ScreenHost host, int piece)
        {
            VisualElement tile = TileOf(host, piece);
            Assert.That(ShownElements.IsShown(tile), Is.True, "whether tile " + Number(piece) + " is shown to be clicked");
            simulate.Click(tile);
            Settle();
        }

        private void ClickAll(ScreenHost host, params int[] pieces)
        {
            foreach (int piece in pieces)
            {
                Click(host, piece);
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

        /// <summary>The tile of the vehicle: the tile layer holds one for each of the Example's vehicles, in PieceId order.</summary>
        private static VisualElement TileOf(ScreenHost host, int piece)
        {
            List<VisualElement> tiles = host.Query(className: StyleNames.Tile).ToList();
            Assert.That(tiles.Count, Is.EqualTo(ExampleVehicles), "the tiles the tile layer holds, one for each vehicle");
            return tiles[piece];
        }

        private static List<VisualElement> ShownTiles(ScreenHost host) => ShownElements.WithClass(host, StyleNames.Tile);

        /// <summary>The vehicles whose tiles have the class, shown or not, in PieceId order.</summary>
        private static IReadOnlyList<int> TilesWithClass(ScreenHost host, string className)
        {
            var pieces = new List<int>();
            for (int piece = 0; piece < ExampleVehicles; piece++)
            {
                if (TileOf(host, piece).ClassListContains(className))
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        /// <summary>Every element under the host with the class, shown or not.</summary>
        private static List<VisualElement> Everything(ScreenHost host, string className) => host.Query(className: className).ToList();

        /// <summary>The one end card shown.</summary>
        private static VisualElement TheShownCard(ScreenHost host)
        {
            List<VisualElement> cards = ShownElements.WithClass(host, StyleNames.Card);
            Assert.That(cards.Count, Is.EqualTo(1), "the end cards shown");
            return cards[0];
        }

        /// <summary>
        /// Each bay of the bays row, in order: one that shows a letter or a swatch as its letter, then its swatch classes,
        /// as Filled writes it, and one that shows neither as EmptyBay. A bay that kept a letter or a swatch from a colour
        /// that has left it shows that here.
        /// </summary>
        private static List<string> BaysOf(ScreenHost host)
        {
            var bays = new List<string>();
            foreach (VisualElement bay in ShownElements.WithClass(host, StyleNames.Bay))
            {
                List<Label> letters = bay.Query<Label>(className: StyleNames.BayLetter).ToList();
                Assert.That(letters.Count, Is.EqualTo(1), "the letters of a bay");
                var shown = new List<string>();
                if (letters[0].text.Length > 0)
                {
                    shown.Add(letters[0].text);
                }

                foreach (Swatch swatch in Swatches)
                {
                    if (bay.ClassListContains(StyleNames.Of(swatch)))
                    {
                        shown.Add(StyleNames.Of(swatch));
                    }
                }

                bays.Add(shown.Count == 0 ? EmptyBay : string.Join(" ", shown));
            }

            return bays;
        }

        /// <summary>A filled bay as BaysOf describes it: its letter, then its swatch's class.</summary>
        private static string Filled(string letter, Swatch swatch) => letter + " " + StyleNames.Of(swatch);

        private static string StatusOf(ScreenHost host)
        {
            List<string> status = ShownTexts(host, StyleNames.Status);
            Assert.That(status.Count, Is.EqualTo(1), "the status lines shown");
            return status[0];
        }

        /// <summary>The label of a tile or a ghost.</summary>
        private static string LabelOf(VisualElement tileOrGhost)
        {
            List<VisualElement> labels = tileOrGhost.Query(className: StyleNames.TileLabel).ToList();
            Assert.That(labels.Count, Is.EqualTo(1), "the labels of a tile or a ghost");
            return labels[0] is Label label ? label.text : throw new AssertionException("A tile's label is a " + labels[0].GetType().Name + ", not a Label.");
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

        /// <summary>The shown buttons and labels, and each that is smaller than design section 6 allows.</summary>
        private static Measured Measure(ScreenHost host)
        {
            List<Button> buttons = ShownElements.OfType<Button>(host);
            List<Label> labels = ShownElements.OfType<Label>(host);
            var tooSmall = new List<string>();
            foreach (Button button in buttons)
            {
                Rect layout = button.layout;
                if (!(layout.width >= SmallestButtonSide && layout.height >= SmallestButtonSide))
                {
                    tooSmall.Add("button '" + button.text + "' is " + Size(layout.width) + " x " + Size(layout.height) + ", under " + Size(SmallestButtonSide));
                }
            }

            foreach (Label label in labels)
            {
                float smallest = label.ClassListContains(StyleNames.Badge) ? SmallestBadgeFontSize : SmallestFontSize;
                if (!(label.resolvedStyle.fontSize >= smallest))
                {
                    tooSmall.Add("label '" + label.text + "' (" + string.Join(" ", label.GetClasses()) + ") has font size " + Size(label.resolvedStyle.fontSize) + ", under " + Size(smallest));
                }
            }

            return new Measured(buttons, labels, tooSmall);
        }

        private static string Size(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>What the size test found shown: how many buttons and labels, and each that is too small.</summary>
        private sealed class Measured
        {
            public Measured(IReadOnlyList<Button> buttons, IReadOnlyList<Label> labels, IReadOnlyList<string> tooSmall)
            {
                Buttons = buttons.Count;
                Labels = labels.Count;
                TooSmall = tooSmall;
            }

            public int Buttons { get; }

            public int Labels { get; }

            public IReadOnlyList<string> TooSmall { get; }
        }

        /// <summary>A pack that lists no level, and the variant text as its Example: a catalogue of one entry.</summary>
        private sealed class VariantTexts : ILevelTexts
        {
            private readonly string _example;

            public VariantTexts(string example) => _example = example;

            public Option<string> Read(LevelTextName name) => name switch
            {
                LevelTextName.Presentation => Option.Some(EmptyJamPack),
                LevelTextName.Example => Option.Some(_example),
                LevelTextName.Level => Option.None<string>(),
                _ => throw new AssertionException("The variant's host has no texts of the kind " + name.GetType().Name + "."),
            };
        }
    }
}
