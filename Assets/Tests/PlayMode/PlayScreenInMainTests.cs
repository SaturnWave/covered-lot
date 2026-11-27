using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Core;
using CoveredLot.Core.Pictures;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;

namespace CoveredLot.Tests.PlayMode
{
    /// <summary>
    /// The Example played by clicks in the Main scene in Play mode (design section 10.3), through the UI Test Framework's
    /// RuntimeUITestFixture on the Game object's own UIDocument (SetUIContent), under the culture the run names: the scene
    /// loads in [UnitySetUp], GameRoot starts on the level list, as at boot, and each test opens the Example with the
    /// list's button, as a player does. The Example's tiles stand in PieceId order in the tile layer, so tile n is vehicle
    /// n's.
    /// <para>
    /// The editor's batch-mode screen is 640 x 480 pixels, which the game's panel settings, matched on the width, make a
    /// panel of 360 x 270 units: too low for the play screen's lot, and a runtime panel drops a pointer event below its
    /// bottom edge. So each test gives the document a copy of its panel settings matched on the height instead, a panel
    /// 800 units high, and the teardown gives the document its own panel settings back and destroys the copy.
    /// </para>
    /// </summary>
    [HostileCulture]
    public sealed class PlayScreenInMainTests : RuntimeUITestFixture
    {
        private const string MainScene = "Main";
        private const string GameObjectName = "Game";

        /// <summary>The Example's vehicles, ids 0 to 8.</summary>
        private const int ExampleVehicles = 9;

        /// <summary>The title of the Example's button on the level list.</summary>
        private const string ExampleTitle = "Example";

        /// <summary>The panel updates a click needs: the repaint's styles, the layout, and the layout a geometry change makes.</summary>
        private const int UpdatesToSettle = 3;

        /// <summary>PanelSettings.match for a panel matched on the screen's height.</summary>
        private const float MatchHeight = 1f;

        private static readonly int[] WinningLine = { 0, 3, 4, 2, 8, 5, 1, 6, 7 };

        /// <summary>The empty theme of the fixture's own document, which it makes before Main loads.</summary>
        private ThemeStyleSheet? _fixtureTheme;

        /// <summary>The Game object's document, while a test runs.</summary>
        private UIDocument? _document;

        /// <summary>The document's own panel settings, which the test's copy stands in for, while a test runs.</summary>
        private PanelSettings? _ownPanel;

        /// <summary>The copy matched on the height, while a test runs.</summary>
        private PanelSettings? _tallPanel;

        /// <summary>
        /// The fixture's one-time set-up makes a UIDocument of its own before any scene of the test loads, and takes the
        /// first theme it finds loaded, which a run of this fixture alone may not have; SetUIContent puts Main's document
        /// in its place, so an empty theme serves.
        /// </summary>
        public override void FixtureOneTimeSetUp()
        {
            _fixtureTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            themeStyleSheet = _fixtureTheme;
            base.FixtureOneTimeSetUp();
        }

        public override void FixtureOneTimeTearDown()
        {
            base.FixtureOneTimeTearDown();
            if (_fixtureTheme != null)
            {
                Object.Destroy(_fixtureTheme);
            }
        }

        [UnitySetUp]
        public IEnumerator LoadMainOnATallPanel()
        {
            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            UIDocument document = GameDocument();
            PanelSettings own = document.panelSettings;
            PanelSettings tall = Object.Instantiate(own);
            tall.match = MatchHeight;
            _document = document;
            _ownPanel = own;
            _tallPanel = tall;
            document.panelSettings = tall;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator GiveTheDocumentItsOwnPanelBack()
        {
            if (_document != null && _ownPanel != null)
            {
                _document.panelSettings = _ownPanel;
            }

            if (_tallPanel != null)
            {
                Object.Destroy(_tallPanel);
            }

            _document = null;
            _ownPanel = null;
            _tallPanel = null;
            yield break;
        }

        [UnityTest]
        public IEnumerator From_the_list_the_Example_s_winning_line_shows_the_win_card_and_Play_again_brings_the_covers_back()
        {
            UIDocument document = GameDocument();
            Follow(document);
            Click(TheButton(document, ExampleTitle));

            foreach (int piece in WinningLine)
            {
                Click(TileOf(document, piece));
            }

            List<VisualElement> cards = ShownElements.WithClass(document.rootVisualElement, StyleNames.Card);
            Assert.That(cards.Count, Is.EqualTo(1), "the end cards shown after the winning line");
            Assert.That(ShownElements.LabelTexts(cards[0]), Is.EqualTo(new[] { Copy.ClearedHeading, "3 stars", "Most bays used: 3 (par 3)" }), "the win card's lines");

            Click(TheButton(document, Copy.PlayAgain));

            Assert.That(ShownElements.WithClass(document.rootVisualElement, StyleNames.Card), Is.Empty, "the end cards shown after Play again");
            Assert.That(ShownElements.WithClass(document.rootVisualElement, StyleNames.Tile).Count, Is.EqualTo(ExampleVehicles), "the tiles shown after Play again");
            Assert.That(TilesWithClass(document, StyleNames.Covered), Is.EqualTo(new[] { 3, 7 }), "the tiles shown covered after Play again");
            yield break;
        }

        /// <summary>
        /// UIDocument makes a new root element each time it is enabled. GameRoot keeps the screens it built once, and puts
        /// them into the new root in its next Update, with the session they showed: the taps 0 and 3 stay played.
        /// </summary>
        [UnityTest]
        public IEnumerator After_the_document_is_disabled_and_enabled_again_the_play_screen_shows_the_same_session()
        {
            UIDocument document = GameDocument();
            Follow(document);
            Click(TheButton(document, ExampleTitle));
            Click(TileOf(document, 0));
            Click(TileOf(document, 3));
            IReadOnlyList<string> statusBefore = ShownTexts(document.rootVisualElement, StyleNames.Status);
            VisualElement rootBefore = document.rootVisualElement;

            document.enabled = false;
            yield return null;
            document.enabled = true;
            yield return null;
            Follow(document);

            VisualElement root = document.rootVisualElement;
            Assert.That(statusBefore, Is.EqualTo(new[] { "Cover lifted: blue." }), "the premise: the status line after the taps 0 and 3");
            Assert.That(root, Is.Not.SameAs(rootBefore), "the premise: the document made a new root element when it was enabled again");
            Assert.That(ShownElements.WithClass(root, StyleNames.Play).Count, Is.EqualTo(1), "the play screens shown in the new root");
            Assert.That(ShownTexts(root, StyleNames.Status), Is.EqualTo(new[] { "Cover lifted: blue." }), "the status line: the one tap 3 gave");
            Assert.That(ShownElements.WithClass(root, StyleNames.Tile).Count, Is.EqualTo(ExampleVehicles - 2), "the tiles shown: all but vehicles 0 and 3");
            Assert.That(ShownGhosts(root), Is.EqualTo(new[] { 0, 3 }), "the ghosts shown: vehicles 0 and 3");
        }

        private static UIDocument GameDocument()
        {
            GameObject? game = GameObject.Find(GameObjectName);
            if (game == null)
            {
                throw new AssertionException("The " + MainScene + " scene holds no active object named " + GameObjectName + ".");
            }

            UIDocument? document = game.GetComponent<UIDocument>();
            if (document == null)
            {
                throw new AssertionException("The " + GameObjectName + " object has no UIDocument.");
            }

            return document;
        }

        /// <summary>The tile of the vehicle: the tile layer holds one for each of the Example's vehicles, in PieceId order.</summary>
        private static VisualElement TileOf(UIDocument document, int piece)
        {
            List<VisualElement> tiles = document.rootVisualElement.Query(className: StyleNames.Tile).ToList();
            Assert.That(tiles.Count, Is.EqualTo(ExampleVehicles), "the tiles the tile layer holds, one for each vehicle");
            return tiles[piece];
        }

        /// <summary>The vehicles whose tiles have the class, in PieceId order.</summary>
        private static IReadOnlyList<int> TilesWithClass(UIDocument document, string className)
        {
            var pieces = new List<int>();
            for (int piece = 0; piece < ExampleVehicles; piece++)
            {
                if (TileOf(document, piece).ClassListContains(className))
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        /// <summary>The vehicles whose ghosts are shown: the ghost layer holds one for each vehicle, in PieceId order.</summary>
        private static IReadOnlyList<int> ShownGhosts(VisualElement root)
        {
            List<VisualElement> ghosts = root.Query(className: StyleNames.Ghost).ToList();
            Assert.That(ghosts.Count, Is.EqualTo(ExampleVehicles), "the ghosts the ghost layer holds, one for each vehicle");
            var shown = new List<int>();
            for (int piece = 0; piece < ghosts.Count; piece++)
            {
                if (ShownElements.IsShown(ghosts[piece]))
                {
                    shown.Add(piece);
                }
            }

            return shown;
        }

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

        private static Button TheButton(UIDocument document, string text)
        {
            var buttons = new List<Button>();
            foreach (Button button in ShownElements.OfType<Button>(document.rootVisualElement))
            {
                if (button.text == text)
                {
                    buttons.Add(button);
                }
            }

            Assert.That(buttons.Count, Is.EqualTo(1), "the buttons shown with the text " + text);
            return buttons[0];
        }

        private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>Hooks the fixture to the document's current root and lets the panel lay it out.</summary>
        private void Follow(UIDocument document)
        {
            SetUIContent(document);
            Settle();
        }

        /// <summary>
        /// Clicks the element at its centre, which must lie inside the panel, where the runtime panel delivers a pointer
        /// event, and must pick the element, so no element over it takes the click.
        /// </summary>
        private void Click(VisualElement element)
        {
            Vector2 centre = element.worldBound.center;
            Rect panelBounds = panel.visualTree.worldBound;
            VisualElement? picked = panel.Pick(centre);
            Assert.That(ShownElements.IsShown(element), Is.True, "whether the element to click is shown");
            Assert.That(panelBounds.Contains(centre), Is.True,
                "whether the element's centre, " + Number(centre.x) + ", " + Number(centre.y) + ", lies inside the panel, "
                + Number(panelBounds.width) + " x " + Number(panelBounds.height));
            Assert.That(picked is not null && (picked == element || element.Contains(picked)), Is.True,
                "whether a click at the element's centre picks it: it picks " + (picked is null ? "nothing" : picked.GetType().Name + " " + string.Join(" ", picked.GetClasses())));
            simulate.Click(element);
            Settle();
        }

        private void Settle()
        {
            for (int update = 0; update < UpdatesToSettle; update++)
            {
                simulate.FrameUpdate();
            }
        }
    }
}
