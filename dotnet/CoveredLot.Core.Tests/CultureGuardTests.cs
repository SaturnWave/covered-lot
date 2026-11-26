using System;
using System.Globalization;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The run really uses the hostile culture PUZZLE_TEST_CULTURE names (see <see cref="HostileCulture"/>), so a lost
    /// or misspelt variable fails loudly instead of running quietly under this machine's culture.
    /// </summary>
    public sealed class CultureGuardTests
    {
        [Fact]
        public void The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names()
        {
            string? requested = HostileCulture.RequestedCultureName();

            Assert.True(requested is not null, HostileCulture.EnvironmentVariable + " must name the test culture, tr-TR or sv-SE"
                + " (it is " + QuotedOrUnset(Environment.GetEnvironmentVariable(HostileCulture.EnvironmentVariable)) + ")"
                + "; run tools/check-dotnet.sh, or set it before dotnet test.");
            Assert.Equal(requested, CultureInfo.CurrentCulture.Name);
            Assert.Equal(requested, CultureInfo.CurrentUICulture.Name);
        }

        [Fact]
        public void The_culture_breaks_at_least_one_invariant_assumption()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
#pragma warning disable CA1862 // The point is to observe the culture-specific lower-casing.
            bool changesDottedI = "I".ToLower(culture) != "i";
#pragma warning restore CA1862
            bool hasAnotherMinusSign = culture.NumberFormat.NegativeSign != "-";
            bool hasAnotherDecimalSeparator = culture.NumberFormat.NumberDecimalSeparator != ".";

            Assert.True(changesDottedI || hasAnotherMinusSign || hasAnotherDecimalSeparator,
                "'" + culture.Name + "' exposes no culture bug; set " + HostileCulture.EnvironmentVariable + " to tr-TR or sv-SE.");
        }

        private static string QuotedOrUnset(string? value) => value is null ? "unset" : "'" + value + "'";
    }
}
