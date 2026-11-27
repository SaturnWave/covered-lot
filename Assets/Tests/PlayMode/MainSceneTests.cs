using System.Collections;
using System.Collections.Generic;
using CoveredLot.Core.Pictures;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CoveredLot.Tests.PlayMode
{
    /// <summary>
    /// The Main scene in Play mode (design section 10.3): it loads by name, as Main is the one scene in the build, and
    /// once it has loaded and run a frame, the Game object's UIDocument is attached to a panel, which it is only with its
    /// panel settings, and shows the play screen of the Example alone, which GameRoot opens at boot until piece 13 starts
    /// on the level list: one play screen, whose top bar's title reads Example, and neither the level list nor the
    /// problem screen. The scene loads in [UnitySetUp], inside [HostileCulture], so GameRoot runs under the test culture
    /// as well.
    /// </summary>
    [HostileCulture]
    public sealed class MainSceneTests
    {
        private const string MainScene = "Main";
        private const string GameObjectName = "Game";

        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;
        }

        [Test]
        public void The_game_objects_document_is_on_a_panel_and_shows_the_play_screen_of_the_Example_alone()
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainScene), "the active scene");
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

            VisualElement root = document.rootVisualElement;
            List<VisualElement> playScreens = ShownElements.WithClass(root, StyleNames.Play);

            Assert.That(root.panel, Is.Not.Null, "the panel of the document's root, which only its panel settings give it");
            Assert.That(playScreens.Count, Is.EqualTo(1), "the play screens shown");
            Assert.That(TitlesOf(playScreens[0]), Is.EqualTo(new[] { "Example" }), "the titles the play screen shows");
            Assert.That(ShownElements.WithClass(root, StyleNames.LevelList), Is.Empty, "the level lists shown");
            Assert.That(ShownElements.WithClass(root, StyleNames.Problems), Is.Empty, "the problem screens shown");
        }

        private static List<string> TitlesOf(VisualElement screen)
        {
            var titles = new List<string>();
            foreach (VisualElement title in ShownElements.WithClass(screen, StyleNames.Title))
            {
                titles.Add(title is Label label ? label.text : title.GetType().Name);
            }

            return titles;
        }
    }
}
