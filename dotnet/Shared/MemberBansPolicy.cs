using System.Collections.Generic;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The member bans of <see cref="UnityBoundApiPolicy"/> alone, with every platform type allowed, ported from the engine
    /// (SaturnWave/puzzle-engine, tests/PuzzleCore.Tests/Support/MemberBansPolicy.cs): for code that may use UnityEngine,
    /// files and the clock, such as the Unity-built Client and Editor assemblies; no check runs it over them yet. Refused:
    /// the members that policy bans, which are integer formatting and parsing without a provider, string.Format,
    /// AppendFormat and an interpolated value without one, string.Concat and string.Join of objects, the casing,
    /// comparison, search and ordering of text by the culture, CultureInfo.CurrentCulture and CurrentUICulture, the
    /// platform newline, GetHashCode of objects, strings and the comparers, Environment.CurrentManagedThreadId,
    /// enumeration in hash order and unstable sorts; under every policy the scan also refuses native-sized integers and
    /// native calls. Not refused: what only the allowlist keeps out of Unity-bound code, such as text and parsing of float,
    /// double, decimal and DateTime, Convert, HashCode, and the other ways to the device culture
    /// (NumberFormatInfo.CurrentInfo, Thread.CurrentCulture, CultureInfo.DefaultThreadCurrentCulture, Comparer.Default).
    /// Nor are the known gaps that the remarks of the Unity-bound policy list. Code that may use those needs rules of its
    /// own next to this policy, such as the Client's number-text rules in design section 10.4.
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
