using System;
using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core
{
    /// <summary>
    /// Every string the game shows lives here, so a change of wording is a one-line edit (design section 9). The copy is
    /// printable ASCII and holds none of the words the brief keeps out of the game's text, development lines included. It
    /// writes numbers with the invariant culture only, so a line reads the same on every device. It holds the game's title,
    /// the problem screen's lines, and the board's and the tags list's words and glyphs for now; later pieces add the rest.
    /// </summary>
    public static class Copy
    {
        /// <summary>The game's name: the title the first screen shows, and the product name of the builds.</summary>
        public const string GameTitle = "Covered Lot";

        /// <summary>The problem screen's one sentence for players, whatever the catalogue's problems are.</summary>
        public const string LevelsNotLoaded = "Some levels could not be loaded.";

        /// <summary>What a covered vehicle's label shows in place of its colour's letter, as in "?v".</summary>
        internal const string HiddenColourGlyph = "?";

        /// <summary>What a pillar's cell shows.</summary>
        internal const string PillarGlyph = "#";

        private const string PresentationLabel = "presentation";
        private const string ExampleLabel = "example";

        /// <summary>What stands between the tags of one row of the tags list, as in "not R, not Y".</summary>
        private const string TagSeparator = ", ";

        /// <summary>
        /// What the tags list says for one tag (design section 9): "not R" for a Not tag, "same as ahead" or "differs from
        /// ahead" for an Ahead tag, and "touches 2 Y" for a Touch tag, each colour by its letter.
        /// </summary>
        internal static string TagText(LotClue clue) => clue switch
        {
            LotClue.Not not => "not " + Palette.LetterOf(not.Colour),
            LotClue.Ahead ahead => Of(ahead.Relation),
            LotClue.Touch touch => "touches " + Number(touch.Count) + " " + Palette.LetterOf(touch.Colour),
            _ => throw new UnknownEngineCaseException(clue),
        };

        /// <summary>
        /// The development-only line for one catalogue problem: its source, a colon, and what is wrong, as in
        /// "slot 0003: line 1, wrong header". A listed level is named by its slot in four digits or more, never by its id,
        /// which names the game's pack; the Example and the presentation file name themselves.
        /// </summary>
        public static string ProblemLine(CatalogueProblem problem) => problem switch
        {
            CatalogueProblem.PresentationUnreadable => PresentationLabel + ": missing or unreadable",
            CatalogueProblem.NotAJamPack => PresentationLabel + ": a pack of another game",
            CatalogueProblem.LevelTextMissing missing => LabelOf(missing.Source) + ": text missing",
            CatalogueProblem.LevelTextMalformed malformed =>
                LabelOf(malformed.Source) + ": line " + Number(malformed.Line) + ", " + Of(malformed.Problem),
            CatalogueProblem.LevelRejected rejected => LabelOf(rejected.Source) + ": " + Of(rejected.Problem) + PieceNote(rejected.Piece),
            CatalogueProblem.PeakParMissing missing => LabelOf(missing.Source) + ": no par in the presentation",
            CatalogueProblem.ExamplePeakParNotFound => ExampleLabel + ": no par found",
            _ => throw new ArgumentOutOfRangeException(nameof(problem), "Copy has no line for this kind of catalogue problem."),
        };

        /// <summary>What a development line says for each way the engine finds a pc-lot text malformed; never the enum's own name.</summary>
        public static string Of(LotTextProblem problem) => problem switch
        {
            LotTextProblem.WrongHeader => "wrong header",
            LotTextProblem.UnsupportedRulesVersion => "unsupported rules version",
            LotTextProblem.MissingOrMisplacedLine => "missing or misplaced line",
            LotTextProblem.BadNumber => "bad number",
            LotTextProblem.BadSize => "bad size",
            LotTextProblem.BadVehicle => "bad piece",
            LotTextProblem.BadColour => "bad colour",
            LotTextProblem.BadCover => "bad cover",
            LotTextProblem.BadClue => "bad clue",
            LotTextProblem.TrailingText => "trailing text",
            _ => throw new UnknownEngineCaseException(problem),
        };

        /// <summary>What a development line says for each way the engine's loader refuses a level; never the enum's own name.</summary>
        public static string Of(LoadProblem problem) => problem switch
        {
            LoadProblem.BlockingCycle => "pieces wait on each other in a cycle",
            LoadProblem.PillarBlocksPath => "pillar on a path",
            LoadProblem.AheadWithoutBlocker => "ahead clue on a clear path",
            LoadProblem.ColourTotalNotMultipleOfThree => "colour total not a multiple of three",
            LoadProblem.BayCountOutOfRange => "bay count out of range",
            LoadProblem.PieceOutOfLot => "piece outside the lot",
            LoadProblem.PiecesOverlap => "pieces overlap",
            LoadProblem.SizeOutOfRange => "size out of range",
            LoadProblem.VehicleCountOutOfRange => "piece count out of range",
            LoadProblem.PiecesNotInAnchorOrder => "pieces not in anchor order",
            LoadProblem.PillarOutOfLot => "pillar outside the lot",
            LoadProblem.UnknownPiece => "no such piece",
            LoadProblem.ClueOnOpenVehicle => "clue on an open piece",
            _ => throw new UnknownEngineCaseException(problem),
        };

        /// <summary>The glyph of a facing in a vehicle's label, as in the brief's legend: ^ north, &gt; east, v south, &lt; west.</summary>
        internal static string FacingGlyph(Facing facing) => facing switch
        {
            Facing.North => "^",
            Facing.East => ">",
            Facing.South => "v",
            Facing.West => "<",
            _ => throw new UnknownEngineCaseException(facing),
        };

        /// <summary>A vehicle's label: the letter it shows, its colour's or "?", then the glyph of its facing: "B^", "?v".</summary>
        internal static string VehicleLabel(string letter, Facing facing) => letter + FacingGlyph(facing);

        /// <summary>The tags of one row of the tags list, as the row shows them after its badge: "not R, not Y".</summary>
        internal static string TagLine(IReadOnlyList<string> tags) => string.Join(TagSeparator, tags);

        /// <summary>A badge's number: "1".</summary>
        internal static string BadgeText(ClueNumber badge) => Number(badge.Value);

        private static string Of(AheadRelation relation) => relation switch
        {
            AheadRelation.Same => "same as ahead",
            AheadRelation.Differs => "differs from ahead",
            _ => throw new UnknownEngineCaseException(relation),
        };

        private static string LabelOf(LevelSource source) => source switch
        {
            LevelSource.Example => ExampleLabel,
            LevelSource.Listed listed => "slot " + listed.Slot.Value.ToString("D4", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(source), "Copy has no label for this kind of level source."),
        };

        /// <summary>" (piece 4)" for the piece a loader problem concerns, or nothing when it concerns none.</summary>
        private static string PieceNote(Option<PieceId> piece) =>
            piece.TryGet(out PieceId id) ? " (piece " + Number(id.Value) + ")" : string.Empty;

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
