using System;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The root of the screens (design section 8.5): it holds one of each screen and shows the one the navigator's screen
    /// names, hiding the others: the play screen for a level in play, the level list, or the problem screen. Until piece
    /// 13, the list and the problem screen are interim ones. Every button that changes the screen repaints the host, which
    /// shows the new screen and repaints it from the navigator. The host also takes the padding that keeps the screens
    /// inside the screen's safe area.
    /// </summary>
    public sealed class ScreenHost : VisualElement
    {
        private readonly Navigator _navigator;
        private readonly PlayView _play;
        private readonly InterimLevelListView _list;
        private readonly InterimProblemView _problems = new InterimProblemView();
        private readonly VisualElement[] _screens;

        /// <summary>Builds the screens and shows the one the navigator shows now.</summary>
        public ScreenHost(Navigator navigator)
        {
            _navigator = navigator;
            _play = new PlayView(navigator, Repaint);
            _list = new InterimLevelListView(navigator, Repaint);
            _screens = new VisualElement[] { _play, _list, _problems };
            AddToClassList(StyleNames.Screen);
            foreach (VisualElement screen in _screens)
            {
                Add(screen);
            }

            Repaint();
        }

        /// <summary>The play screen, which shows the level in play.</summary>
        public PlayView Play => _play;

        /// <summary>Shows the screen the navigator shows now, repainted, and hides the others.</summary>
        public void Repaint()
        {
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
                case AppScreen.Problems:
                    ShowOnly(_problems);
                    break;
                default:
                    throw new InvalidOperationException("The screen host has no screen for this kind of app screen.");
            }
        }

        /// <summary>Pads the screens so that none reaches outside the screen's safe area.</summary>
        public void Pad(SafeInsets insets)
        {
            style.paddingTop = insets.Top.Value;
            style.paddingLeft = insets.Left.Value;
            style.paddingBottom = insets.Bottom.Value;
            style.paddingRight = insets.Right.Value;
        }

        private void ShowOnly(VisualElement shown)
        {
            foreach (VisualElement screen in _screens)
            {
                screen.style.display = screen == shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
