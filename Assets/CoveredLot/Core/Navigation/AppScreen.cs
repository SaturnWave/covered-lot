using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using PuzzleCore;

namespace CoveredLot.Core.Navigation
{
    /// <summary>
    /// The screen the game shows (design section 7): the problem screen when the catalogue did not load, the level list, or
    /// a level in play. The end card is no screen of its own: the play screen shows it whenever its session's outcome is not
    /// InPlay. Closed; only the navigator makes screens.
    /// </summary>
    public abstract class AppScreen
    {
        private AppScreen()
        {
        }

        /// <summary>The problem screen of a catalogue that did not load: one sentence, and a line for each problem in development builds.</summary>
        public sealed class Problems : AppScreen
        {
            internal Problems(ReadOnlyArray<CatalogueProblem> list) => List = list;

            /// <summary>The catalogue's problems, in its order; never empty.</summary>
            public ReadOnlyArray<CatalogueProblem> List { get; }
        }

        /// <summary>The level list: the Example first, then Level 1 to N.</summary>
        public sealed class LevelList : AppScreen
        {
            internal LevelList(ReadOnlyArray<CatalogueEntry> entries) => Entries = entries;

            /// <summary>Every entry of the catalogue, in its order.</summary>
            public ReadOnlyArray<CatalogueEntry> Entries { get; }
        }

        /// <summary>A level in play: its entry's index in the level list, and the session that plays it.</summary>
        public sealed class Playing : AppScreen
        {
            internal Playing(int index, GameSession session)
            {
                Index = index;
                Session = session;
            }

            /// <summary>The entry's index in the level list: 0 for the Example, n for Level n.</summary>
            public int Index { get; }

            public GameSession Session { get; }
        }
    }
}
