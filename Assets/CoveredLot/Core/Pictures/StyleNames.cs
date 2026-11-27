using System;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The USS class names of what the board and the tags list show (design section 9), the names CoveredLot.uss styles and
    /// the Client puts on its elements: the lot and its cells, the pillars, the tiles with their covered face and their
    /// blocker mark, the ghosts, the badges, the Ahead links, the tags list and its rows, and one class for each swatch.
    /// Every colour, size and border lives in the USS; these names are all the Client and the USS share.
    /// </summary>
    public static class StyleNames
    {
        /// <summary>The lot: the dark rectangle of W x H cells.</summary>
        public const string Lot = "cl-lot";

        /// <summary>A cell of the lot, empty or a pillar's.</summary>
        public const string Cell = "cl-cell";

        /// <summary>A pillar's cell, with its glyph.</summary>
        public const string Pillar = "cl-pillar";

        /// <summary>A vehicle still in the lot: the rounded box over its footprint, with its label.</summary>
        public const string Tile = "cl-tile";

        /// <summary>A tile shown covered: grey, whatever its colour.</summary>
        public const string Covered = "cl-covered";

        /// <summary>The tile or pillar cell the last tap bumped into, outlined until the next tap.</summary>
        public const string Blocker = "cl-blocker";

        /// <summary>The outline of a vehicle that has left a covered level, at its start footprint, in its swatch.</summary>
        public const string Ghost = "cl-ghost";

        /// <summary>The number of a tagged vehicle, on its tile, on its ghost and on its row of the tags list.</summary>
        public const string Badge = "cl-badge";

        /// <summary>The layer that draws the Ahead links, in the colour the USS gives it.</summary>
        public const string AheadLinks = "cl-ahead-links";

        /// <summary>The tags list under the board.</summary>
        public const string Tags = "cl-tags";

        /// <summary>One row of the tags list: a badge and the vehicle's tags.</summary>
        public const string TagRow = "cl-tag-row";

        /// <summary>The class that paints a swatch: the fill of an open tile, the outline of a ghost.</summary>
        public static string Of(Swatch swatch) => swatch switch
        {
            Swatch.Red => "cl-swatch-red",
            Swatch.Blue => "cl-swatch-blue",
            Swatch.Yellow => "cl-swatch-yellow",
            Swatch.Green => "cl-swatch-green",
            Swatch.Pink => "cl-swatch-pink",
            Swatch.Orange => "cl-swatch-orange",
            _ => throw new ArgumentOutOfRangeException(nameof(swatch), "There is no class for this swatch."),
        };
    }
}
