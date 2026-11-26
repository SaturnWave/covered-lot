using System.Collections.Generic;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The member bans of <see cref="UnityBoundApiPolicy"/> alone, with every platform type allowed, ported from the engine
    /// (SaturnWave/puzzle-engine, tests/PuzzleCore.Tests/Support/MemberBansPolicy.cs): for code that may use UnityEngine,
    /// files, the clock and floating point but must make no culture-dependent text, such as the Unity-built Client and
    /// Editor assemblies; no check runs it over them yet. Refused: culture-dependent text and text order (an interpolated
    /// integer included), the platform newline, randomized hashing, thread identity, enumeration in hash order and
    /// unstable sorts. The scan itself refuses native-sized integers and native calls under every policy.
    /// </summary>
    internal sealed class MemberBansPolicy : IPlatformApiPolicy
    {
        private MemberBansPolicy()
        {
        }

        public static MemberBansPolicy Default { get; } = new MemberBansPolicy();

        public string RefusedTypeReason => "is refused (never: this policy refuses no type)";

        public bool IsTypeAllowed(string typeName) => true;

        public IEnumerable<string> MemberViolations(MemberUse use, CallerContext caller) => UnityBoundApiPolicy.Default.BannedMemberUses(use, caller);
    }
}
