using System.Collections;
using System.Globalization;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// [HostileCulture] itself (design section 10.2), driven on a fresh instance inside a test: BeforeTest applies the
    /// culture PUZZLE_TEST_CULTURE names to the current, UI, default-thread and default-thread UI cultures, and AfterTest
    /// puts back each of the four as BeforeTest found it. The test first gives the four different values, an unset default
    /// among them, so a culture put back in another's place fails as well. The fixture's own [HostileCulture] puts back the
    /// cultures this test found once it ends.
    /// </summary>
    [HostileCulture]
    public sealed class HostileCultureTests
    {
        private const string Unset = "(unset)";

        [Test]
        public void It_applies_the_requested_culture_to_all_four_and_puts_back_each_culture_it_found()
        {
            string requested = TestCulture.Required().Name;
            CultureInfo current = CultureInfo.InvariantCulture;
            CultureInfo currentUi = CultureInfo.GetCultureInfo("en-US");
            CultureInfo defaultThreadUi = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = current;
            CultureInfo.CurrentUICulture = currentUi;
            CultureInfo.DefaultThreadCurrentCulture = null;
            CultureInfo.DefaultThreadCurrentUICulture = defaultThreadUi;
            var attribute = new HostileCultureAttribute();
            ITest test = TestExecutionContext.CurrentContext.CurrentTest;

            RunToEnd(attribute.BeforeTest(test));
            string[] applied = CultureNames();
            RunToEnd(attribute.AfterTest(test));
            string[] putBack = CultureNames();

            Assert.That(applied, Is.EqualTo(new[] { requested, requested, requested, requested }),
                "the current, UI, default-thread and default-thread UI cultures after BeforeTest");
            Assert.That(putBack, Is.EqualTo(new[] { NameOf(current), NameOf(currentUi), Unset, NameOf(defaultThreadUi) }),
                "the current, UI, default-thread and default-thread UI cultures after AfterTest");
        }

        /// <summary>The current, UI, default-thread and default-thread UI cultures, in that order.</summary>
        private static string[] CultureNames() => new[]
        {
            NameOf(CultureInfo.CurrentCulture), NameOf(CultureInfo.CurrentUICulture),
            NameOf(CultureInfo.DefaultThreadCurrentCulture), NameOf(CultureInfo.DefaultThreadCurrentUICulture),
        };

        private static string NameOf(CultureInfo? culture)
        {
            if (culture is null)
            {
                return Unset;
            }

            return culture.Name.Length == 0 ? "the invariant culture" : culture.Name;
        }

        /// <summary>Runs an outer test action's steps, which these actions never spread over frames.</summary>
        private static void RunToEnd(IEnumerator steps)
        {
            while (steps.MoveNext())
            {
            }
        }
    }
}
