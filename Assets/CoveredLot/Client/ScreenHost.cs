using System;
using CoveredLot.Core;
using CoveredLot.Core.Development;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The root of the screens (design section 8.5). It holds one of each screen and shows the one the navigator's screen
    /// names, hiding the others: the play screen for a level in play, the level list, or the problem screen. Every button
    /// that changes the screen repaints the host, which shows the new screen and repaints it from the navigator. A Rules
    /// button shows the rules in place of the screen, inside the same safe-area padding, and their Back button brings the
    /// navigator's screen back: the navigator never sees the rules, so a level under them is just as it was. Development
    /// builds also show the development line at the foot of the screens, written again whenever what it names may have
    /// changed: the screen, the board's layout, the culture, or the device's screen (which GameRoot reports); a release
    /// build has neither the line nor the culture switch. The host also takes the padding that keeps the screens inside
    /// the screen's safe area.
    /// </summary>
    public sealed class ScreenHost : VisualElement
    {
        private readonly Navigator _navigator;
        private readonly PlayView _play;
        private readonly LevelListView _list;
        private readonly ProblemView _problems;
        private readonly RulesView _rules;
        private readonly VisualElement[] _screens;
        private readonly Label _devLine = Classed.Label(StyleNames.DevLine, PickingMode.Ignore);

        /// <summary>The culture switch of a development build; None in a release build, which shows no culture button.</summary>
        private readonly Option<DevCultureSwitch> _cultures;

        /// <summary>Whether the rules show in place of the navigator's screen.</summary>
        private bool _rulesShown;

        /// <summary>Builds the screens and shows the one the navigator shows now.</summary>
        /// <param name="navigator">The game's screen flow.</param>
        /// <param name="developmentBuild">Whether the build is a development build: only those show the development line and the culture button.</param>
        public ScreenHost(Navigator navigator, bool developmentBuild)
        {
            _navigator = navigator;
            _cultures = developmentBuild ? Option.Some(new DevCultureSwitch()) : Option.None<DevCultureSwitch>();
            _play = new PlayView(navigator, Repaint, ShowRules, RefreshDevLine);
            _list = new LevelListView(navigator, Repaint, ShowRules, NextCulture, developmentBuild);
            _problems = new ProblemView(developmentBuild);
            _rules = new RulesView(HideRules);
            _screens = new VisualElement[] { _play, _list, _problems, _rules };
            AddToClassList(StyleNames.Screen);
            foreach (VisualElement screen in _screens)
            {
                Add(screen);
            }

            _devLine.style.display = developmentBuild ? DisplayStyle.Flex : DisplayStyle.None;
            Add(_devLine);
            Repaint();
        }

        /// <summary>The play screen, which shows the level in play.</summary>
        public PlayView Play => _play;

        /// <summary>Whether the build is a development build, the only kind that has the culture switch and the development line.</summary>
        private bool IsDevelopmentBuild => _cultures.HasValue;

        /// <summary>Shows the rules while they are open, and otherwise the screen the navigator shows now, repainted; hides the others.</summary>
        public void Repaint()
        {
            if (_rulesShown)
            {
                ShowOnly(_rules);
                RefreshDevLine();
                return;
            }

            switch (_navigator.Screen)
            {
                case AppScreen.Playing playing:
                    ShowOnly(_play);
                    _play.Show(playing.Session);
                    break;
                case AppScreen.LevelList list:
                    ShowOnly(_list);
                    _list.Show(list);
                    break;
                case AppScreen.Problems problems:
                    ShowOnly(_problems);
                    _problems.Show(problems);
                    break;
                default:
                    throw new InvalidOperationException("The screen host has no screen for this kind of app screen.");
            }

            RefreshDevLine();
        }

        /// <summary>Pads the screens so that none reaches outside the screen's safe area.</summary>
        public void Pad(SafeInsets insets)
        {
            style.paddingTop = insets.Top.Value;
            style.paddingLeft = insets.Left.Value;
            style.paddingBottom = insets.Bottom.Value;
            style.paddingRight = insets.Right.Value;
        }

        /// <summary>
        /// Writes the development line from what holds now: the screen, the board's geometry while a level is in play, the
        /// culture's name, and the device's screen. A release build has no line, so this writes nothing.
        /// </summary>
        public void RefreshDevLine()
        {
            if (!IsDevelopmentBuild)
            {
                return;
            }

            Option<BoardGeometry> board = _navigator.Screen is AppScreen.Playing ? _play.Board.Geometry : Option.None<BoardGeometry>();
            ScreenFacts screen = ScreenFacts.Now();
            _devLine.text = Copy.DevLine(new DevFacts(_navigator.Screen, board, DevCultureSwitch.CurrentName(), screen.Size, screen.SafeArea));
        }

        private void ShowOnly(VisualElement shown)
        {
            foreach (VisualElement screen in _screens)
            {
                screen.style.display = screen == shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ShowRules()
        {
            _rulesShown = true;
            Repaint();
        }

        private void HideRules()
        {
            _rulesShown = false;
            Repaint();
        }

        /// <summary>Sets the next culture of the cycle, names it on the culture button and on the development line.</summary>
        private void NextCulture()
        {
            if (_cultures.TryGet(out DevCultureSwitch? cultures))
            {
                cultures.Next();
                _list.ShowCulture(cultures.Current);
                RefreshDevLine();
            }
        }
    }
}
