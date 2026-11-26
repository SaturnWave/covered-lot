using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Runs the whole test assembly under a culture that exposes culture bugs, named by PUZZLE_TEST_CULTURE: tr-TR
    /// (dotted and dotless i, decimal comma) or sv-SE (decimal comma, U+2212 minus sign). Ported from the engine's
    /// HostileCulture (tests/PuzzleCore.Tests/Support), with one change: the variable is required and has no
    /// default. When it is missing or names another culture, the culture is left alone and CultureGuardTests
    /// fails and names the variable, so a lost variable can never pass quietly under this machine's culture.
    /// </summary>
    internal static class HostileCulture
    {
        public const string EnvironmentVariable = "PUZZLE_TEST_CULTURE";

        /// <summary>The cultures a run may name, spelled as PUZZLE_TEST_CULTURE must spell them.</summary>
        public static IReadOnlyList<string> AllowedCultureNames { get; } = new[] { "tr-TR", "sv-SE" };

        /// <summary>The culture PUZZLE_TEST_CULTURE names; null when the variable is missing or names another culture.</summary>
        public static string? RequestedCultureName()
        {
            string? requested = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return requested is not null && AllowedCultureNames.Contains(requested, StringComparer.Ordinal) ? requested : null;
        }

        [ModuleInitializer]
        [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
            Justification = "The test assembly must switch culture before any test runs; xUnit 2 has no assembly fixture.")]
        internal static void Apply()
        {
            string? requested = RequestedCultureName();
            if (requested is null)
            {
                return;
            }

            CultureInfo culture = CultureInfo.GetCultureInfo(requested);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }
}
