using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
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
    /// text of a floating-point number, a decimal and an integer whatever the provider, a number handed to a builder or a
    /// writer, text built from values (formats, joins, a FormattableString, a writer's composite format and a boxed value
    /// handed to a writer), and a colour made each way C# makes one. The colours are the stand-ins of UnityStandIns.cs.
    /// Compiled into the test assembly only; ClientRulesTests expects each method to be reported, and never runs it.
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

        /// <summary>StringBuilder's Append of a float: the member bans refuse it of an integer or an object only.</summary>
        public static string AppendedFloat(float bays) => new StringBuilder("bays ").Append(bays).ToString();

        /// <summary>StringBuilder's Insert of a double, whose value comes after its Int32 index.</summary>
        public static string InsertedDouble(double bays) => new StringBuilder(" bays").Insert(0, bays).ToString();

        public static void WrittenDecimal(TextWriter writer, decimal bays) => writer.Write(bays);

        public static void WrittenLineOfAFloat(TextWriter writer, float bays) => writer.WriteLine(bays);

        public static void ConsoleWrittenDouble(double bays) => Console.Write(bays);

        /// <summary>AppendFormat with a provider, here the device culture's: the member bans refuse it only without one.</summary>
        public static string AppendedFormatWithAProvider(float bays) =>
            new StringBuilder().AppendFormat(NumberFormatInfo.CurrentInfo, "{0} bays", bays).ToString();

        /// <summary>AppendJoin over numbers, which calls each one's ToString: the member bans name no AppendJoin.</summary>
        public static string AppendedJoinOfDoubles(double[] bays) => new StringBuilder().AppendJoin(", ", bays).ToString();

        /// <summary>AppendJoin of values passed one by one, which this compiler hands over as a ReadOnlySpan of objects.</summary>
        public static string AppendedJoinOfValues(object bays, object free) => new StringBuilder().AppendJoin(", ", bays, free).ToString();

        /// <summary>A FormattableString, as C# makes one from an interpolated string, formats with the device culture.</summary>
#pragma warning disable CA1305 // The device culture's text is what the sample plants.
        public static string FormattableText(double bays) => FormattableStringFactory.Create("{0} bays", bays).ToString();
#pragma warning restore CA1305

        /// <summary>A writer's composite format: the number reaches it as an object, so no value parameter names a number type.</summary>
        public static void WrittenFormat(TextWriter writer, double bays) => writer.Write("{0} bays", bays);

        public static void WrittenLineOfAFormat(TextWriter writer, float bays) => writer.WriteLine("{0:F1}", bays);

        public static void ConsoleWrittenLineOfAFormat(decimal bays) => Console.WriteLine("{0} bays", bays);

        /// <summary>Four values, which this compiler hands over as a ReadOnlySpan of objects, and C# 9 as an object array.</summary>
        public static void ConsoleWrittenFourValues(int red, int blue, int yellow, int green) =>
            Console.Write("{0} {1} {2} {3}", red, blue, yellow, green);

        /// <summary>A boxed number handed to a writer, which writes it with the device culture.</summary>
        public static void WrittenObject(TextWriter writer, object bays) => writer.Write(bays);

        public static Color NewColour() => new Color(1f, 0f, 0f, 1f);

        public static Color32 NewColour32() => new Color32(255, 0, 0, 255);

        /// <summary>A struct without a parameterless constructor: new Color() compiles to initobj and calls no constructor.</summary>
        public static Color DefaultColour() => new Color();

        public static Color32 DefaultColour32() => default;
    }

    /// <summary>
    /// Code of the kind the Client writes, which keeps every one of its rules: text joined from text, in a StringBuilder too,
    /// text written to a writer and the console, an ordinal comparison, a colour passed on as it was given, and integer
    /// arithmetic. ClientRulesTests expects none of it to be reported.
    /// </summary>
    internal static class CleanClientSample
    {
        private const string Title = "Covered Lot";

        public static string TitleWith(string suffix) => Title + suffix;

        public static bool IsTitle(string text) => string.Equals(text, Title, StringComparison.Ordinal);

        public static string Lines(IEnumerable<string> lines) => string.Join("\n", lines);

        /// <summary>Text built from text: StringBuilder's Append of a char and Insert of a string at an Int32 index.</summary>
        public static string Framed(string title) => new StringBuilder(title).Append(']').Insert(0, "[").ToString();

        /// <summary>Text handed to a writer and to the console: their string overloads take no value to format.</summary>
        public static void Written(TextWriter writer)
        {
            writer.WriteLine(Title);
            Console.Write(Title);
        }

        /// <summary>A colour handed on, as a view hands a resolved style's colour to the painter: no colour is made.</summary>
        public static Color Stroke(Color resolved) => resolved;

        public static int Cells(int width, int height) => width * height;
    }
}
