using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Shared;
using CoveredLot.UnityChecks.Support;
using Xunit;

namespace CoveredLot.UnityChecks
{
    /// <summary>
    /// Piece 3b's scans on what Unity built (design section 10.4). Unity's build of Core holds no floating point and uses
    /// nothing the full Unity-bound API policy refuses (its allowlist, its member bans and its unordered-sequence rules),
    /// as its .NET build already shows; PuzzleCore and PuzzleCore.Jam, the engine DLLs of Assets/Generated/PuzzleCore that
    /// its asmdef names, are its siblings, whose types are no platform APIs. The Client and the Editor, which may use
    /// UnityEngine, files and floats, use none of the members the policy bans (MemberBansPolicy), apart from the one
    /// reviewed exemption (ReviewedExemptions): the Client's DevCultureSwitch reads the culture the game runs in. That
    /// exemption is shown to cover reads Unity's build of the Client really makes, and nothing in the Editor's. The
    /// policy's known gaps are in its remarks; CoveredLot.Core.Tests shows each scan on planted samples (ApiPolicyTests
    /// there) and the exemption on planted violations (ReviewedExemptionsTests).
    /// </summary>
    public sealed class ApiPolicyTests
    {
        private const string Core = "CoveredLot.Core";

        /// <summary>What Core's asmdef gives it in Unity: netstandard, which every assembly gets, and its two precompiled references.</summary>
        private static readonly string[] CoreReferences = { "PuzzleCore", "PuzzleCore.Jam", "netstandard" };

        public static TheoryData<string> ClientAndEditor => new() { "CoveredLot.Client", "CoveredLot.Editor" };

        [Fact]
        public void Unity_built_Core_references_only_netstandard_and_the_engine_dlls()
        {
            using AssemblyScanner core = UnityOutputs.Scan(Core);

            IReadOnlyList<string> unexpected = core.ReferencedAssemblyNames().Except(CoreReferences, StringComparer.Ordinal).ToList();

            Assert.True(unexpected.Count == 0, "Unity's build of " + Core + " references " + string.Join(", ", unexpected) + ", which its asmdef does not give it.");
        }

        [Fact]
        public void Unity_built_Core_holds_no_floating_point()
        {
            using AssemblyScanner core = UnityOutputs.Scan(Core);

            AssertNone("Unity's build of " + Core + " holds floating point", core.FloatingPointUses());
        }

        [Fact]
        public void Unity_built_Core_uses_only_what_the_unity_bound_policy_allows()
        {
            using AssemblyScanner core = UnityOutputs.Scan(Core);

            AssertNone("Unity's build of " + Core + " uses what the Unity-bound API policy refuses", core.ApiPolicyViolations(UnityBoundApiPolicy.Default));
        }

        [Theory]
        [MemberData(nameof(ClientAndEditor))]
        public void Unity_built_Client_and_Editor_use_no_banned_member_beyond_the_reviewed_exemption(string name)
        {
            using AssemblyScanner scanner = UnityOutputs.Scan(name);

            AssertNone("Unity's build of " + name + " uses what the member bans refuse", ReviewedExemptions.Without(scanner.ApiPolicyViolations(MemberBansPolicy.Default)));
        }

        /// <summary>The exemption is no blank cheque: it covers both culture reads that DevCultureSwitch really makes, and the Editor needs it nowhere.</summary>
        [Fact]
        public void The_reviewed_exemption_covers_both_culture_reads_of_Unity_built_DevCultureSwitch_and_nothing_in_the_Editor()
        {
            using AssemblyScanner client = UnityOutputs.Scan("CoveredLot.Client");
            using AssemblyScanner editor = UnityOutputs.Scan("CoveredLot.Editor");

            IReadOnlyList<Violation> covered = ReviewedExemptions.Covered(client.ApiPolicyViolations(MemberBansPolicy.Default));

            Assert.Contains(covered, violation => violation.Reason.StartsWith("System.Globalization.CultureInfo::get_CurrentCulture(): ", StringComparison.Ordinal));
            Assert.Contains(covered, violation => violation.Reason.StartsWith("System.Globalization.CultureInfo::get_CurrentUICulture(): ", StringComparison.Ordinal));
            Assert.Empty(ReviewedExemptions.Covered(editor.ApiPolicyViolations(MemberBansPolicy.Default)));
        }

        private static void AssertNone(string heading, IReadOnlyList<Violation> violations) =>
            Assert.True(violations.Count == 0, heading + ":\n" + string.Join("\n", violations.Select(violation => "  " + violation)));
    }
}
