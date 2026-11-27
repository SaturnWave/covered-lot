using System.Collections;
using System.Collections.Generic;
using CoveredLot.Core;
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
    /// panel settings, and shows the level list alone, where GameRoot starts from piece 13: one level list, titled with the
    /// game's title, with a button for the Example and one for each listed level, and neither a play screen nor the
    /// problem screen. The scene loads in [UnitySetUp], inside [HostileCulture], so GameRoot runs under the test culture
    /// as well.
    /// </summary>
    [HostileCulture]
    public sealed class MainSceneTests
    {
        private const string MainScene = "Main";
        private const string GameObjectName = "Game";

        /// <summary>The Example and the levels engine-sync.txt records for the synced commit; a sync that changes the count changes this.</summary>
        private const int Entries = 99;

        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;
        }

        [Test]
        public void The_game_objects_document_is_on_a_panel_and_shows_the_level_list_alone()
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
            List<VisualElement> lists = ShownElements.WithClass(root, StyleNames.LevelList);

            Assert.That(root.panel, Is.Not.Null, "the panel of the document's root, which only its panel settings give it");
            Assert.That(lists.Count, Is.EqualTo(1), "the level lists shown");
            Assert.That(TitlesOf(lists[0]), Is.EqualTo(new[] { Copy.GameTitle }), "the titles the level list shows");
            Assert.That(ShownElements.WithClass(lists[0], StyleNames.LevelButton).Count, Is.EqualTo(Entries), "the entries' buttons the level list shows");
            Assert.That(ShownElements.WithClass(root, StyleNames.Play), Is.Empty, "the play screens shown");
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
