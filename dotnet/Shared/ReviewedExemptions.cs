using System;
using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The member bans' one reviewed exemption in the Client (design section 10.4, simple UI piece 13): the development
    /// line names the culture the game runs in, and its culture button keeps the device's cultures to put them back, so
    /// CoveredLot.Client.DevCultureSwitch reads CultureInfo.CurrentCulture and CultureInfo.CurrentUICulture. Nothing it
    /// reads makes the game's text or orders it: Copy writes every number with the invariant culture whatever the culture
    /// is. The exemption covers those two getters inside that one type's own methods, and nothing else: another type, a
    /// type nested in it, or another banned member stays refused. UnityChecks applies it to Unity's build of the Client
    /// and shows that it covers reads that are really there.
    /// </summary>
    internal static class ReviewedExemptions
    {
        /// <summary>The exempted type's methods, as the scanner names their locations: the type's full name, then "::".</summary>
        private const string ExemptCaller = "CoveredLot.Client.DevCultureSwitch::";

        /// <summary>The exempted member uses, as the scanner starts a member ban's reason: the getter, then its empty parameter list.</summary>
        private static readonly string[] ExemptUses =
        {
            "System.Globalization.CultureInfo::get_CurrentCulture(): ",
            "System.Globalization.CultureInfo::get_CurrentUICulture(): ",
        };

        /// <summary>The violations the exemption does not cover, in their order.</summary>
        public static IReadOnlyList<Violation> Without(IEnumerable<Violation> violations) => violations.Where(violation => !IsExempt(violation)).ToList();

        /// <summary>The violations the exemption covers, in their order.</summary>
        public static IReadOnlyList<Violation> Covered(IEnumerable<Violation> violations) => violations.Where(IsExempt).ToList();

        private static bool IsExempt(Violation violation) =>
            violation.Location.StartsWith(ExemptCaller, StringComparison.Ordinal)
            && ExemptUses.Any(use => violation.Reason.StartsWith(use, StringComparison.Ordinal));
    }
}
