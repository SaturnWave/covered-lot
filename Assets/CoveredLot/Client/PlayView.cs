using System;
using System.Collections.Generic;
using CoveredLot.Core;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using PuzzleCore;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The play screen of a level (design sections 7 and 9): a top bar with the Levels button, the entry's title and the
    /// Restart button; under it the status line, the lot, the bays row, the line of the bays that are free and the line
    /// of the peak against the par; on a covered level the ledger and the tags list; and, once the level is over, an
    /// opaque end card over all of that, which takes every tap there. The win card shows "Cleared!", the stars and the
    /// peak, with Play again, Next level (only when another entry follows) and Levels; the fail card shows the fail's
    /// sentence with Retry and Levels. Restart, Play again and Retry play the level again with a new session, Next level
    /// plays the next entry and Levels returns to the list, each through the navigator, after which the screen host
    /// repaints. A tap on a tile taps the session, and the screen repaints. Every repaint sets every text, class and
    /// display from the session's pictures, so nothing a picture no longer shows survives it. Every string comes from
    /// Copy or a picture.
    /// </summary>
    public sealed class PlayView : VisualElement
    {
        private readonly Navigator _navigator;
        private readonly Action _screenChanged;

        private readonly Label _title = Classed.Label(StyleNames.Title);
        private readonly Label _status = Classed.Label(StyleNames.Status);
        private readonly BoardView _board;
        private readonly VisualElement _bays = Classed.Element(StyleNames.Bays);
        private readonly List<BayBox> _bayBoxes = new List<BayBox>();
        private readonly Label _baysFree = Classed.Label(StyleNames.HudLine);
        private readonly Label _peak = Classed.Label(StyleNames.HudLine);
        private readonly VisualElement _ledger = Classed.Element(StyleNames.Ledger);
        private readonly List<Label> _pills = new List<Label>();
        private readonly VisualElement _tags = Classed.Element(StyleNames.Tags);
        private readonly List<TagRow> _tagRows = new List<TagRow>();

        private readonly VisualElement _winCard = Classed.Element(StyleNames.Card);
        private readonly Label _winStars = Classed.Label(StyleNames.CardLine);
        private readonly Label _winPeak = Classed.Label(StyleNames.CardLine);
        private readonly Button _nextLevel;
        private readonly VisualElement _failCard = Classed.Element(StyleNames.Card);
        private readonly Label _failSentence = Classed.Label(StyleNames.CardHeading);

        /// <summary>The session the screen shows; null until the first is shown.</summary>
        private GameSession? _session;

        /// <param name="navigator">The game's screen flow, which the buttons drive.</param>
        /// <param name="screenChanged">What the buttons do after the navigator has changed the screen: repaint the screens.</param>
        public PlayView(Navigator navigator, Action screenChanged)
        {
            _navigator = navigator;
            _screenChanged = screenChanged;
            _board = new BoardView(Tap);
            _nextLevel = Classed.Button(Copy.NextLevel, Next);
            AddToClassList(StyleNames.Play);

            VisualElement topBar = Classed.Element(StyleNames.TopBar);
            topBar.Add(Classed.Button(Copy.Levels, BackToList));
            topBar.Add(_title);
            topBar.Add(Classed.Button(Copy.Restart, Again));
            Add(topBar);

            Label stillCovered = Classed.Label(StyleNames.Heading);
            stillCovered.text = Copy.StillCoveredHeading;
            _ledger.Add(stillCovered);
            Label tagsHeading = Classed.Label(StyleNames.Heading);
            tagsHeading.text = Copy.TagsHeading;
            _tags.Add(tagsHeading);

            Label cleared = Classed.Label(StyleNames.CardHeading);
            cleared.text = Copy.ClearedHeading;
            _winCard.Add(cleared);
            _winCard.Add(_winStars);
            _winCard.Add(_winPeak);
            _winCard.Add(Classed.Button(Copy.PlayAgain, Again));
            _winCard.Add(_nextLevel);
            _winCard.Add(Classed.Button(Copy.Levels, BackToList));
            _failCard.Add(_failSentence);
            _failCard.Add(Classed.Button(Copy.Retry, Again));
            _failCard.Add(Classed.Button(Copy.Levels, BackToList));

            VisualElement area = Classed.Element(StyleNames.PlayArea);
            area.Add(_status);
            area.Add(_board);
            area.Add(_bays);
            area.Add(_baysFree);
            area.Add(_peak);
            area.Add(_ledger);
            area.Add(_tags);
            area.Add(_winCard);
            area.Add(_failCard);
            Add(area);
            ShowIf(_winCard, false);
            ShowIf(_failCard, false);
        }

        /// <summary>The lot of the level in play.</summary>
        public BoardView Board => _board;

        /// <summary>Shows the session as it is now; the board makes fresh elements for a session it has not shown before.</summary>
        public void Show(GameSession session)
        {
            _session = session;
            Paint(session);
        }

        private static void ShowIf(VisualElement element, bool shown) =>
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>
        /// Makes the list, and the container after the children it already holds, have exactly this many elements: new
        /// ones are made at the end, and the last ones are dropped.
        /// </summary>
        private static void Resize<T>(List<T> elements, VisualElement container, int count, Func<T> make)
            where T : VisualElement
        {
            while (elements.Count < count)
            {
                T made = make();
                elements.Add(made);
                container.Add(made);
            }

            while (elements.Count > count)
            {
                T last = elements[elements.Count - 1];
                elements.RemoveAt(elements.Count - 1);
                last.RemoveFromHierarchy();
            }
        }

        private void Tap(PieceId piece)
        {
            // A tile exists only once a session is shown, so a click always finds one.
            if (_session is null)
            {
                return;
            }

            _session.Tap(piece);
            Paint(_session);
        }

        private void Again()
        {
            _navigator.Again();
            _screenChanged();
        }

        /// <summary>Plays the next entry. The button shows only while one follows, so the navigator always opens it.</summary>
        private void Next()
        {
            _ = _navigator.Next();
            _screenChanged();
        }

        private void BackToList()
        {
            _navigator.BackToList();
            _screenChanged();
        }

        private void Paint(GameSession session)
        {
            HudPicture hud = HudPicture.Of(session);
            _title.text = hud.Title;
            _status.text = hud.StatusLine;
            _board.Show(session);
            PaintBays(hud.Bays);
            _baysFree.text = hud.BaysFreeLine;
            _peak.text = hud.PeakLine;
            PaintLedger(hud.Ledger);
            PaintTags(hud.Tags);
            PaintCard(EndPicture.Of(session));
        }

        private void PaintBays(ReadOnlyArray<BayPicture> bays)
        {
            Resize(_bayBoxes, _bays, bays.Count, () => new BayBox());
            for (int index = 0; index < bays.Count; index++)
            {
                _bayBoxes[index].Paint(bays[index]);
            }
        }

        /// <summary>The ledger's pills, on a covered level; a level without covers has none, and no ledger.</summary>
        private void PaintLedger(ReadOnlyArray<LedgerPill> pills)
        {
            ShowIf(_ledger, !pills.IsEmpty);
            Resize(_pills, _ledger, pills.Count, () => Classed.Label(StyleNames.Pill));
            for (int index = 0; index < pills.Count; index++)
            {
                _pills[index].text = pills[index].Text;
                SwatchClasses.Paint(_pills[index], Option.Some(pills[index].Swatch));
            }
        }

        /// <summary>The tags list's rows, on a covered level with tags; any other level has no tags list.</summary>
        private void PaintTags(Option<CluePicture> tags)
        {
            ReadOnlyArray<ClueRow> rows = tags.TryGet(out CluePicture? picture) ? picture.Rows : ReadOnlyArray.Empty<ClueRow>();
            ShowIf(_tags, !rows.IsEmpty);
            Resize(_tagRows, _tags, rows.Count, () => new TagRow());
            for (int index = 0; index < rows.Count; index++)
            {
                _tagRows[index].Paint(rows[index]);
            }
        }

        private void PaintCard(EndPicture end)
        {
            switch (end)
            {
                case EndPicture.Playing:
                    ShowIf(_winCard, false);
                    ShowIf(_failCard, false);
                    break;
                case EndPicture.Cleared cleared:
                    _winStars.text = cleared.StarsLine;
                    _winPeak.text = cleared.PeakLine;
                    ShowIf(_nextLevel, _navigator.HasNext);
                    ShowIf(_winCard, true);
                    ShowIf(_failCard, false);
                    break;
                case EndPicture.BaysFull full:
                    ShowFailCard(full.Text);
                    break;
                case EndPicture.NoMoveLeft stuck:
                    ShowFailCard(stuck.Text);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(end), "The play screen has no card for this kind of end picture.");
            }
        }

        private void ShowFailCard(string sentence)
        {
            _failSentence.text = sentence;
            ShowIf(_winCard, false);
            ShowIf(_failCard, true);
        }

        /// <summary>One bay of the bays row: a light outline while empty, and the swatch and letter of the colour waiting in it.</summary>
        private sealed class BayBox : VisualElement
        {
            private readonly Label _letter = Classed.Label(StyleNames.BayLetter, PickingMode.Ignore);

            public BayBox()
            {
                AddToClassList(StyleNames.Bay);
                pickingMode = PickingMode.Ignore;
                Add(_letter);
            }

            public void Paint(BayPicture bay)
            {
                switch (bay)
                {
                    case BayPicture.Empty:
                        SwatchClasses.Paint(this, Option.None<Swatch>());
                        _letter.text = string.Empty;
                        break;
                    case BayPicture.Filled filled:
                        SwatchClasses.Paint(this, Option.Some(filled.Swatch));
                        _letter.text = filled.Letter;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(bay), "The bays row has no look for this kind of bay.");
                }
            }
        }

        /// <summary>One row of the tags list: the vehicle's badge, then its tags.</summary>
        private sealed class TagRow : VisualElement
        {
            private readonly BadgeLabel _badge = new BadgeLabel();
            private readonly Label _line = Classed.Label(StyleNames.TagLine);

            public TagRow()
            {
                AddToClassList(StyleNames.TagRow);
                Add(_badge);
                Add(_line);
            }

            public void Paint(ClueRow row)
            {
                _badge.Show(Option.Some(row.Badge));
                _line.text = row.Line;
            }
        }
    }
}
