using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PuzzleCore;
using PuzzleCore.Jam;
using Xunit;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The first problem the catalogue gives for a broken level text, in the words of <see cref="CatalogueDescriptions"/>
    /// once the source is known. Closed: a malformed text or a level the loader refuses.
    /// </summary>
    internal abstract record ExpectedProblem
    {
        private ExpectedProblem()
        {
        }

        /// <summary>How <see cref="CatalogueDescriptions.Problem"/> writes this problem for a source it writes as <paramref name="describedSource"/>.</summary>
        public abstract string DescribedAt(string describedSource);

        public sealed record Malformed(int Line, LotTextProblem Problem) : ExpectedProblem
        {
            public override string DescribedAt(string describedSource) =>
                "LevelTextMalformed(" + describedSource + ", " + Line.ToString(CultureInfo.InvariantCulture) + ", " + Problem.ToString() + ")";
        }

        public sealed record Rejected(LoadProblem Problem, Option<PieceId> Piece) : ExpectedProblem
        {
            public override string DescribedAt(string describedSource) =>
                "LevelRejected(" + describedSource + ", " + Problem.ToString() + ", "
                + (Piece.TryGet(out PieceId id) ? "piece " + id.Value.ToString(CultureInfo.InvariantCulture) : "no piece") + ")";
        }
    }

    /// <summary>A level text broken in one way, and the first problem the engine finds in it.</summary>
    internal sealed record BrokenLevelText
    {
        public required string Text { get; init; }

        public required ExpectedProblem Problem { get; init; }
    }

    /// <summary>
    /// Level texts broken in one way each, nearly all made from the Example's text by one change, with the first problem the
    /// engine's reader or loader gives for each: every LotTextProblem that LotText.Read returns (it never returns BadColour,
    /// as an unknown colour letter makes its whole vehicle or clue bad) and every LoadProblem, and garbage besides. The
    /// catalogue must give each as a problem, never throw.
    /// </summary>
    internal static class BrokenLevelTexts
    {
        /// <summary>The LotTextProblem that LotText.Read never returns at the synced engine commit.</summary>
        public const LotTextProblem UnreachableTextProblem = LotTextProblem.BadColour;

        /// <summary>The broken texts by the name of the way each is broken; the names are the theory cases of the tests.</summary>
        public static IReadOnlyDictionary<string, BrokenLevelText> ByCase { get; } = Cases();

        public static TheoryData<string> CaseNames => new(ByCase.Keys.OrderBy(name => name, StringComparer.Ordinal));

        private static IReadOnlyDictionary<string, BrokenLevelText> Cases()
        {
            string example = ShippedLevels.Text(ShippedLevels.ExampleName);
            return new Dictionary<string, BrokenLevelText>(StringComparer.Ordinal)
            {
                ["an empty text"] = Malformed(string.Empty, 1, LotTextProblem.WrongHeader),
                ["carriage returns"] = Malformed(example.Replace("\n", "\r\n", StringComparison.Ordinal), 1, LotTextProblem.WrongHeader),
                ["a byte-order mark"] = Malformed("﻿" + example, 1, LotTextProblem.WrongHeader),
                ["a header with a trailing space"] = Malformed(example.Replace("pc-lot\n", "pc-lot \n", StringComparison.Ordinal), 1, LotTextProblem.WrongHeader),
                ["newlines only"] = Malformed("\n\n\n\n\n\n\n", 1, LotTextProblem.WrongHeader),
                ["NUL characters"] = Malformed("pc-lot\0\0", 1, LotTextProblem.WrongHeader),
                ["a long garbage text"] = Malformed(new string('x', 100_000), 1, LotTextProblem.WrongHeader),
                ["the header alone"] = Malformed("pc-lot", 2, LotTextProblem.MissingOrMisplacedLine),
                ["rules version 2"] = Malformed(example.Replace("r=1", "r=2", StringComparison.Ordinal), 2, LotTextProblem.UnsupportedRulesVersion),
                ["no size line"] = Malformed(example.Replace("size=6x6\n", string.Empty, StringComparison.Ordinal), 3, LotTextProblem.MissingOrMisplacedLine),
                ["a size in words"] = Malformed(example.Replace("size=6x6", "size=6by6", StringComparison.Ordinal), 3, LotTextProblem.BadSize),
                ["a size with a multiplication sign"] = Malformed(example.Replace("size=6x6", "size=6×6", StringComparison.Ordinal), 3, LotTextProblem.BadSize),
                ["a size of three numbers"] = Malformed(example.Replace("size=6x6", "size=6x6x6", StringComparison.Ordinal), 3, LotTextProblem.BadSize),
                ["bays with a leading zero"] = Malformed(example.Replace("bays=4", "bays=04", StringComparison.Ordinal), 4, LotTextProblem.BadNumber),
                ["bays above 999"] = Malformed(example.Replace("bays=4", "bays=1000", StringComparison.Ordinal), 4, LotTextProblem.BadNumber),
                ["a pillar that is no number"] = Malformed(example.Replace("pillars=4", "pillars=x", StringComparison.Ordinal), 5, LotTextProblem.BadNumber),
                ["a vehicle of an unknown colour"] = Malformed(example.Replace("B:1:2:N", "X:1:2:N", StringComparison.Ordinal), 6, LotTextProblem.BadVehicle),
                ["a vehicle of length 4"] = Malformed(example.Replace("B:1:2:N", "B:1:4:N", StringComparison.Ordinal), 6, LotTextProblem.BadVehicle),
                ["covers out of order"] = Malformed(example.Replace("covers=3,7", "covers=7,3", StringComparison.Ordinal), 7, LotTextProblem.BadCover),
                ["a cover of a vehicle that is not there"] = Malformed(example.Replace("covers=3,7", "covers=3,99", StringComparison.Ordinal), 7, LotTextProblem.BadCover),
                ["an unknown clue"] = Malformed(example.Replace("Not(3,R)", "Maybe(3,R)", StringComparison.Ordinal), 8, LotTextProblem.BadClue),
                ["a clue of an unknown colour"] = Malformed(example.Replace("Not(3,R)", "Not(3,X)", StringComparison.Ordinal), 8, LotTextProblem.BadClue),
                ["a clue on vehicle 24"] = Malformed(example.Replace("Not(3,R)", "Not(24,R)", StringComparison.Ordinal), 8, LotTextProblem.BadClue),
                ["a touch count above 255"] = Malformed(example.Replace("Not(3,R)", "Touch(3,Y,999)", StringComparison.Ordinal), 8, LotTextProblem.BadClue),
                ["a trailing newline"] = Malformed(example + "\n", 9, LotTextProblem.TrailingText),
                ["a ninth line"] = Malformed(example + "\nmore", 9, LotTextProblem.TrailingText),
                ["a lot 4 wide"] = Rejected(example.Replace("size=6x6", "size=4x6", StringComparison.Ordinal), LoadProblem.SizeOutOfRange),
                ["three bays"] = Rejected(example.Replace("bays=4", "bays=3", StringComparison.Ordinal), LoadProblem.BayCountOutOfRange),
                ["no vehicle"] = Rejected("pc-lot\nr=1\nsize=6x6\nbays=4\npillars=4\nvehicles=\ncovers=\nclues=", LoadProblem.VehicleCountOutOfRange),
                ["a pillar outside the lot"] = Rejected(example.Replace("pillars=4", "pillars=36", StringComparison.Ordinal), LoadProblem.PillarOutOfLot),
                ["vehicles out of anchor order"] = RejectedAt(
                    example.Replace("B:1:2:N;R:8:2:E", "R:8:2:E;B:1:2:N", StringComparison.Ordinal), LoadProblem.PiecesNotInAnchorOrder, 1),
                ["a vehicle outside the lot"] = RejectedAt(example.Replace("Y:33:2:E", "Y:35:2:E", StringComparison.Ordinal), LoadProblem.PieceOutOfLot, 8),
                ["overlapping vehicles"] = RejectedAt(example.Replace("Y:33:2:E", "Y:28:2:E", StringComparison.Ordinal), LoadProblem.PiecesOverlap, 8),
                ["a pillar on a path"] = RejectedAt(example.Replace("pillars=4", "pillars=4,11", StringComparison.Ordinal), LoadProblem.PillarBlocksPath, 1),
                ["vehicles waiting on each other"] = Rejected(
                    "pc-lot\nr=1\nsize=5x5\nbays=4\npillars=\nvehicles=R:0:2:E;R:2:2:W;R:10:2:E\ncovers=\nclues=", LoadProblem.BlockingCycle),
                ["a colour total that is no multiple of three"] = Rejected(
                    example.Replace("vehicles=B:1:2:N", "vehicles=R:1:2:N", StringComparison.Ordinal), LoadProblem.ColourTotalNotMultipleOfThree),
                ["a clue on a vehicle that is not there"] = RejectedAt(example.Replace("Not(3,R)", "Not(9,R)", StringComparison.Ordinal), LoadProblem.UnknownPiece, 9),
                ["a clue on an open vehicle"] = RejectedAt(example.Replace("Not(3,R)", "Not(0,R)", StringComparison.Ordinal), LoadProblem.ClueOnOpenVehicle, 0),
                ["an ahead clue on a clear path"] = RejectedAt(
                    example.Replace("Not(3,R)", "Ahead(3,Same)", StringComparison.Ordinal), LoadProblem.AheadWithoutBlocker, 3),
            };
        }

        private static BrokenLevelText Malformed(string text, int line, LotTextProblem problem) =>
            new() { Text = text, Problem = new ExpectedProblem.Malformed(line, problem) };

        private static BrokenLevelText Rejected(string text, LoadProblem problem) =>
            new() { Text = text, Problem = new ExpectedProblem.Rejected(problem, Option.None<PieceId>()) };

        private static BrokenLevelText RejectedAt(string text, LoadProblem problem, byte piece) =>
            new() { Text = text, Problem = new ExpectedProblem.Rejected(problem, Option.Some(new PieceId(piece))) };
    }

    /// <summary>Valid level texts with a property the catalogue tests need.</summary>
    internal static class FakeLevels
    {
        /// <summary>
        /// A valid 8x8 level with 4 bays that no order wins. At the start only R in the corner can leave; then B, Y and G of
        /// the top row, which face West, can leave one after the other, and every other vehicle waits, through those of
        /// columns 6 and 7, on G. So the first four parks are R, B, Y and G, four colours with no set to clear, and the
        /// fourth fills the bays: the engine's solver finds no par.
        /// </summary>
        public const string Unwinnable = "pc-lot\nr=1\nsize=8x8\nbays=4\npillars=\n"
            + "vehicles=R:0:2:W;B:2:2:W;Y:4:2:W;G:6:2:W;R:12:2:E;R:14:2:N;B:15:2:N;B:20:2:E;Y:30:2:N;Y:31:2:N;G:46:2:N;G:47:2:N\n"
            + "covers=\nclues=";
    }

    /// <summary>
    /// Presentation files the engine's reader refuses, each in its own way: not JSON, JSON of another shape, an unknown
    /// format version, and files whose levels break the rules of a presentation file. The catalogue must give each as
    /// PresentationUnreadable, never throw.
    /// </summary>
    internal static class BrokenPresentations
    {
        /// <summary>The broken files by the name of the way each is broken; the names are the theory cases of the tests.</summary>
        public static IReadOnlyDictionary<string, string> ByCase { get; } = Cases();

        public static TheoryData<string> CaseNames => new(ByCase.Keys.OrderBy(name => name, StringComparer.Ordinal));

        private static IReadOnlyDictionary<string, string> Cases()
        {
            string shipped = ShippedLevels.PresentationText();
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["an empty text"] = string.Empty,
                ["JSON cut short"] = "{\"format\":",
                ["null"] = "null",
                ["an array"] = "[]",
                ["an empty object"] = "{}",
                ["arrays 100 deep"] = new string('[', 100) + new string(']', 100),
                ["a byte-order mark"] = "﻿" + shipped,
                ["a long garbage text"] = new string('x', 100_000),
                ["format version 2"] = shipped.Replace("\"formatVersion\":1", "\"formatVersion\":2", StringComparison.Ordinal),
                ["another format"] = shipped.Replace("\"pc-presentation\"", "\"pc-record\"", StringComparison.Ordinal),
                ["an unknown game"] = shipped.Replace("\"game\":\"Jam\"", "\"game\":\"Chess\"", StringComparison.Ordinal),
                ["an unknown field"] = shipped.Replace("{\"format\":", "{\"extra\":1,\"format\":", StringComparison.Ordinal),
                ["two levels in one slot"] = shipped.Replace(",\"slot\":1}", ",\"slot\":0}", StringComparison.Ordinal),
                ["a level id that is not its slot's"] = shipped.Replace("\"levelId\":\"campaign-jam-0001\"", "\"levelId\":\"campaign-jam-0099\"", StringComparison.Ordinal),
                ["a slot beyond int"] = shipped.Replace(",\"slot\":1}", ",\"slot\":99999999999}", StringComparison.Ordinal),
                ["a Jam level without a par"] = FakePresentations.JamPackWithALevelWithoutPar,
            };
        }
    }
}
