using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The Client's own rules (<see cref="ClientRules"/>), which UnityChecks applies to Unity's build of the Client, shown
    /// on the samples compiled into this assembly (dotnet/Shared/Samples/ClientRuleSamples.cs): each planted use is reported
    /// at its method with the member and the rule it breaks, every rules type has a planted use, and the clean sample of
    /// Client-like code passes.
    /// </summary>
    public sealed class ClientRulesTests
    {
        private const string Samples = "CoveredLot.Shared.Samples.";
        private const string ViolationSamples = Samples + "ClientRuleSamples";
        private const string CleanSample = Samples + "CleanClientSample";

        private const string RulesCode = "the Client calls no rules code";
        private const string NumberText = "the Client formats and parses no number";
        private const string TextFromValues = "the Client builds no text from values";
        private const string Colour = "the Client constructs no colour";

        /// <summary>The rules' findings over this test assembly, made once: the assembly cannot change while its own tests run.</summary>
        private static readonly Lazy<SampleScan> Scan = new(ScanSamples);

        /// <summary>Each planted use: the sample method, the start of the member or value the finding names, and its rule.</summary>
        public static TheoryData<string, string, string> PlantedViolations => new()
        {
            { "RulesOf", "PuzzleCore.Jam.JamRules::.ctor(", RulesCode },
            { "OutcomeOf", "PuzzleCore.Play.Attempt`3<", RulesCode },
            { "LoadOf", "PuzzleCore.Jam.LotLoader::Load(", RulesCode },
            { "TextOf", "PuzzleCore.Jam.LotText::Write(", RulesCode },
            { "PeakOf", "PuzzleCore.Jam.LotSolver::PeakOf(", RulesCode },
            { "BuildOf", "PuzzleCore.Jam.LotPackBuilder::Build(", RulesCode },
            { "NeighboursOf", "PuzzleCore.Jam.LotClues::Neighbours(", RulesCode },
            { "FloatText", "System.Single::ToString(System.IFormatProvider)", NumberText },
            { "DoubleOfText", "System.Double::Parse(System.String, System.IFormatProvider)", NumberText },
            { "DecimalOfText", "System.Decimal::TryParse(", NumberText },
            { "IntegerTextWithAProvider", "System.Int32::ToString(System.IFormatProvider)", NumberText },
            { "FormattedWithAProvider", "System.String::Format(System.IFormatProvider, ", TextFromValues },
            { "ConcatenatedObject", "System.String::Concat(System.Object, System.Object)", TextFromValues },
            { "ConvertedText", "System.Convert::ToString(System.Int64, System.IFormatProvider)", TextFromValues },
            { "NewColour", "UnityEngine.Color::.ctor(", Colour },
            { "NewColour32", "UnityEngine.Color32::.ctor(", Colour },
            { "DefaultColour", "the default value of UnityEngine.Color:", Colour },
            { "DefaultColour32", "the default value of UnityEngine.Color32:", Colour },
        };

        [Theory]
        [MemberData(nameof(PlantedViolations))]
        public void Each_planted_use_is_reported_at_its_method_with_its_rule(string method, string found, string rule)
        {
            string at = ViolationSamples + "::" + method;

            IReadOnlyList<Violation> reported = Scan.Value.Violations.Where(violation => IsAt(violation, at)).ToList();

            Assert.True(reported.Any(violation => violation.Reason.StartsWith(found, StringComparison.Ordinal) && violation.Reason.Contains(": " + rule, StringComparison.Ordinal)),
                "No finding that starts with '" + found + "' and gives the rule '" + rule + "' at " + at + ". Found there:\n" + string.Join("\n", reported));
        }

        /// <summary>A rules type that nothing plants could be dropped from the rule with every test green.</summary>
        [Fact]
        public void Every_rules_type_has_a_planted_use()
        {
            IReadOnlyList<Violation> planted = Scan.Value.Violations.Where(violation => violation.Location.StartsWith(ViolationSamples + "::", StringComparison.Ordinal)).ToList();

            IReadOnlyList<string> unplanted = ClientRules.RulesTypes
                .Where(type => !planted.Any(violation => violation.Reason.StartsWith(type, StringComparison.Ordinal)))
                .ToList();

            Assert.NotEmpty(ClientRules.RulesTypes);
            Assert.True(unplanted.Count == 0, "No sample in ClientRuleSamples uses a member of " + string.Join(", ", unplanted) + ".");
        }

        [Fact]
        public void The_clean_client_sample_breaks_no_rule()
        {
            IReadOnlyList<LocatedMemberUse> read = Scan.Value.MemberUses.Where(use => use.Location.StartsWith(CleanSample + "::", StringComparison.Ordinal)).ToList();
            IReadOnlyList<Violation> reported = Scan.Value.Violations.Where(violation => violation.Location.StartsWith(CleanSample + "::", StringComparison.Ordinal)).ToList();

            // The rules read the clean sample's member uses: its string concatenation, comparison and join.
            Assert.Contains(read, use => use.Use.Display == "System.String::Concat(System.String, System.String)");
            Assert.True(reported.Count == 0, "The Client's rules report the clean sample:\n" + string.Join("\n", reported));
        }

        private static SampleScan ScanSamples()
        {
            using var samples = new AssemblyScanner(typeof(ClientRulesTests).Assembly.Location);
            return new SampleScan(ClientRules.Violations(samples), samples.LocatedMemberUses());
        }

        /// <summary>Whether the violation lies in the member (Type::Method), at an IL offset.</summary>
        private static bool IsAt(Violation violation, string member) => violation.Location.StartsWith(member + "+IL_", StringComparison.Ordinal);

        private sealed record SampleScan(IReadOnlyList<Violation> Violations, IReadOnlyList<LocatedMemberUse> MemberUses);
    }
}
