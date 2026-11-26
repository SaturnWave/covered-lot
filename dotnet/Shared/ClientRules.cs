using System;
using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The Client's own rules (design sections 8.5 and 10.4), which UnityChecks applies to Unity's build of the Client next
    /// to <see cref="MemberBansPolicy"/>. The Client paints the pictures Core makes and passes taps back, so it:
    /// <list type="bullet">
    /// <item><description>names no member of the rules engine's JamRules, Attempt`3, LotLoader, LotText, LotSolver,
    /// LotPackBuilder or LotClues;</description></item>
    /// <item><description>calls no ToString, Parse or TryParse of a System number type, whatever provider it passes, and
    /// hands no number to a member that writes its text: StringBuilder's Append and Insert, TextWriter's and Console's
    /// Write and WriteLine;</description></item>
    /// <item><description>calls no String.Format, with or without a provider, no String.Concat overload that takes an object,
    /// and no Convert.ToString;</description></item>
    /// <item><description>calls no UnityEngine.Color or Color32 constructor and sets neither to its default value
    /// (<c>new Color()</c> or <c>default</c>, which call no constructor).</description></item>
    /// </list>
    /// The member bans cover the ToString, Parse and TryParse of the integer types only, StringBuilder's Append and Insert of
    /// an integer or an object only, refuse String.Format only without a provider, and refuse nothing of Convert, TextWriter
    /// or Console; these rules close those gaps for the Client. Like the bans they name members, so these pass them: a number
    /// formatted through Object.ToString (boxed, or as a generic T, as Debug.Log formats its message), a culture-sensitive
    /// StringComparison value, Convert's parsing, DateTime text; a mention of a rules type that uses none of its members (a
    /// local, a cast, typeof); and a colour that no constructor makes, such as Color's static colours (Color.red), Color.Lerp,
    /// the conversion from Color32 or ColorUtility. Each rule is shown on planted samples (dotnet/Shared/Samples/ClientRuleSamples.cs)
    /// by ClientRulesTests.
    /// </summary>
    internal static class ClientRules
    {
        private const string Constructor = ".ctor";
        private const string CallsRulesCode = "the Client calls no rules code; Core drives the engine";
        private const string MakesNumberText = "the Client formats and parses no number; Core makes every text it shows";
        private const string BuildsTextFromValues = "the Client builds no text from values; Core makes every text it shows";
        private const string ConstructsColour = "the Client constructs no colour; every colour lives in CoveredLot.uss";

        /// <summary>The members that build text from their arguments, whichever overload: Type::Member.</summary>
        private static readonly IReadOnlySet<string> TextFromValues = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.String::Format", "System.Convert::ToString",
        };

        private static readonly IReadOnlySet<string> NumberTextMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "ToString", "Parse", "TryParse",
        };

        /// <summary>
        /// The members that write the text of a value they are given (Type::Member), each with the position of the parameter
        /// that takes the value: StringBuilder.Insert takes its index first, and an Int32 index makes no text.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, int> ValueParameterOfWriters = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["System.Text.StringBuilder::Append"] = 0,
            ["System.Text.StringBuilder::Insert"] = 1,
            ["System.IO.TextWriter::Write"] = 0,
            ["System.IO.TextWriter::WriteLine"] = 0,
            ["System.Console::Write"] = 0,
            ["System.Console::WriteLine"] = 0,
        };

        /// <summary>The System number types: the integers, the floating-point types and decimal, native-sized and big integers.</summary>
        private static readonly IReadOnlySet<string> NumberTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Byte", "System.SByte", "System.Int16", "System.UInt16", "System.Int32", "System.UInt32",
            "System.Int64", "System.UInt64", "System.Int128", "System.UInt128", "System.IntPtr", "System.UIntPtr",
            "System.Half", "System.Single", "System.Double", "System.Decimal", "System.Numerics.BigInteger",
        };

        /// <summary>The parameter types of a String.Concat overload that takes objects, as signatures display them.</summary>
        private static readonly IReadOnlySet<string> ObjectParameters = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Object", "System.Object[]", "System.ReadOnlySpan`1<System.Object>",
        };

        private static readonly IReadOnlySet<string> ColourTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "UnityEngine.Color", "UnityEngine.Color32",
        };

        /// <summary>The rules engine's types whose members the Client may not name, generic arguments stripped.</summary>
        public static IReadOnlyList<string> RulesTypes { get; } = new[]
        {
            "PuzzleCore.Jam.JamRules", "PuzzleCore.Play.Attempt`3", "PuzzleCore.Jam.LotLoader", "PuzzleCore.Jam.LotText",
            "PuzzleCore.Jam.LotSolver", "PuzzleCore.Jam.LotPackBuilder", "PuzzleCore.Jam.LotClues",
        };

        /// <summary>Every use that breaks one of the rules, at its method and IL offset, with the member and the rule.</summary>
        public static IReadOnlyList<Violation> Violations(AssemblyScanner client)
        {
            IEnumerable<Violation> memberUses = client.LocatedMemberUses().SelectMany(ViolationsOf);
            IEnumerable<Violation> defaultColours = client.DefaultedStructs()
                .Where(site => ColourTypes.Contains(site.TypeName))
                .Select(site => new Violation { Location = site.Location, Reason = "the default value of " + site.TypeName + ": " + ConstructsColour });
            return memberUses.Concat(defaultColours).ToList();
        }

        private static IEnumerable<Violation> ViolationsOf(LocatedMemberUse located) =>
            ReasonsAgainst(located.Use).Select(reason => new Violation { Location = located.Location, Reason = located.Use.Display + ": " + reason });

        private static IEnumerable<string> ReasonsAgainst(MemberUse use)
        {
            string type = use.DeclaringType.DefinitionName;
            if (RulesTypes.Contains(type, StringComparer.Ordinal))
            {
                yield return CallsRulesCode;
            }

            bool writesANumber = ValueParameterOfWriters.TryGetValue(type + "::" + use.MemberName, out int valueParameter)
                && valueParameter < use.Parameters.Count
                && NumberTypes.Contains(use.Parameters[valueParameter].Display);
            if ((NumberTypes.Contains(type) && NumberTextMembers.Contains(use.MemberName)) || writesANumber)
            {
                yield return MakesNumberText;
            }

            bool concatenatesObjects = type == "System.String" && use.MemberName == "Concat"
                && use.Parameters.Any(parameter => ObjectParameters.Contains(parameter.Display));
            if (TextFromValues.Contains(type + "::" + use.MemberName) || concatenatesObjects)
            {
                yield return BuildsTextFromValues;
            }

            if (ColourTypes.Contains(type) && use.MemberName == Constructor)
            {
                yield return ConstructsColour;
            }
        }
    }
}
