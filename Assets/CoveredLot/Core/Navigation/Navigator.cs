using System;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using PuzzleCore;

namespace CoveredLot.Core.Navigation
{
    /// <summary>
    /// The game's screen flow (design section 7). A catalogue that loaded starts on the level list, and one that did not on
    /// the problem screen, which no action leaves. Open plays an entry of the list, Next the entry after the one in play,
    /// Again the one in play once more (the win card's Play again, the fail card's Retry and the top bar's Restart), and
    /// BackToList returns to the list. Every level it opens, the same one again included, gets a new session: a fresh
    /// attempt with nothing seen, so every cover comes back, the peak starts at no bays and the last tap at None. Leaving a
    /// level discards its attempt, since nothing is saved. An action that does not apply to the screen shown changes
    /// nothing.
    /// </summary>
    public sealed class Navigator
    {
        /// <summary>The level list; None for a catalogue that did not load, which has none.</summary>
        private readonly Option<AppScreen.LevelList> _list;

        public Navigator(CatalogueResult catalogue)
        {
            Screen = catalogue switch
            {
                CatalogueResult.Ready ready => new AppScreen.LevelList(ready.Entries),
                CatalogueResult.Broken broken => new AppScreen.Problems(broken.Problems),
                _ => throw new ArgumentOutOfRangeException(nameof(catalogue), "The navigator has no first screen for this kind of catalogue result."),
            };
            _list = Screen is AppScreen.LevelList list ? Option.Some(list) : Option.None<AppScreen.LevelList>();
        }

        /// <summary>The screen shown now.</summary>
        public AppScreen Screen { get; private set; }

        /// <summary>Whether Next would play an entry: a level is in play and another entry follows it. The win card offers Next level only then.</summary>
        public bool HasNext => Screen is AppScreen.Playing playing && IsEntry(playing.Index + 1);

        /// <summary>The catalogue's entries; none for a catalogue that did not load.</summary>
        private ReadOnlyArray<CatalogueEntry> Entries =>
            _list.TryGet(out AppScreen.LevelList? list) ? list.Entries : ReadOnlyArray.Empty<CatalogueEntry>();

        /// <summary>
        /// Plays the entry at this index of the level list, from any screen of a catalogue that loaded, with a new session.
        /// An index outside the list changes nothing, and so does any index on the problem screen.
        /// </summary>
        public void Open(int index)
        {
            if (IsEntry(index))
            {
                Screen = PlayingAt(index);
            }
        }

        /// <summary>
        /// Plays the entry after the one in play, with a new session, and returns its screen. None, with nothing changed,
        /// after the last entry and when no level is in play.
        /// </summary>
        public Option<AppScreen.Playing> Next()
        {
            if (Screen is AppScreen.Playing playing && IsEntry(playing.Index + 1))
            {
                AppScreen.Playing next = PlayingAt(playing.Index + 1);
                Screen = next;
                return Option.Some(next);
            }

            return Option.None<AppScreen.Playing>();
        }

        /// <summary>Plays the entry in play once more, with a new session. Off a level it changes nothing.</summary>
        public void Again()
        {
            if (Screen is AppScreen.Playing playing)
            {
                Screen = PlayingAt(playing.Index);
            }
        }

        /// <summary>Shows the level list. On the problem screen it changes nothing: a catalogue that did not load has no list.</summary>
        public void BackToList()
        {
            if (_list.TryGet(out AppScreen.LevelList? list))
            {
                Screen = list;
            }
        }

        private bool IsEntry(int index) => index >= 0 && index < Entries.Count;

        private AppScreen.Playing PlayingAt(int index) => new AppScreen.Playing(index, new GameSession(Entries[index]));
    }
}
