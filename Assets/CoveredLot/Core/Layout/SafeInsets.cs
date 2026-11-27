using System;

namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// The padding that keeps the screens inside the screen's safe area, clear of a camera cutout or a rounded corner
    /// (design section 6), in panel units: on each side, the screen's pixels outside the safe area, scaled to the panel on
    /// their own axis and rounded up, so that nothing reaches into the cutout. Screen.safeArea counts its y from the
    /// screen's bottom and the panel counts down from its top, so the pixels below the safe area are the bottom padding and
    /// those above it the top padding. A side where the safe area reaches the screen's edge, or past it as a rounding of the
    /// host's can make it, gets none, and so does an axis of a screen with no pixels.
    /// </summary>
    public readonly struct SafeInsets
    {
        private SafeInsets(PixelSize screen, PixelRect safeArea, PanelSize panel)
        {
            var across = new Scale(screen.Width, panel.Width);
            var down = new Scale(screen.Height, panel.Height);
            Left = new HorizontalPanelUnits(across.UnitsFor(safeArea.X.Value));
            Right = new HorizontalPanelUnits(across.UnitsFor((long)screen.Width.Value - safeArea.X.Value - safeArea.Size.Width.Value));
            Bottom = new VerticalPanelUnits(down.UnitsFor(safeArea.Y.Value));
            Top = new VerticalPanelUnits(down.UnitsFor((long)screen.Height.Value - safeArea.Y.Value - safeArea.Size.Height.Value));
        }

        public VerticalPanelUnits Top { get; }

        public HorizontalPanelUnits Left { get; }

        public VerticalPanelUnits Bottom { get; }

        public HorizontalPanelUnits Right { get; }

        /// <summary>The padding for the safe area of the screen, in units of the panel the screens are laid out in.</summary>
        public static SafeInsets ToPanel(PixelSize screen, PixelRect safeArea, PanelSize panel) => new SafeInsets(screen, safeArea, panel);

        /// <summary>How many of the panel's units along one axis a count of the screen's pixels along it takes.</summary>
        private readonly struct Scale
        {
            private readonly int _screenPixels;
            private readonly int _panelUnits;

            public Scale(HorizontalPixels screen, HorizontalPanelUnits panel)
            {
                _screenPixels = screen.Value;
                _panelUnits = panel.Value;
            }

            public Scale(VerticalPixels screen, VerticalPanelUnits panel)
            {
                _screenPixels = screen.Value;
                _panelUnits = panel.Value;
            }

            /// <summary>
            /// The panel units these pixels take, rounded up, after holding the pixels to the screen's own: none below zero
            /// and none beyond the screen's pixels on this axis. None at all on an axis of a screen with no pixels.
            /// </summary>
            public int UnitsFor(long pixels)
            {
                if (_screenPixels == 0)
                {
                    return 0;
                }

                long held = Math.Min(Math.Max(pixels, 0), _screenPixels);
                return (int)(((held * _panelUnits) + _screenPixels - 1) / _screenPixels);
            }
        }
    }
}
