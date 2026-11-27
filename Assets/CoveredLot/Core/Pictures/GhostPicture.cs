using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// A vehicle that has left a covered level, as the board shows it (design section 9): an outline at its start footprint,
    /// in its swatch, with its true label ("Bv" for a blue vehicle facing south, covered or not at the start) and the badge
    /// of its tags. The tags describe the level's start, so the ghost keeps every Touch neighbour and Ahead reference in
    /// sight. It cannot be tapped.
    /// </summary>
    public sealed class GhostPicture
    {
        internal GhostPicture(LotLevel level, PieceId piece, Option<ClueNumber> badge)
        {
            Vehicle vehicle = level.VehicleOf(piece);
            Piece = piece;
            Box = new GridBox(level.FootprintOf(piece), level.Size);
            Swatch = Palette.SwatchOf(vehicle.Colour);
            Label = Copy.VehicleLabel(Palette.LetterOf(vehicle.Colour), vehicle.Facing);
            Badge = badge;
        }

        /// <summary>The vehicle that has left.</summary>
        public PieceId Piece { get; }

        /// <summary>The box of its start footprint.</summary>
        public GridBox Box { get; }

        public Swatch Swatch { get; }

        public string Label { get; }

        /// <summary>The number of the vehicle's row in the tags list; None when it carries no tag.</summary>
        public Option<ClueNumber> Badge { get; }
    }
}
