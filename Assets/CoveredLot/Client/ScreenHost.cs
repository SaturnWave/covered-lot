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
    /// The root of the views (design section 8.5). It holds one view of each screen and shows the one the navigator's
    /// screen names, hiding the others: the play view for a level in play, the level list, or the problem view. Every
    /// button that changes the screen repaints the host, which shows the new view and repaints it from the navigator. A
    /// Rules button shows the rules in place of the view, inside the same safe-area padding, and their Back button brings
    /// the navigator's screen back: the navigator never sees the rules, so a level left for them is just as it was. A
    /// development build also shows the development line at the foot of the views, written again whenever what it names
    /// may have changed: the screen, the board's layout, the culture, or the display (which GameRoot reports); a release
    /// build has neither the line nor the culture switch. The host also takes the padding that keeps the views inside the
    /// display's safe area, and puts the device's cultures back when the game ends.
    /// </summary>
    public sealed class ScreenHost : VisualElement
    {
        private readonly Navigator _navigator;
        private readonly BuildKind _build;
        private readonly PlayView _play;
        private readonly LevelListView _list;
        private readonly ProblemView _problems;
        private readonly RulesView _rules;
        private readonly VisualElement[] _views;
        private readonly Label _devLine = Classed.Label(StyleNames.DevLine, PickingMode.Ignore);

        /// <summary>The culture switch of a development build; None in a release build, which shows no culture button.</summary>
        private readonly Option<DevCultureSwitch> _cultures;

        /// <summary>Whether the rules show in place of the navigator's screen.</summary>
        private bool _rulesShown;

        /// <summary>Builds the views and shows the one for the navigator's screen now.</summary>
        /// <param name="navigator">The game's screen flow.</param>
        /// <param name="build">The kind of build: only a development build shows the development line, the culture button and the problems' lines.</param>
        public ScreenHost(Navigator navigator, BuildKind build)
        {
            _navigator = navigator;
            _build = build;
            _cultures = build == BuildKind.Development ? Option.Some(new DevCultureSwitch()) : Option.None<DevCultureSwitch>();
            _play = new PlayView(navigator, screenChanged: Repaint, showRules: ShowRules, boardLaidOut: RefreshDevLine);
            _list = new LevelListView(navigator, screenChanged: Repaint, showRules: ShowRules, nextCulture: NextCulture, build: build);
            _problems = new ProblemView(build);
            _rules = new RulesView(back: HideRules);
            _views = new VisualElement[] { _play, _list, _problems, _rules };
            AddToClassList(StyleNames.Screen);
            foreach (VisualElement view in _views)
            {
                Add(view);
            }

            _devLine.style.display = IsDevelopmentBuild ? DisplayStyle.Flex : DisplayStyle.None;
            Add(_devLine);
            Repaint();
        }

        /// <summary>The play view, which shows the level in play.</summary>
        public PlayView Play => _play;

        private bool IsDevelopmentBuild => _build == BuildKind.Development;

        /// <summary>Shows the rules while they are open, and otherwise the view for the navigator's screen now, repainted; hides the others.</summary>
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
                    throw new InvalidOperationException("The screen host has no view for this kind of app screen.");
            }

            RefreshDevLine();
        }

        /// <summary>Pads the views so that none reaches outside the display's safe area.</summary>
        public void Pad(SafeInsets insets)
        {
            style.paddingTop = insets.Top.Value;
            style.paddingLeft = insets.Left.Value;
            style.paddingBottom = insets.Bottom.Value;
            style.paddingRight = insets.Right.Value;
        }

        /// <summary>
        /// Writes the development line from what holds now: the screen, the board's geometry while a level is in play, the
        /// culture's name, and the display. A release build has no line, so this writes nothing.
        /// </summary>
        public void RefreshDevLine()
        {
            if (!IsDevelopmentBuild)
            {
                return;
            }

            Option<BoardGeometry> board = _navigator.Screen is AppScreen.Playing ? _play.Board.Geometry : Option.None<BoardGeometry>();
            DisplayFacts display = DisplayFacts.Now();
            _devLine.text = Copy.DevLine(new DevFacts(_navigator.Screen, board, DevCultureSwitch.CurrentName(), display.Size, display.SafeArea));
        }

        /// <summary>Puts back the cultures the device had when the views were made; a release build never changed them.</summary>
        public void RestoreCultures()
        {
            if (_cultures.TryGet(out DevCultureSwitch? cultures))
            {
                cultures.Restore();
                _list.ShowCulture(cultures.Current);
            }
        }

        private void ShowOnly(VisualElement shown)
        {
            foreach (VisualElement view in _views)
            {
                view.style.display = view == shown ? DisplayStyle.Flex : DisplayStyle.None;
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
