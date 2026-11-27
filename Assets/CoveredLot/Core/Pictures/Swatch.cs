namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The colour a vehicle shows on screen, one for each level colour, in the order of the colour ids 1 to 6 (design
    /// section 9). CoveredLot.uss paints each through its class (StyleNames.Of); Core never names a colour value itself.
    /// </summary>
    public enum Swatch
    {
        /// <summary>Colour id 1, letter R.</summary>
        Red,

        /// <summary>Colour id 2, letter B.</summary>
        Blue,

        /// <summary>Colour id 3, letter Y.</summary>
        Yellow,

        /// <summary>Colour id 4, letter G.</summary>
        Green,

        /// <summary>Colour id 5, letter P.</summary>
        Pink,

        /// <summary>Colour id 6, letter O.</summary>
        Orange,
    }
}
