using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace CoveredLot.Tests.Support
{
    /// <summary>
    /// The culture a Unity test run asks for: PUZZLE_TEST_CULTURE, which must name tr-TR or sv-SE, the two cultures that
    /// expose culture bugs (design section 10.2). There is no default, so a run that lost the variable fails, naming it,
    /// instead of running quietly under this machine's culture. The variable reaches the editor from the shell that starts
    /// it, so to run the tests from the editor's Test Runner window, set it before starting the editor.
    /// </summary>
    public static class TestCulture
    {
        public const string EnvironmentVariable = "PUZZLE_TEST_CULTURE";

        /// <summary>The cultures a run may name, spelt as the variable must spell them.</summary>
        public static IReadOnlyList<string> AllowedNames { get; } = new[] { "tr-TR", "sv-SE" };

        /// <summary>The culture the variable names; null when it is unset or names any other culture.</summary>
        public static CultureInfo? Requested()
        {
            string? name = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return name is not null && AllowedNames.Contains(name, StringComparer.Ordinal) ? CultureInfo.GetCultureInfo(name) : null;
        }

        /// <summary>The culture the variable names; fails the test that asks, naming the variable, when it names none.</summary>
        public static CultureInfo Required() => Requested() ?? throw new AssertionException(WhyNoneIsRequested());

        /// <summary>
        /// The ways the culture breaks what invariant code takes for granted, inside the runtime that runs the tests: the
        /// lower case of "I", the decimal separator and the digit group separator. In Unity's culture tables tr-TR lower-cases
        /// "I" to a dotless i, and sv-SE writes a decimal comma and groups digits with a no-break space; sv-SE's minus sign
        /// there is U+002D, so the minus sign is not among the probes (design section 10.2). Empty for a culture that breaks
        /// none of them.
        /// </summary>
        public static IReadOnlyList<string> InvariantAssumptionsBrokenBy(CultureInfo culture)
        {
            var broken = new List<string>();
            if ("I".ToLower(culture) != "i")
            {
                broken.Add("it lower-cases I to something other than i");
            }

            if (culture.NumberFormat.NumberDecimalSeparator != ".")
            {
                broken.Add("its decimal separator is not a full stop");
            }

            if (culture.NumberFormat.NumberGroupSeparator != ",")
            {
                broken.Add("its digit group separator is not a comma");
            }

            return broken;
        }

        private static string WhyNoneIsRequested()
        {
            string? value = Environment.GetEnvironmentVariable(EnvironmentVariable);
            string state = value is null ? "it is unset" : "it is '" + value + "'";
            return EnvironmentVariable + " must name " + string.Join(" or ", AllowedNames) + ", and " + state
                + ". Run tools/check.sh, or set it before starting the Unity editor or the unity command.";
        }
    }
}
