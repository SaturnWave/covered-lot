using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace CoveredLot.Tests.Support
{
    /// <summary>
    /// The culture guard (design section 10.2), which each test assembly runs as a fixture of its own that derives from
    /// this one: the tests run under the culture PUZZLE_TEST_CULTURE names, and that culture breaks at least one invariant
    /// assumption inside the runtime that runs them. A lost or misspelt variable therefore fails loudly instead of running
    /// quietly under this machine's culture. The name test reads the variable itself rather than through TestCulture,
    /// which [HostileCulture] uses to pick the culture, so a fault there cannot hide from it; and it writes the culture it
    /// ran under to its output ("culture under test: tr-TR"), the only place the report records it. UnityChecks requires
    /// each run's guard to have passed and to name the culture the run was asked for. The attribute passes to the derived
    /// fixtures.
    /// </summary>
    [HostileCulture]
    public abstract class CultureGuardFixture
    {
        /// <summary>What the name test writes before the culture's name; UnityChecks reads the same text (UnityTestReport).</summary>
        private const string CultureLinePrefix = "culture under test: ";

        [Test]
        public void The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names()
        {
            string? requested = Environment.GetEnvironmentVariable(TestCulture.EnvironmentVariable);
            TestContext.Out.WriteLine(CultureLinePrefix + CultureInfo.CurrentCulture.Name);

            Assert.That(requested, Is.Not.Null, TestCulture.EnvironmentVariable + " is unset");
            Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo(requested), "CultureInfo.CurrentCulture");
            Assert.That(CultureInfo.CurrentUICulture.Name, Is.EqualTo(requested), "CultureInfo.CurrentUICulture");
        }

        [Test]
        public void The_culture_breaks_at_least_one_invariant_assumption()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;

            IReadOnlyList<string> broken = TestCulture.InvariantAssumptionsBrokenBy(culture);

            Assert.That(broken, Is.Not.Empty, "'" + culture.Name + "' lower-cases I to i and writes numbers as the invariant culture does,"
                + " so it exposes no culture bug; set " + TestCulture.EnvironmentVariable + " to tr-TR or sv-SE.");
        }
    }
}
