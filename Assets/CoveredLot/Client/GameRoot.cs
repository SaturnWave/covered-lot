using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Navigation;
using PuzzleCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The game's one MonoBehaviour and its composition root (design sections 6 and 8.5). It sits on the Main scene's Game
    /// object next to the UIDocument and finds that document itself, so it has no serialized reference fields. In Awake it
    /// loads the catalogue once, through ResourcesLevelTexts, makes the navigator, which starts on the level list (or on
    /// the problem screen when the catalogue did not load), and builds the screens once, with the development line and the
    /// culture button only in a development build (Debug.isDebugBuild). It keeps them in the document's root, its
    /// safe-area padding current, the development line told of each change of the display, and the screen awake while a
    /// level is open (<see cref="DocumentScreens"/>). When it is destroyed, the device's cultures go back.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>The screens in the document; null when Awake found no UIDocument, and this component disabled itself.</summary>
        private DocumentScreens? _screens;

        /// <summary>UIDocument runs at execution order -100, so its root element exists by the time this Awake runs.</summary>
        private void Awake()
        {
            UIDocument? document = GetComponent<UIDocument>();
            if (document == null)
            {
                Debug.LogError(nameof(GameRoot) + " needs a " + nameof(UIDocument) + " on the same GameObject, so it disables itself.", this);
                enabled = false;
                return;
            }

            BuildKind build = Debug.isDebugBuild ? BuildKind.Development : BuildKind.Release;
            _screens = new DocumentScreens(document, new Navigator(LevelCatalogue.Load(new ResourcesLevelTexts())), build);
        }

        /// <summary>
        /// When the Game object is activated, UIDocument, which runs first, has made a new root element; this puts the
        /// screens into it at once, so no frame shows the new root empty.
        /// </summary>
        private void OnEnable()
        {
            if (_screens is not null)
            {
                _screens.KeepInDocument();
            }
        }

        private void Update()
        {
            if (_screens is not null)
            {
                _screens.KeepInDocument();
                _screens.KeepUpWithTheDisplay();
                _screens.KeepAwakeWhileALevelIsOpen();
            }
        }

        /// <summary>No level shows while this component is disabled, so the screen may sleep as the device's settings say.</summary>
        private void OnDisable()
        {
            if (_screens is not null)
            {
                _screens.LetSleep();
            }
        }

        /// <summary>
        /// The game ends with this object: the device's cultures go back, so a culture picked with the culture button does
        /// not outlive the game, as it would in the editor after Play mode.
        /// </summary>
        private void OnDestroy()
        {
            if (_screens is not null)
            {
                _screens.RestoreCultures();
            }
        }

        /// <summary>
        /// The screens, which this object keeps in its UIDocument, and the state of the device that follows them.
        /// <list type="bullet">
        /// <item><description>The screens live in a ScreenHost made once, not in the document: UIDocument makes a new root
        /// element each time it is enabled, so the host is put back into the document's current root whenever it is not
        /// there, and the same screens, with the same navigator and session, come back after the document is disabled and
        /// enabled again.</description></item>
        /// <item><description>The safe-area padding: the screen's size and safe area in pixels and the panel's size in
        /// units, made whole and non-negative (<see cref="Whole"/>), give SafeInsets, which the host takes as its padding
        /// whenever it differs from the padding it has. When the screen's size or safe area differs from the last seen,
        /// the host also writes its development line again.</description></item>
        /// <item><description>The sleep timeout: NeverSleep while a level is open, and the device's own setting on any
        /// other screen, set whenever it should change.</description></item>
        /// </list>
        /// </summary>
        private sealed class DocumentScreens
        {
            private readonly UIDocument _document;
            private readonly Navigator _navigator;
            private readonly ScreenHost _host;

            /// <summary>The padding the host has; None until the first padding is worked out.</summary>
            private Option<SafeInsets> _padding;

            /// <summary>The display as it was last seen; None until it is first seen.</summary>
            private Option<DisplayFacts> _display;

            /// <summary>The sleep timeout set last; None when it has not been set since this was made or since LetSleep.</summary>
            private Option<int> _sleepTimeout;

            public DocumentScreens(UIDocument document, Navigator navigator, BuildKind build)
            {
                _document = document;
                _navigator = navigator;
                _host = new ScreenHost(navigator, build);
            }

            /// <summary>Puts back the device's cultures, which a development build's culture button may have changed.</summary>
            public void RestoreCultures() => _host.RestoreCultures();

            /// <summary>Puts the screens into the document's current root, unless they are there already; a disabled document has no root.</summary>
            public void KeepInDocument()
            {
                VisualElement? root = _document.rootVisualElement;
                if (root is not null && _host.parent != root)
                {
                    root.Add(_host);
                }
            }

            /// <summary>Keeps the safe-area padding current, and the development line told when the display's size or safe area changes.</summary>
            public void KeepUpWithTheDisplay()
            {
                IPanel? panel = _host.panel;
                if (panel is null)
                {
                    return;
                }

                DisplayFacts display = DisplayFacts.Now();
                SafeInsets padding = SafeInsets.ToPanel(display.Size, display.SafeArea, PanelSizeOf(panel));
                if (!_padding.TryGet(out SafeInsets current) || !SamePadding(current, padding))
                {
                    _host.Pad(padding);
                    _padding = Option.Some(padding);
                }

                if (!_display.TryGet(out DisplayFacts seen) || !seen.Matches(display))
                {
                    _display = Option.Some(display);
                    _host.RefreshDevLine();
                }
            }

            public void KeepAwakeWhileALevelIsOpen()
            {
                int wanted = _navigator.Screen is AppScreen.Playing ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
                if (!_sleepTimeout.TryGet(out int current) || current != wanted)
                {
                    Screen.sleepTimeout = wanted;
                    _sleepTimeout = Option.Some(wanted);
                }
            }

            public void LetSleep()
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
                _sleepTimeout = Option.None<int>();
            }

            /// <summary>The panel's size in its own units, as its root's layout gives it: 360 units wide on every phone.</summary>
            private static PanelSize PanelSizeOf(IPanel panel)
            {
                Rect layout = panel.visualTree.layout;
                return new PanelSize(new HorizontalPanelUnits(Whole.Floor(layout.width)), new VerticalPanelUnits(Whole.Floor(layout.height)));
            }

            private static bool SamePadding(SafeInsets one, SafeInsets other) =>
                one.Top.Value == other.Top.Value && one.Left.Value == other.Left.Value
                && one.Bottom.Value == other.Bottom.Value && one.Right.Value == other.Right.Value;
        }
    }
}
