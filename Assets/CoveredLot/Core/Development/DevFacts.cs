using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Navigation;
using PuzzleCore;

namespace CoveredLot.Core.Development
{
    /// <summary>
    /// What the development line shows (design section 9), which only development builds have: while a level is in play,
    /// its entry, whose slot (or the Example) and par the line names; the name of the culture the game runs in; the screen's
    /// size and its safe area, in pixels; and, once the board is laid out, its geometry, whose cells' side the line names.
    /// The host reads the culture's name, the screen and the safe area, and hands them over unchanged; Copy.DevLine writes
    /// the line.
    /// </summary>
    public sealed class DevFacts
    {
        /// <param name="shown">The screen the game shows; the line names a level only while one is in play.</param>
        /// <param name="board">The board's geometry as it is laid out; None off a level and before the board's first layout.</param>
        /// <param name="cultureName">The culture's name, as the host reads it: "tr-TR", or empty for the invariant culture.</param>
        /// <param name="screen">The screen's size in pixels.</param>
        /// <param name="safeArea">The screen's safe area in pixels, from its bottom-left corner.</param>
        public DevFacts(AppScreen shown, Option<BoardGeometry> board, string cultureName, PixelSize screen, PixelRect safeArea)
        {
            Entry = shown is AppScreen.Playing playing ? Option.Some(playing.Session.Entry) : Option.None<CatalogueEntry>();
            Board = board;
            CultureName = cultureName;
            Screen = screen;
            SafeArea = safeArea;
        }

        /// <summary>The entry of the level in play; None off a level.</summary>
        public Option<CatalogueEntry> Entry { get; }

        /// <summary>The board's geometry as it is laid out; None off a level and before the board's first layout.</summary>
        public Option<BoardGeometry> Board { get; }

        /// <summary>The name of the culture the game runs in: "tr-TR", or empty for the invariant culture.</summary>
        public string CultureName { get; }

        /// <summary>The screen's size in pixels.</summary>
        public PixelSize Screen { get; }

        /// <summary>The screen's safe area in pixels, from its bottom-left corner.</summary>
        public PixelRect SafeArea { get; }
    }
}
