using System;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// The USS class names of what the screens show (design sections 6, 7 and 9), the names CoveredLot.uss styles and the
    /// Client puts on its elements: the screens and their parts, the buttons, the lot and its cells, the pillars, the tiles
    /// with their covered face and their blocker mark, the ghosts, the badges, the Ahead links, the HUD with its bays, its
    /// lines, its ledger and its tags list, the end cards, and one class for each swatch. Every colour, size, border and
    /// spacing lives in the USS; these names are all the Client and the USS share.
    /// </summary>
    public static class StyleNames
    {
        /// <summary>The root of the screens: the page, inside the padding that keeps it clear of the screen's cutouts.</summary>
        public const string Screen = "cl-screen";

        /// <summary>The play screen: its top bar above its play area.</summary>
        public const string Play = "cl-play";

        /// <summary>The play screen's top bar: the Levels button, the entry's title and the Restart button.</summary>
        public const string TopBar = "cl-top-bar";

        /// <summary>A title: the entry's on the top bar, the game's on the level list.</summary>
        public const string Title = "cl-title";

        /// <summary>Every button the screens show, at least 48 x 48 units (design section 6).</summary>
        public const string Button = "cl-button";

        /// <summary>
        /// What the play screen shows under its top bar: the status line, the lot's frame, the bays and the HUD's lines,
        /// the ledger and the tags list, and an end card over all of them once the level is over.
        /// </summary>
        public const string PlayArea = "cl-play-area";

        /// <summary>The status line, which says what the last tap did.</summary>
        public const string Status = "cl-status";

        /// <summary>The frame the lot is fitted in: as much room as the play area leaves it, with no padding and no border.</summary>
        public const string LotFrame = "cl-lot-frame";

        /// <summary>A layer of the lot that fills its frame: the cells and pillars, the ghosts, the tiles, the Ahead links.</summary>
        public const string Layer = "cl-layer";

        /// <summary>The lot: the dark rectangle of W x H cells.</summary>
        public const string Lot = "cl-lot";

        /// <summary>A cell of the lot, empty or a pillar's.</summary>
        public const string Cell = "cl-cell";

        /// <summary>A pillar's cell, with its glyph.</summary>
        public const string Pillar = "cl-pillar";

        /// <summary>A pillar's glyph, "#".</summary>
        public const string Glyph = "cl-glyph";

        /// <summary>The place of a vehicle's box on the lot, which holds its tile or its ghost a little inside the box's edges.</summary>
        public const string Slot = "cl-slot";

        /// <summary>A vehicle still in the lot: the rounded box over its footprint, with its label.</summary>
        public const string Tile = "cl-tile";

        /// <summary>The label of a tile or of a ghost: its colour's letter, or "?" while it is shown covered, then its facing.</summary>
        public const string TileLabel = "cl-tile-label";

        /// <summary>A tile shown covered: grey, whatever its colour.</summary>
        public const string Covered = "cl-covered";

        /// <summary>The tile or pillar cell the last tap bumped into, outlined until the next tap.</summary>
        public const string Blocker = "cl-blocker";

        /// <summary>The outline of a vehicle that has left a covered level, at its start footprint, in its swatch.</summary>
        public const string Ghost = "cl-ghost";

        /// <summary>The number of a tagged vehicle, on its tile, on its ghost and on its row of the tags list.</summary>
        public const string Badge = "cl-badge";

        /// <summary>The layer that draws the Ahead links, in the colour, the line width and the arrowhead size the USS gives it.</summary>
        public const string AheadLinks = "cl-ahead-links";

        /// <summary>The bays row, bay 0 on the left.</summary>
        public const string Bays = "cl-bays";

        /// <summary>One bay: a light outline while it is empty, and its swatch while a vehicle waits in it.</summary>
        public const string Bay = "cl-bay";

        /// <summary>The letter of the colour that waits in a bay; empty while the bay is.</summary>
        public const string BayLetter = "cl-bay-letter";

        /// <summary>A line of the HUD under the bays: the bays that are free, and the peak against the par.</summary>
        public const string HudLine = "cl-hud-line";

        /// <summary>The ledger, on covered levels only: its heading and its pills.</summary>
        public const string Ledger = "cl-ledger";

        /// <summary>The heading of the ledger or of the tags list.</summary>
        public const string Heading = "cl-heading";

        /// <summary>One pill of the ledger, with a border in its colour's swatch: "R 1".</summary>
        public const string Pill = "cl-pill";

        /// <summary>The tags list under the board.</summary>
        public const string Tags = "cl-tags";

        /// <summary>One row of the tags list: a badge and the vehicle's tags.</summary>
        public const string TagRow = "cl-tag-row";

        /// <summary>The tags of one row of the tags list, after its badge: "not R, not Y".</summary>
        public const string TagLine = "cl-tag-line";

        /// <summary>An end card, the win card or the fail card: opaque over the play area, so it takes every tap there.</summary>
        public const string Card = "cl-card";

        /// <summary>An end card's heading: "Cleared!", or the sentence of a fail, "All bays are full."</summary>
        public const string CardHeading = "cl-card-heading";

        /// <summary>A line of the win card under its heading: the stars, and the peak against the par.</summary>
        public const string CardLine = "cl-card-line";

        /// <summary>The level list.</summary>
        public const string LevelList = "cl-level-list";

        /// <summary>The problem screen, which a catalogue that did not load shows.</summary>
        public const string Problems = "cl-problems";

        /// <summary>The problem screen's sentence.</summary>
        public const string Message = "cl-message";

        /// <summary>The class that paints a swatch: the fill of an open tile or a filled bay, the outline of a ghost or a pill.</summary>
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
