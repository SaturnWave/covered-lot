using System;
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
    /// ("cell 27 to cell 24"), worked out again through the engine's LotSize. A test compares lists of them, so a failure
    /// shows every line that differs.
    /// </summary>
    internal static class PictureDescriptions
    {
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

        /// <summary>"Open(Blue, B)", or "Covered".</summary>
        public static string Face(TileFace face) => face switch
        {
            TileFace.Open open => "Open(" + open.Swatch.ToString() + ", " + open.Letter + ")",
            TileFace.Covered => "Covered",
            _ => throw new ArgumentOutOfRangeException(nameof(face), "No description for " + face.GetType().Name + "."),
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
