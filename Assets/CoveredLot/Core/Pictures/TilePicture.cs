using System;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// A vehicle still in the lot, as the board shows it (design section 9): its box, its face, its facing, its label (its
    /// colour's letter, or "?" while it is shown covered, then the glyph of its facing: "B^", "?v"), the badge of its tags,
    /// if it carries any, and its mark. Tapping the tile taps its vehicle.
    /// </summary>
    public sealed class TilePicture
    {
        internal TilePicture(LotLevel level, PieceId piece, TileFace face, Option<ClueNumber> badge, TileMark mark)
        {
            Vehicle vehicle = level.VehicleOf(piece);
            Piece = piece;
            Box = new GridBox(level.FootprintOf(piece), level.Size);
            Face = face;
            Facing = vehicle.Facing;
            Label = Copy.VehicleLabel(LetterShownOn(face), vehicle.Facing);
            Badge = badge;
            Mark = mark;
        }

        /// <summary>The vehicle, which a tap on the tile taps.</summary>
        public PieceId Piece { get; }

        public GridBox Box { get; }

        public TileFace Face { get; }

        /// <summary>The way the vehicle points, which it drives out along.</summary>
        public Facing Facing { get; }

        public string Label { get; }

        /// <summary>The number of the vehicle's row in the tags list; None when it carries no tag.</summary>
        public Option<ClueNumber> Badge { get; }

        public TileMark Mark { get; }

        private static string LetterShownOn(TileFace face) => face switch
        {
            TileFace.Open open => open.Letter,
            TileFace.Covered => Copy.HiddenColourGlyph,
            _ => throw new ArgumentOutOfRangeException(nameof(face), "A tile has no label for this kind of face."),
        };
    }
}
