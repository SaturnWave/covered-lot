using CoveredLot.Core.Layout;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The layer that draws the Ahead links over the lot (design sections 8.5 and 9): for each link, a line from the centre
    /// of the tagged vehicle's front cell to the centre of the cell of the vehicle ahead of it, ending in an arrowhead
    /// there. The layer fills the lot's frame, so its own coordinates are the frame's, in which the board's geometry
    /// places every cell. It draws with Painter2D when UI Toolkit generates its visual content, and takes no pointer
    /// events. CoveredLot.uss gives its whole look: the colour of the lines and arrowheads is the layer's resolved text
    /// colour, and two custom properties give, in panel units, the width of the lines (--cl-link-width) and the length
    /// and base of each arrowhead (--cl-arrow-size). Until the style gives both, and while the lot has no geometry, it
    /// draws nothing.
    /// </summary>
    public sealed class AheadLinksElement : VisualElement
    {
        private static readonly CustomStyleProperty<float> LineWidthProperty = new CustomStyleProperty<float>("--cl-link-width");
        private static readonly CustomStyleProperty<float> ArrowSizeProperty = new CustomStyleProperty<float>("--cl-arrow-size");

        private ReadOnlyArray<AheadLinkPicture> _links;
        private Option<BoardGeometry> _geometry;

        /// <summary>The width of the lines in panel units, as the style gives it; 0, which draws nothing, until it does.</summary>
        private float _lineWidth;

        /// <summary>The length and the base of each arrowhead in panel units, as the style gives it; 0, which draws nothing, until it does.</summary>
        private float _arrowSize;

        public AheadLinksElement()
        {
            AddToClassList(StyleNames.Layer);
            AddToClassList(StyleNames.AheadLinks);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(TakeCustomStyle);
        }

        /// <summary>The links the layer shows, in the order of the board's picture.</summary>
        public ReadOnlyArray<AheadLinkPicture> Links => _links;

        /// <summary>How many arrows the layer drew the last time UI Toolkit generated its visual content.</summary>
        public int ArrowsDrawn { get; private set; }

        /// <summary>Shows these links on the lot as the geometry lays it out; with None it draws none, until it is given a geometry.</summary>
        public void Show(ReadOnlyArray<AheadLinkPicture> links, Option<BoardGeometry> geometry)
        {
            _links = links;
            _geometry = geometry;
            MarkDirtyRepaint();
        }

        private static Vector2 PointOf(PanelPoint point) => new Vector2(point.X.Value, point.Y.Value);

        private void TakeCustomStyle(CustomStyleResolvedEvent resolved)
        {
            _lineWidth = resolved.customStyle.TryGetValue(LineWidthProperty, out float lineWidth) ? lineWidth : 0f;
            _arrowSize = resolved.customStyle.TryGetValue(ArrowSizeProperty, out float arrowSize) ? arrowSize : 0f;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            int drawn = 0;
            if (_geometry.TryGet(out BoardGeometry? geometry) && _lineWidth > 0f && _arrowSize > 0f)
            {
                Painter2D painter = context.painter2D;
                painter.strokeColor = resolvedStyle.color;
                painter.fillColor = resolvedStyle.color;
                painter.lineWidth = _lineWidth;
                foreach (AheadLinkPicture link in _links)
                {
                    if (DrawArrow(painter, PointOf(geometry.CentreOf(link.From)), PointOf(geometry.CentreOf(link.To))))
                    {
                        drawn++;
                    }
                }
            }

            ArrowsDrawn = drawn;
        }

        /// <summary>
        /// Draws a line from one point towards the other, and an arrowhead whose tip is the other point; returns whether it
        /// drew one. Two points closer together than an arrowhead's length leave no room for it, and get no arrow.
        /// </summary>
        private bool DrawArrow(Painter2D painter, Vector2 from, Vector2 to)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length <= _arrowSize)
            {
                return false;
            }

            Vector2 direction = along / length;
            Vector2 headBase = to - (direction * _arrowSize);
            Vector2 halfBase = new Vector2(-direction.y, direction.x) * (_arrowSize / 2f);

            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(headBase);
            painter.Stroke();

            painter.BeginPath();
            painter.MoveTo(to);
            painter.LineTo(headBase + halfBase);
            painter.LineTo(headBase - halfBase);
            painter.ClosePath();
            painter.Fill();
            return true;
        }
    }
}
