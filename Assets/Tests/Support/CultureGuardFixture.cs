using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace CoveredLot.Tests.Support
{
    /// <summary>
    /// The culture guard (design section 10.2), which each test assembly runs as a fixture of its own that derives from
    /// this one: the tests run under the culture PUZZLE_TEST_CULTURE names, and that culture breaks at least one invariant
    /// assumption inside the runtime that runs them. A lost or misspelt variable therefore fails loudly instead of running
    /// quietly under this machine's culture. UnityChecks requires each run's culture guard to have passed. The attribute
    /// passes to the derived fixtures.
    /// </summary>
    [HostileCulture]
    public abstract class CultureGuardFixture
    {
        [Test]
        public void The_tests_run_under_the_culture_that_PUZZLE_TEST_CULTURE_names()
        {
            string requested = TestCulture.Required().Name;

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
