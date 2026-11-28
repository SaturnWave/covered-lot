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

        /// <summary>
        /// The exemption is no blank cheque: in Unity's build of the Client it covers exactly the three reads
        /// DevCultureSwitch makes (CurrentName reads the current culture, ReadCultures the current and the UI culture),
        /// and the Editor needs it nowhere.
        /// </summary>
        [Fact]
        public void The_reviewed_exemption_covers_exactly_the_three_culture_reads_of_Unity_built_DevCultureSwitch_and_nothing_in_the_Editor()
        {
            using AssemblyScanner client = UnityOutputs.Scan("CoveredLot.Client");
            using AssemblyScanner editor = UnityOutputs.Scan("CoveredLot.Editor");

            IReadOnlyList<string> covered = ReviewedExemptions.Covered(client.ApiPolicyViolations(MemberBansPolicy.Default))
                .Select(violation => MethodOf(violation.Location) + " reads " + MemberOf(violation.Reason))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(read => read, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(
                new[]
                {
                    "CurrentName reads get_CurrentCulture",
                    "ReadCultures reads get_CurrentCulture",
                    "ReadCultures reads get_CurrentUICulture",
                },
                covered);
            Assert.Empty(ReviewedExemptions.Covered(editor.ApiPolicyViolations(MemberBansPolicy.Default)));
        }

        /// <summary>The method of a location such as "CoveredLot.Client.DevCultureSwitch::ReadCultures+IL_0005": "ReadCultures".</summary>
        private static string MethodOf(string location)
        {
            int start = location.IndexOf("::", StringComparison.Ordinal) + 2;
            int end = location.IndexOf("+IL_", start, StringComparison.Ordinal);
            return end < 0 ? location.Substring(start) : location.Substring(start, end - start);
        }

        /// <summary>The member of a ban's reason such as "System.Globalization.CultureInfo::get_CurrentCulture(): ...": "get_CurrentCulture".</summary>
        private static string MemberOf(string reason)
        {
            int start = reason.IndexOf("::", StringComparison.Ordinal) + 2;
            int end = reason.IndexOf('(', start);
            return end < 0 ? reason.Substring(start) : reason.Substring(start, end - start);
        }

        private static void AssertNone(string heading, IReadOnlyList<Violation> violations) =>
            Assert.True(violations.Count == 0, heading + ":\n" + string.Join("\n", violations.Select(violation => "  " + violation)));
    }
}
