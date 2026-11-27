using System;
using System.Collections.Generic;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using PuzzleCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The lot of the level in play (design sections 8.5 and 9), drawn in the frame the play screen gives it, in layers
    /// from the bottom up: the dark lot with its cells and pillars, the ghosts, the tiles, and the Ahead links.
    /// <list type="bullet">
    /// <item><description>A session's first picture makes the elements, so a level that opens, the same one again included,
    /// starts with fresh ones. That picture shows every vehicle of the level, so the tile layer holds a tile for each, in
    /// PieceId order, and the ghost layer a ghost for each, in the same order, hidden until its vehicle leaves a covered
    /// level. A click on a tile taps its vehicle.</description></item>
    /// <item><description>Every later picture of the session repaints the elements by their display, text and classes, so
    /// no element is removed while it handles its own click, and nothing a picture no longer shows survives it: a tile
    /// whose vehicle has left is hidden, and the blocker mark stays only on what the last tap bumped into.</description></item>
    /// <item><description>The frame's size is measured on every GeometryChangedEvent, made whole and non-negative, and
    /// BoardGeometry.Fit gives the positions and sizes of the lot, its cells and its vehicles' boxes in it. While Fit gives
    /// none, as before the frame's first layout, the lot is hidden until the frame's size changes again.</description></item>
    /// </list>
    /// The cells, the pillars, the ghosts and the links take no pointer events; only the tiles do.
    /// </summary>
    public sealed class BoardView : VisualElement
    {
        private readonly Action<PieceId> _tapped;

        /// <summary>Everything the geometry places, which is hidden while there is no geometry.</summary>
        private readonly VisualElement _content = Classed.Element(StyleNames.Layer, PickingMode.Ignore);

        private readonly VisualElement _lot = Classed.Element(StyleNames.Lot, PickingMode.Ignore);
        private readonly VisualElement _cellLayer = Classed.Element(StyleNames.Layer, PickingMode.Ignore);
        private readonly VisualElement _ghostLayer = Classed.Element(StyleNames.Layer, PickingMode.Ignore);
        private readonly VisualElement _tileLayer = Classed.Element(StyleNames.Layer, PickingMode.Ignore);
        private readonly AheadLinksElement _links = new AheadLinksElement();
        private readonly List<PillarCell> _pillars = new List<PillarCell>();
        private readonly List<GhostSlot> _ghosts = new List<GhostSlot>();
        private readonly List<TileSlot> _tiles = new List<TileSlot>();

        /// <summary>The session the board shows; null before the first one.</summary>
        private GameSession? _session;

        /// <summary>The picture the board shows now; null before the first session.</summary>
        private BoardPicture? _picture;

        /// <summary>The frame's size, as its last layout gave it; None before its first.</summary>
        private Option<PanelSize> _frame;

        /// <summary>Where the lot lies in the frame; None while the frame has no size that holds it.</summary>
        private Option<BoardGeometry> _geometry;

        /// <param name="tapped">What a click on a vehicle's tile does: tap that vehicle.</param>
        public BoardView(Action<PieceId> tapped)
        {
            _tapped = tapped;
            AddToClassList(StyleNames.LotFrame);
            _content.Add(_lot);
            _content.Add(_cellLayer);
            _content.Add(_ghostLayer);
            _content.Add(_tileLayer);
            _content.Add(_links);
            Add(_content);
            _content.style.display = DisplayStyle.None;
            RegisterCallback<GeometryChangedEvent>(Measure);
        }

        /// <summary>The Ahead links layer, which draws the arrows of the board's picture.</summary>
        public AheadLinksElement Links => _links;

        /// <summary>
        /// Shows the session's board as it is now. A session other than the one shown before gets fresh elements; the same
        /// one has its elements repainted.
        /// </summary>
        public void Show(GameSession session)
        {
            BoardPicture picture = BoardPicture.Of(session);
            if (session != _session)
            {
                MakeElements(picture);
                _session = session;
            }

            _picture = picture;
            Paint(picture);
            Layout();
        }

        /// <summary>Drops the elements of the session shown before, and makes the pillars, tiles and ghosts of a session's first picture.</summary>
        private void MakeElements(BoardPicture first)
        {
            _pillars.Clear();
            _ghosts.Clear();
            _tiles.Clear();
            _cellLayer.Clear();
            _ghostLayer.Clear();
            _tileLayer.Clear();
            foreach (PillarPicture pillar in first.Pillars)
            {
                var cell = new PillarCell(pillar.Cell);
                _pillars.Add(cell);
                _cellLayer.Add(cell);
            }

            foreach (TilePicture tile in first.Tiles)
            {
                EnsureSlotsFor(tile.Piece, tile.Box);
            }
        }

        /// <summary>Repaints every element from the picture. A level's pillars never change, so the picture has one for each pillar cell, in the same order.</summary>
        private void Paint(BoardPicture picture)
        {
            for (int index = 0; index < _pillars.Count && index < picture.Pillars.Count; index++)
            {
                _pillars[index].Paint(picture.Pillars[index]);
            }

            foreach (GhostPicture ghost in picture.Ghosts)
            {
                EnsureSlotsFor(ghost.Piece, ghost.Box);
            }

            foreach (TileSlot tile in _tiles)
            {
                tile.Paint(TileOf(tile.Piece, picture));
            }

            foreach (GhostSlot ghost in _ghosts)
            {
                ghost.Paint(GhostOf(ghost.Piece, picture));
            }
        }

        /// <summary>
        /// Gives the vehicle a tile and a ghost if it has none yet. A session's first picture gives every vehicle both;
        /// a vehicle a later picture names for the first time, which a session never does, gets them at the end.
        /// </summary>
        private void EnsureSlotsFor(PieceId piece, GridBox box)
        {
            foreach (TileSlot tile in _tiles)
            {
                if (tile.Piece == piece)
                {
                    return;
                }
            }

            var newTile = new TileSlot(piece, box, _tapped);
            var newGhost = new GhostSlot(piece, box);
            _tiles.Add(newTile);
            _ghosts.Add(newGhost);
            _tileLayer.Add(newTile);
            _ghostLayer.Add(newGhost);
        }

        private static Option<TilePicture> TileOf(PieceId piece, BoardPicture picture)
        {
            foreach (TilePicture tile in picture.Tiles)
            {
                if (tile.Piece == piece)
                {
                    return Option.Some(tile);
                }
            }

            return Option.None<TilePicture>();
        }

        private static Option<GhostPicture> GhostOf(PieceId piece, BoardPicture picture)
        {
            foreach (GhostPicture ghost in picture.Ghosts)
            {
                if (ghost.Piece == piece)
                {
                    return Option.Some(ghost);
                }
            }

            return Option.None<GhostPicture>();
        }

        private void Measure(GeometryChangedEvent changed)
        {
            Rect frame = changed.newRect;
            _frame = Option.Some(new PanelSize(new HorizontalPanelUnits(Whole.Floor(frame.width)), new VerticalPanelUnits(Whole.Floor(frame.height))));
            Layout();
        }

        /// <summary>
        /// Fits the lot of the picture shown into the frame, places every element where the geometry puts it, and shows
        /// the lot; hides it when there is no picture yet, no frame size yet, or no geometry for them.
        /// </summary>
        private void Layout()
        {
            _geometry = _picture is not null && _frame.TryGet(out PanelSize frame)
                ? BoardGeometry.Fit(frame, _picture.Size)
                : Option.None<BoardGeometry>();
            _content.style.display = _geometry.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            _links.Show(_picture is not null ? _picture.AheadLinks : ReadOnlyArray.Empty<AheadLinkPicture>(), _geometry);
            if (!_geometry.TryGet(out BoardGeometry? geometry))
            {
                return;
            }

            Place(_lot, geometry.Board);
            PlaceCells(geometry);
            foreach (PillarCell pillar in _pillars)
            {
                Place(pillar, geometry.RectOf(pillar.Cell));
            }

            foreach (GhostSlot ghost in _ghosts)
            {
                Place(ghost, geometry.RectOf(ghost.Box));
            }

            foreach (TileSlot tile in _tiles)
            {
                Place(tile, geometry.RectOf(tile.Box));
            }
        }

        /// <summary>
        /// One empty cell for every cell of the lot, under the pillars, which stay the cell layer's last children. The
        /// layer gains or loses cells when the lot's size has changed.
        /// </summary>
        private void PlaceCells(BoardGeometry geometry)
        {
            int cellCount = geometry.Cells.Count;
            while (_cellLayer.childCount - _pillars.Count < cellCount)
            {
                _cellLayer.Insert(0, Classed.Element(StyleNames.Cell, PickingMode.Ignore));
            }

            while (_cellLayer.childCount - _pillars.Count > cellCount)
            {
                _cellLayer.RemoveAt(0);
            }

            for (int index = 0; index < cellCount; index++)
            {
                Place(_cellLayer[index], geometry.Cells[index]);
            }
        }

        /// <summary>Puts the element at the rectangle, in panel units from the frame's top-left corner.</summary>
        private static void Place(VisualElement element, PanelRect rect)
        {
            element.style.left = rect.X.Value;
            element.style.top = rect.Y.Value;
            element.style.width = rect.Size.Width.Value;
            element.style.height = rect.Size.Height.Value;
        }

        /// <summary>A pillar's cell, with its glyph and its blocker mark.</summary>
        private sealed class PillarCell : VisualElement
        {
            private readonly Label _glyph = Classed.Label(StyleNames.Glyph, PickingMode.Ignore);

            public PillarCell(GridCell cell)
            {
                Cell = cell;
                AddToClassList(StyleNames.Cell);
                AddToClassList(StyleNames.Pillar);
                pickingMode = PickingMode.Ignore;
                Add(_glyph);
            }

            public GridCell Cell { get; }

            public void Paint(PillarPicture pillar)
            {
                _glyph.text = pillar.Label;
                EnableInClassList(StyleNames.Blocker, pillar.Mark == TileMark.Blocker);
            }
        }

        /// <summary>
        /// A vehicle's tile: its slot, which the geometry places over the vehicle's box, holding the tile itself a little
        /// inside the box's edges, with its label and its badge. A click on the tile taps the vehicle.
        /// </summary>
        private sealed class TileSlot : VisualElement
        {
            private readonly VisualElement _tile = Classed.Element(StyleNames.Tile);
            private readonly Label _label = Classed.Label(StyleNames.TileLabel, PickingMode.Ignore);
            private readonly BadgeLabel _badge = new BadgeLabel();

            public TileSlot(PieceId piece, GridBox box, Action<PieceId> tapped)
            {
                Piece = piece;
                Box = box;
                AddToClassList(StyleNames.Slot);
                pickingMode = PickingMode.Ignore;
                _tile.RegisterCallback<ClickEvent>(_ => tapped(piece));
                _tile.Add(_label);
                _tile.Add(_badge);
                Add(_tile);
            }

            public PieceId Piece { get; }

            public GridBox Box { get; }

            /// <summary>Shows the tile as its picture says; None, for a vehicle that has left the lot, hides it.</summary>
            public void Paint(Option<TilePicture> picture)
            {
                if (!picture.TryGet(out TilePicture? tile))
                {
                    style.display = DisplayStyle.None;
                    return;
                }

                style.display = DisplayStyle.Flex;
                PaintFace(tile.Face);
                _tile.EnableInClassList(StyleNames.Blocker, tile.Mark == TileMark.Blocker);
                _label.text = tile.Label;
                _badge.Show(tile.Badge);
            }

            private void PaintFace(TileFace face)
            {
                switch (face)
                {
                    case TileFace.Open open:
                        _tile.RemoveFromClassList(StyleNames.Covered);
                        SwatchClasses.Paint(_tile, Option.Some(open.Swatch));
                        break;
                    case TileFace.Covered:
                        _tile.AddToClassList(StyleNames.Covered);
                        SwatchClasses.Paint(_tile, Option.None<Swatch>());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(face), "A tile has no look for this kind of face.");
                }
            }
        }

        /// <summary>
        /// A vehicle's ghost: its slot, which the geometry places over the vehicle's start box, holding the outline in the
        /// vehicle's swatch with its true label and its badge. It takes no pointer events.
        /// </summary>
        private sealed class GhostSlot : VisualElement
        {
            private readonly VisualElement _ghost = Classed.Element(StyleNames.Ghost, PickingMode.Ignore);
            private readonly Label _label = Classed.Label(StyleNames.TileLabel, PickingMode.Ignore);
            private readonly BadgeLabel _badge = new BadgeLabel();

            public GhostSlot(PieceId piece, GridBox box)
            {
                Piece = piece;
                Box = box;
                AddToClassList(StyleNames.Slot);
                pickingMode = PickingMode.Ignore;
                _ghost.Add(_label);
                _ghost.Add(_badge);
                Add(_ghost);
                style.display = DisplayStyle.None;
            }

            public PieceId Piece { get; }

            public GridBox Box { get; }

            /// <summary>Shows the ghost as its picture says; None, for a vehicle still in the lot or on a level without covers, hides it.</summary>
            public void Paint(Option<GhostPicture> picture)
            {
                if (!picture.TryGet(out GhostPicture? ghost))
                {
                    style.display = DisplayStyle.None;
                    return;
                }

                style.display = DisplayStyle.Flex;
                SwatchClasses.Paint(_ghost, Option.Some(ghost.Swatch));
                _label.text = ghost.Label;
                _badge.Show(ghost.Badge);
            }
        }
    }
}
