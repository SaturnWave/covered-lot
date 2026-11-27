namespace CoveredLot.Core.Layout
{
    /// <summary>A size in panel units, a panel's or an element's as UI Toolkit lays it out: a width and a height.</summary>
    public readonly struct PanelSize
    {
        public PanelSize(HorizontalPanelUnits width, VerticalPanelUnits height)
        {
            Width = width;
            Height = height;
        }

        public HorizontalPanelUnits Width { get; }

        public VerticalPanelUnits Height { get; }
    }
}
