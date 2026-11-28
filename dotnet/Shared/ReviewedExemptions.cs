using System;
using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The member bans' one reviewed exemption in the Client (design section 10.4, simple UI piece 13): the development
    /// line names the culture the game runs in, and its culture button keeps the device's cultures to put them back, so
    /// two methods of CoveredLot.Client.DevCultureSwitch read CultureInfo.CurrentCulture or CultureInfo.CurrentUICulture:
    /// CurrentName, which returns the culture's name, and ReadCultures, which keeps the cultures to set them again. Nothing
    /// they read makes the game's text or orders it: Copy writes every number with the invariant culture whatever the
    /// culture is. The exemption covers those two getters inside those two methods, and nothing else: another method of
    /// the type, a type nested in it, another type, and every other banned member stay refused. UnityChecks applies it to
    /// Unity's build of the Client and pins the exact reads it covers.
    /// </summary>
    internal static class ReviewedExemptions
    {
        /// <summary>The exempted methods, as the scanner starts their locations: the type's full name, "::", the method's name and "+".</summary>
        private static readonly string[] ExemptCallers =
        {
            "CoveredLot.Client.DevCultureSwitch::CurrentName+",
            "CoveredLot.Client.DevCultureSwitch::ReadCultures+",
        };

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
            ExemptCallers.Any(caller => violation.Location.StartsWith(caller, StringComparison.Ordinal))
            && ExemptUses.Any(use => violation.Reason.StartsWith(use, StringComparison.Ordinal));
    }
}
