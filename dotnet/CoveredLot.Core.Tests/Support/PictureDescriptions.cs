using System.Globalization;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The pictures written out as plain lines in the tests' own words, as the plan writes them: a tile as its vehicle, its
    /// box (row, column, rows x columns) and its label ("3 (2,2,2x1) ?v"), a pillar as its cell and its glyph ("(0,4) #"),
    /// a row of the tags list as its badge and its line ("1: not R, not Y"), and a link as the indexes of its two cells
    /// ("cell 27 to cell 24"), worked out again through the engine's LotSize; a bay as its letter and swatch ("B Blue") or
    /// "empty", a ledger pill as its text and swatch ("R 1 Red"), and an end card as its case and its lines
    /// ("Cleared(Three, 3 stars, Most bays used: 3 (par 3))"). A test compares lists of them, so a failure shows every line
    /// that differs.
    /// </summary>
    internal static class PictureDescriptions
    {
        /// <summary>"B Blue", or "empty". A kind of bay the tests do not know is described by its type, so a comparison fails on it without a throw.</summary>
        public static string Bay(BayPicture bay) => bay switch
        {
            BayPicture.Filled filled => filled.Letter + " " + filled.Swatch.ToString(),
            BayPicture.Empty => "empty",
            _ => "an unknown kind of bay, " + bay.GetType().Name,
        };

        /// <summary>"R 1 Red".</summary>
        public static string Pill(LedgerPill pill) => pill.Text + " " + pill.Swatch.ToString();

        /// <summary>
        /// "Playing", "Cleared(Three, 3 stars, Most bays used: 3 (par 3))", "BaysFull(All bays are full.)" or "NoMoveLeft(No
        /// move is possible.)". A kind of card the tests do not know is described by its type, so a comparison fails on it
        /// without a throw.
        /// </summary>
        public static string End(EndPicture end) => end switch
        {
            EndPicture.Playing => "Playing",
            EndPicture.Cleared cleared => "Cleared(" + cleared.Rating.ToString() + ", " + cleared.StarsLine + ", " + cleared.PeakLine + ")",
            EndPicture.BaysFull full => "BaysFull(" + full.Text + ")",
            EndPicture.NoMoveLeft noMove => "NoMoveLeft(" + noMove.Text + ")",
            _ => "an unknown kind of card, " + end.GetType().Name,
        };

        /// <summary>"3 (2,2,2x1) ?v".</summary>
        public static string Tile(TilePicture tile) => Number(tile.Piece.Value) + " " + Box(tile.Box) + " " + tile.Label;

        /// <summary>"3 (2,2,2x1) Bv Blue badge 1".</summary>
        public static string Ghost(GhostPicture ghost) =>
            Number(ghost.Piece.Value) + " " + Box(ghost.Box) + " " + ghost.Label + " " + ghost.Swatch.ToString() + " " + Badge(ghost.Badge);

        /// <summary>"(0,4) #".</summary>
        public static string Pillar(PillarPicture pillar) => Cell(pillar.Cell) + " " + pillar.Label;

        /// <summary>"1: not R, not Y".</summary>
        public static string Row(ClueRow row) => row.Badge.Text + ": " + row.Line;

        /// <summary>"(row,column,rows x columns)": "(2,2,2x1)".</summary>
        public static string Box(GridBox box) =>
            "(" + Number(box.Row.Value) + "," + Number(box.Column.Value) + "," + Number(box.Rows) + "x" + Number(box.Columns) + ")";

        /// <summary>"(row,column)": "(0,4)".</summary>
        public static string Cell(GridCell cell) => "(" + Number(cell.Row.Value) + "," + Number(cell.Column.Value) + ")";

        /// <summary>"Open(Blue, B)", or "Covered". A kind of face the tests do not know is described by its type, so a comparison fails on it without a throw.</summary>
        public static string Face(TileFace face) => face switch
        {
            TileFace.Open open => "Open(" + open.Swatch.ToString() + ", " + open.Letter + ")",
            TileFace.Covered => "Covered",
            _ => "an unknown kind of face, " + face.GetType().Name,
        };

        /// <summary>"badge 1", or "no badge".</summary>
        public static string Badge(Option<ClueNumber> badge) => badge.TryGet(out ClueNumber number) ? "badge " + number.Text : "no badge";

        /// <summary>"cell 27 to cell 24": each end's cell index, as the engine's LotSize gives it for the end's row and column.</summary>
        public static string Link(AheadLinkPicture link, LotSize size) =>
            "cell " + Number(IndexOf(link.From, size)) + " to cell " + Number(IndexOf(link.To, size));

        /// <summary>The cell's index, as the engine's LotSize gives it for the cell's row and column.</summary>
        public static int IndexOf(GridCell cell, LotSize size) => size.CellAt(cell.Row, cell.Column).Index;

        public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
