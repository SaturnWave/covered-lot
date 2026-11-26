using System;
using System.Collections.Generic;
using System.Globalization;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Play;
using PuzzleCore.Randomness;
using PuzzleCore.Rules;
using UnityEngine;

namespace CoveredLot.Shared.Samples
{
    /// <summary>
    /// One use per method that the Client's own rules refuse (<see cref="ClientRules"/>): a member of each rules type, the
    /// text of a floating-point number, a decimal and an integer whatever the provider, text built from values, and a colour
    /// made each way C# makes one. The colours are the stand-ins of UnityStandIns.cs. Compiled into the test assembly only;
    /// ClientRulesTests expects each method to be reported, and never runs it.
    /// </summary>
    internal static class ClientRuleSamples
    {
        public static JamRules RulesOf(LotLevel level) => new JamRules(level);

        public static Outcome OutcomeOf(Attempt<LotState, Exit, ExitRejection> attempt) => attempt.Outcome;

        public static LoadResult LoadOf(LotDefinition definition) => LotLoader.Load(definition);

        public static string TextOf(LotLevel level) => LotText.Write(level);

        public static int PeakOf(LotLevel level, IReadOnlyList<Exit> path) => LotSolver.PeakOf(level, path);

        public static BuildResult BuildOf(PackId pack, LevelIndex index) => LotPackBuilder.Build(pack, index);

        public static IReadOnlyList<PieceId> NeighboursOf(LotLevel level, PieceId piece) => LotClues.Neighbours(level, piece);

        public static string FloatText(float value) => value.ToString(CultureInfo.InvariantCulture);

        public static double DoubleOfText(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        public static bool DecimalOfText(string text, out decimal value) =>
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

        /// <summary>An integer's text with the invariant culture: the member bans pass it, the Client's rule does not.</summary>
        public static string IntegerTextWithAProvider(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>String.Format with a provider: the member bans pass it, the Client's rule does not.</summary>
        public static string FormattedWithAProvider(int bays) => string.Format(CultureInfo.InvariantCulture, "{0} bays free", bays);

        public static string ConcatenatedObject(object bays) => string.Concat(bays, " bays free");

        public static string ConvertedText(long bays) => Convert.ToString(bays, CultureInfo.InvariantCulture);

        public static Color NewColour() => new Color(1f, 0f, 0f, 1f);

        public static Color32 NewColour32() => new Color32(255, 0, 0, 255);

        /// <summary>A struct without a parameterless constructor: new Color() compiles to initobj and calls no constructor.</summary>
        public static Color DefaultColour() => new Color();

        public static Color32 DefaultColour32() => default;
    }

    /// <summary>
    /// Code of the kind the Client writes, which keeps every one of its rules: text joined from text, an ordinal comparison,
    /// a colour passed on as it was given, and integer arithmetic. ClientRulesTests expects none of it to be reported.
    /// </summary>
    internal static class CleanClientSample
    {
        private const string Title = "Covered Lot";

        public static string TitleWith(string suffix) => Title + suffix;

        public static bool IsTitle(string text) => string.Equals(text, Title, StringComparison.Ordinal);

        public static string Lines(IEnumerable<string> lines) => string.Join("\n", lines);

        /// <summary>A colour handed on, as a view hands a resolved style's colour to the painter: no colour is made.</summary>
        public static Color Stroke(Color resolved) => resolved;

        public static int Cells(int width, int height) => width * height;
    }
}
