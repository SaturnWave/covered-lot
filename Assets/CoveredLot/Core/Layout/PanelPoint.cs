namespace CoveredLot.Core.Layout
{
    /// <summary>
    /// A point in panel units inside an element, counted from the element's top-left corner: where an Ahead link's arrow
    /// starts or ends. Only the board's geometry makes them.
    /// </summary>
    public readonly struct PanelPoint
    {
        internal PanelPoint(HorizontalPanelUnits x, VerticalPanelUnits y)
        {
            X = x;
            Y = y;
        }

        public HorizontalPanelUnits X { get; }

        public VerticalPanelUnits Y { get; }
    }
}
