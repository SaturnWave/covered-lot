namespace CoveredLot.Core.Pictures
{
    /// <summary>How a tile or a pillar cell is marked (design section 9).</summary>
    public enum TileMark
    {
        /// <summary>No mark.</summary>
        Plain,

        /// <summary>
        /// The first blocker the engine named for the last tap, which bumped: the vehicle or pillar cell on the tapped
        /// vehicle's path. It keeps the mark until the next tap, and a new session starts with none.
        /// </summary>
        Blocker,
    }
}
