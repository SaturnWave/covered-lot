using System.Collections;
using System.Collections.Generic;
using CoveredLot.Core;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CoveredLot.Tests.PlayMode
{
    /// <summary>
    /// The Main scene in Play mode (design section 10.3): it loads by name, as Main is the one scene in the build, and once
    /// it has loaded, the Game object's UIDocument shows the game's title, which GameRoot adds in Awake. The scene loads in
    /// [UnitySetUp], inside [HostileCulture], so GameRoot runs under the test culture as well.
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
        }

        [Test]
        public void The_game_objects_document_holds_one_label_and_it_shows_the_game_title()
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

            List<Label> labels = document.rootVisualElement.Query<Label>().ToList();

            Assert.That(labels.Count, Is.EqualTo(1), "the labels under the document's root");
            Assert.That(labels[0].text, Is.EqualTo(Copy.GameTitle), "the label's text");
        }
    }
}
