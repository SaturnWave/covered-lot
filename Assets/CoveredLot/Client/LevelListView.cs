using System;
using System.Collections.Generic;
using CoveredLot.Core;
using CoveredLot.Core.Development;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The level list (design sections 7 and 9), the screen a catalogue that loaded starts on: the game's title, the Rules
    /// button, and a scrolling column of buttons, the Example's first and then Level 1 to N, each of which plays its entry
    /// with a new session. Development builds also show the culture button, which steps the culture the game runs in. The
    /// entries' buttons are made when the list is first shown, and made again only for a list of another length.
    /// </summary>
    public sealed class LevelListView : VisualElement
    {
        private readonly Navigator _navigator;
        private readonly Action _screenChanged;
        private readonly Button _culture;
        private readonly ScrollView _entries = new ScrollView(ScrollViewMode.Vertical);
        private readonly List<Button> _entryButtons = new List<Button>();

        /// <param name="navigator">The game's screen flow, which the entries' buttons drive.</param>
        /// <param name="screenChanged">What an entry's button does after the navigator has opened its level: repaint the screens.</param>
        /// <param name="showRules">What the Rules button does: show the rules over the screens.</param>
        /// <param name="nextCulture">What the culture button does: set the next culture, then show it with <see cref="ShowCulture"/>.</param>
        /// <param name="build">The kind of build: only a development build shows the culture button.</param>
        public LevelListView(Navigator navigator, Action screenChanged, Action showRules, Action nextCulture, BuildKind build)
        {
            _navigator = navigator;
            _screenChanged = screenChanged;
            AddToClassList(StyleNames.LevelList);
            Label title = Classed.Label(StyleNames.Title);
            title.text = Copy.GameTitle;
            Add(title);
            Add(Classed.Button(Copy.RulesButton, showRules));
            _culture = Classed.Button(Copy.CultureButton(DevCulture.Device), nextCulture);
            _culture.style.display = build == BuildKind.Development ? DisplayStyle.Flex : DisplayStyle.None;
            Add(_culture);
            _entries.AddToClassList(StyleNames.LevelScroll);
            Add(_entries);
        }

        /// <summary>Shows the list's entries, each button titled with its entry's title.</summary>
        public void Show(AppScreen.LevelList list)
        {
            if (_entryButtons.Count == list.Entries.Count)
            {
                return;
            }

            _entries.Clear();
            _entryButtons.Clear();
            for (int index = 0; index < list.Entries.Count; index++)
            {
                int entry = index;
                Button button = Classed.Button(Copy.TitleOf(list.Entries[entry].Title), () => Open(entry));
                button.AddToClassList(StyleNames.LevelButton);
                _entryButtons.Add(button);
                _entries.Add(button);
            }
        }

        /// <summary>Names the culture set now on the culture button: "Culture: device", "Culture: tr-TR" or "Culture: sv-SE".</summary>
        public void ShowCulture(DevCulture culture) => _culture.text = Copy.CultureButton(culture);

        private void Open(int index)
        {
            _navigator.Open(index);
            _screenChanged();
        }
    }
}
