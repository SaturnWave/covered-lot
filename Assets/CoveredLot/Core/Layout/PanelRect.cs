namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// A rectangle in panel units inside an element, with its origin at the element's top-left corner, as UI Toolkit places
    /// an absolutely positioned child: its left, its top and its size. Only the board's geometry makes them.
    /// </summary>
    public readonly struct PanelRect
    {
        internal PanelRect(HorizontalPanelUnits x, VerticalPanelUnits y, PanelSize size)
        {
            X = x;
            Y = y;
            Size = size;
        }

        /// <summary>The distance of the rectangle's left edge from the element's left edge.</summary>
        public HorizontalPanelUnits X { get; }

        /// <summary>The distance of the rectangle's top edge from the element's top edge.</summary>
        public VerticalPanelUnits Y { get; }

        public PanelSize Size { get; }
    }
}
