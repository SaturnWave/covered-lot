using System;
using CoveredLot.Core;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The level list until piece 13 replaces it with the real one: the game's title and one button, titled with the
    /// Example's title, that opens the Example, the catalogue's first entry. The play screen's Levels buttons lead here,
    /// and the button leads back to the Example, the one level the game opens until then.
    /// </summary>
    public sealed class InterimLevelListView : VisualElement
    {
        /// <summary>The Example's index in the list: the catalogue puts it first.</summary>
        private const int ExampleIndex = 0;

        private readonly Navigator _navigator;
        private readonly Action _screenChanged;
        private readonly Button _example;

        /// <param name="navigator">The game's screen flow, which the button drives.</param>
        /// <param name="screenChanged">What the button does after the navigator has opened the Example: repaint the screens.</param>
        public InterimLevelListView(Navigator navigator, Action screenChanged)
        {
            _navigator = navigator;
            _screenChanged = screenChanged;
            AddToClassList(StyleNames.LevelList);
            Label title = Classed.Label(StyleNames.Title);
            title.text = Copy.GameTitle;
            Add(title);
            _example = Classed.Button(string.Empty, OpenExample);
            Add(_example);
        }

        /// <summary>Shows the list: its button takes the title of the list's first entry, the Example.</summary>
        public void Show(AppScreen.LevelList list) => _example.text = Copy.TitleOf(list.Entries[ExampleIndex].Title);

        private void OpenExample()
        {
            _navigator.Open(ExampleIndex);
            _screenChanged();
        }
    }
}
