namespace CoveredLot.Core
{
    /// <summary>
    /// Every string the game shows lives here, so a change of wording is a one-line edit (design section 9). The copy
    /// is printable ASCII and is never built from numbers here; for now it holds only the game's title, and later
    /// pieces add the rest.
    /// </summary>
    public static class Copy
    {
        /// <summary>The game's name: the title the first screen shows, and the product name of the builds.</summary>
        public const string GameTitle = "Covered Lot";
    }
}
