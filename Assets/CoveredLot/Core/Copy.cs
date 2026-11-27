using System;
using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Development;
using CoveredLot.Core.Layout;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Rules;

namespace CoveredLot.Core
{
    /// <summary>
    /// Every string the game shows lives here, so a change of wording is a one-line edit (design section 9). The copy keeps
    /// the text rules (<see cref="TextRules"/>): it is printable ASCII and holds none of the banned words, development lines
    /// included. It writes numbers with the invariant culture only, so a line reads the same on every device. It holds the
    /// game's title, the entries' titles, the buttons and the headings, the rules, the HUD's lines, the status lines, the
    /// end card's lines, the board's and the tags list's words and glyphs, the problem screen's lines, and the development
    /// line and its culture button. The pictures hold every string they show, so the members that only the pictures use are
    /// internal, and the public ones are what the screens show by themselves.
    /// </summary>
    public static class Copy
    {
        /// <summary>The game's name: the title the first screen shows, and the product name of the builds.</summary>
        public const string GameTitle = "Covered Lot";

        /// <summary>The problem screen's one sentence for players, whatever the catalogue's problems are.</summary>
        public const string LevelsNotLoaded = "Some levels could not be loaded.";

        /// <summary>The button that goes back to the level list: on the top bar, and on the win card and the fail card.</summary>
        public const string Levels = "Levels";

        /// <summary>The top bar's button that starts the level again.</summary>
        public const string Restart = "Restart";

        /// <summary>The heading of the ledger, before its pills.</summary>
        public const string StillCoveredHeading = "Still covered:";

        /// <summary>The heading of the tags list, above its rows.</summary>
        public const string TagsHeading = "Tags:";

        /// <summary>The win card's heading.</summary>
        public const string ClearedHeading = "Cleared!";

        /// <summary>The win card's button that plays the level again.</summary>
        public const string PlayAgain = "Play again";

        /// <summary>The win card's button that opens the next entry of the list; the card has none after the last entry.</summary>
        public const string NextLevel = "Next level";

        /// <summary>The fail card's button that starts the level again.</summary>
        public const string Retry = "Retry";

        /// <summary>The button that opens the rules: on the level list, and on the play screen's top bar.</summary>
        public const string RulesButton = "Rules";

        /// <summary>The heading of the rules.</summary>
        public const string RulesHeading = "How to play";

        /// <summary>The rules' button that closes them and shows again the screen they cover.</summary>
        public const string Back = "Back";

        /// <summary>What a covered vehicle's label shows in place of its colour's letter, as in "?v".</summary>
        internal const string HiddenColourGlyph = "?";

        /// <summary>What a pillar's cell shows.</summary>
        internal const string PillarGlyph = "#";

        /// <summary>The status line after a bump, whatever stands first on the vehicle's path.</summary>
        internal const string SomethingInTheWay = "Something is in its way.";

        /// <summary>The fail card's sentence when every bay is full, Failed(SlotsFull).</summary>
        internal const string AllBaysFull = "All bays are full.";

        /// <summary>The fail card's sentence when no vehicle can leave, Failed(NoLegalMove), which no loaded level reaches (BRIEF:47).</summary>
        internal const string NoMovePossible = "No move is possible.";

        /// <summary>The fail card's line under "All bays are full.": the rule that frees bays, as the bays left in view show it broken.</summary>
        internal const string ThreeOfOneColourLeave = "Three of one colour leave the bays together.";

        private const string PresentationLabel = "presentation";
        private const string ExampleLabel = "example";

        /// <summary>What stands between the tags of one row of the tags list, as in "not R, not Y".</summary>
        private const string TagSeparator = ", ";

        /// <summary>What stands between the parts of a status line, as in "Cover lifted: red. Three red cleared."</summary>
        private const string StatusSeparator = " ";

        /// <summary>What stands between the parts of the development line, as in "slot 0024 | par 4".</summary>
        private const string DevLineSeparator = " | ";

        /// <summary>
        /// The rules, one line each, in the simple UI's interim words (piece 13, on the user's word of 2026-11-28): the goal,
        /// the tap, the bays, the set of three, the fail, the covers with the ledger and the tags, and the stars. P10 settles
        /// the game's own goal text and the theme's words; until then these lines keep the text rules, as every string the
        /// game shows does.
        /// </summary>
        public static ReadOnlyArray<string> RulesLines { get; } = ReadOnlyArray.Of(
            "Get every piece out to win.",
            "Tap a piece to send it out the way its arrow points (^ > v <). It goes only if nothing is in its way.",
            "Each piece that leaves waits in one of the bays below.",
            "Three of one colour in the bays leave together and free their bays.",
            "If every bay is full, the level is lost.",
            "Grey pieces hide their colour until they leave. \"Still covered\" counts them by colour. A tag gives a clue: \"not R\" means not red.",
            "\"Most bays used\" counts the most bays full at once. Match the par for 3 stars.");

        /// <summary>What the level list and the top bar call an entry: "Example", or "Level 12" for the twelfth listed level.</summary>
        public static string TitleOf(EntryTitle title) => title switch
        {
            EntryTitle.Example => "Example",
            EntryTitle.Numbered numbered => "Level " + Number(numbered.Number.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(title), "Copy has no title for this kind of entry."),
        };

        /// <summary>The HUD's line of the bays that are free now (BRIEF:408): "4 bays free", and "1 bay free" for one.</summary>
        internal static string BaysFreeLine(int freeBays) => freeBays == 1 ? "1 bay free" : Number(freeBays) + " bays free";

        /// <summary>The peak against the par, on the HUD and on the win card (BRIEF:74): "Most bays used: 3 (par 3)".</summary>
        internal static string PeakLine(Peak peak, PeakPar par) =>
            "Most bays used: " + Number(peak.Bays.Value) + " (par " + Number(par.Bays.Value) + ")";

        /// <summary>The win card's stars: "3 stars", "2 stars" or "1 star".</summary>
        internal static string StarsLine(StarRating rating) => rating switch
        {
            StarRating.One => "1 star",
            StarRating.Two => "2 stars",
            StarRating.Three => "3 stars",
            _ => throw new ArgumentOutOfRangeException(nameof(rating), "Copy has no words for this star rating."),
        };

        /// <summary>A pill of the ledger: the colour's letter, then how many vehicles of it are still covered, "R 1".</summary>
        internal static string PillText(ColourId colour, int covered) => Palette.LetterOf(colour) + " " + Number(covered);

        /// <summary>
        /// The status line after the last tap (design section 9). It is empty before the first tap and after an ignored one,
        /// and reads "Something is in its way." after a bump. After a move it says what the move's events say, in the
        /// engine's order and joined by a space: "Cover lifted: blue." when a covered vehicle left, "Three blue cleared."
        /// when a set left the bays, both when one move did both, and nothing when it did neither.
        /// </summary>
        internal static string StatusLine(TapFeedback lastTap) => lastTap switch
        {
            TapFeedback.None or TapFeedback.Ignored => string.Empty,
            TapFeedback.Moved moved => StatusOf(moved.Events),
            TapFeedback.Bumped => SomethingInTheWay,
            _ => throw new ArgumentOutOfRangeException(nameof(lastTap), "Copy has no status line for this kind of tap feedback."),
        };

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

        /// <summary>
        /// The development line (design section 9), which only development builds show, its parts joined by " | ": in a
        /// level, "slot 0024 | par 4 | tr-TR | 1080x2400 | safe 0 63 1080 2247 | cell 47". It names the level by its slot in
        /// four digits or more, or as "example", then its par, the culture, the screen's width and height in pixels, the
        /// safe area as Screen.safeArea gives it (its left, its bottom, its width and its height, in pixels) and the side
        /// of the board's cells in panel units. Off a level it starts at the culture, and before the board's first layout it
        /// has no cell. An empty culture name, the invariant culture's, reads "invariant".
        /// </summary>
        public static string DevLine(DevFacts facts)
        {
            var parts = new List<string>();
            if (facts.Entry.TryGet(out CatalogueEntry? entry))
            {
                parts.Add(entry.Slot.TryGet(out LevelIndex slot) ? SlotLabel(slot) : ExampleLabel);
                parts.Add("par " + Number(entry.PeakPar.Bays.Value));
            }

            PixelRect safeArea = facts.SafeArea;
            parts.Add(facts.CultureName.Length == 0 ? "invariant" : facts.CultureName);
            parts.Add(Number(facts.Screen.Width.Value) + "x" + Number(facts.Screen.Height.Value));
            parts.Add("safe " + Number(safeArea.X.Value) + " " + Number(safeArea.Y.Value) + " "
                + Number(safeArea.Size.Width.Value) + " " + Number(safeArea.Size.Height.Value));
            if (facts.Board.TryGet(out BoardGeometry? board))
            {
                parts.Add("cell " + Number(board.CellSide));
            }

            return string.Join(DevLineSeparator, parts);
        }

        /// <summary>The level list's culture button in development builds, naming the culture set now: "Culture: device", "Culture: tr-TR", "Culture: sv-SE".</summary>
        public static string CultureButton(DevCulture culture) =>
            "Culture: " + (DevCultures.NameOf(culture).TryGet(out string? name) ? name : "device");

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

        /// <summary>The status line after a move with these events: what each event says, in the engine's order, joined by a space.</summary>
        private static string StatusOf(ReadOnlyArray<PuzzleEvent> events)
        {
            var parts = new List<string>();
            foreach (PuzzleEvent item in events)
            {
                if (StatusPartOf(item).TryGet(out string? part))
                {
                    parts.Add(part);
                }
            }

            return string.Join(StatusSeparator, parts);
        }

        /// <summary>
        /// What the status line says for one event of a move: a lifted cover and a cleared set, each with its colour's name;
        /// the exit itself, a win, a fail, a spare and a rewind say nothing. Every kind is named, so that a kind a later
        /// engine adds reaches the discard arm instead of passing unseen; the pin tests catch it at the next sync first.
        /// </summary>
        private static Option<string> StatusPartOf(PuzzleEvent item) => item switch
        {
            PuzzleEvent.CoverLifted lifted => Option.Some("Cover lifted: " + NameOf(lifted.Colour) + "."),
            PuzzleEvent.SetCleared cleared => Option.Some("Three " + NameOf(cleared.Colour) + " cleared."),
            PuzzleEvent.Exited or PuzzleEvent.Won or PuzzleEvent.Failed or PuzzleEvent.SpareAdded or PuzzleEvent.Rewound => Option.None<string>(),
            _ => throw new UnknownEngineCaseException(item),
        };

        /// <summary>A colour's name in a status line, by its swatch: "blue" for colour id 2.</summary>
        private static string NameOf(ColourId colour) => Palette.SwatchOf(colour) switch
        {
            Swatch.Red => "red",
            Swatch.Blue => "blue",
            Swatch.Yellow => "yellow",
            Swatch.Green => "green",
            Swatch.Pink => "pink",
            Swatch.Orange => "orange",
            _ => throw new ArgumentOutOfRangeException(nameof(colour), "Copy has no name for this colour's swatch."),
        };

        private static string LabelOf(LevelSource source) => source switch
        {
            LevelSource.Example => ExampleLabel,
            LevelSource.Listed listed => SlotLabel(listed.Slot),
            _ => throw new ArgumentOutOfRangeException(nameof(source), "Copy has no label for this kind of level source."),
        };

        /// <summary>A listed level's name in the development lines: its slot in four digits or more, "slot 0003".</summary>
        private static string SlotLabel(LevelIndex slot) => "slot " + slot.Value.ToString("D4", CultureInfo.InvariantCulture);

        /// <summary>" (piece 4)" for the piece a loader problem concerns, or nothing when it concerns none.</summary>
        private static string PieceNote(Option<PieceId> piece) =>
            piece.TryGet(out PieceId id) ? " (piece " + Number(id.Value) + ")" : string.Empty;

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
