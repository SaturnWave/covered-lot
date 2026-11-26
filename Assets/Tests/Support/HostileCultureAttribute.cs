using System;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;

namespace CoveredLot.Tests.Support
{
    /// <summary>
    /// Runs each test of the fixture it marks under the culture PUZZLE_TEST_CULTURE names (<see cref="TestCulture"/>,
    /// design section 10.2). As an outer Unity test action it runs before the fixture's [UnitySetUp] and [SetUp] and after
    /// its teardowns, so the set-ups run under that culture too. BeforeTest fails the test, naming the variable, when the
    /// variable is unset or names another culture; otherwise it saves the current, UI and default-thread cultures and
    /// applies the requested culture to all four. AfterTest puts back what BeforeTest saved; the test runner calls it even
    /// when BeforeTest failed, and then it has nothing to put back. Subclasses of a marked fixture inherit the attribute.
    /// FixtureGuardTests checks that every fixture of the test assemblies carries it, and HostileCultureTests that it
    /// applies the culture to all four and puts back each of the four it found.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class HostileCultureAttribute : NUnitAttribute, IOuterUnityTestAction
    {
        /// <summary>The cultures that BeforeTest replaced, until AfterTest restores them; null outside a test.</summary>
        private SavedCultures? _saved;

        public IEnumerator BeforeTest(ITest test)
        {
            CultureInfo requested = TestCulture.Required();
            _saved = new SavedCultures();
            CultureInfo.DefaultThreadCurrentCulture = requested;
            CultureInfo.DefaultThreadCurrentUICulture = requested;
            CultureInfo.CurrentCulture = requested;
            CultureInfo.CurrentUICulture = requested;
            yield break;
        }

        public IEnumerator AfterTest(ITest test)
        {
            if (_saved is not null)
            {
                _saved.Restore();
                _saved = null;
            }

            yield break;
        }

        /// <summary>
        /// The current, UI and default-thread cultures as this thread has them when it is made; a default may be unset
        /// (null). Each field reads its own culture, so none can be stored in another's place.
        /// </summary>
        private sealed class SavedCultures
        {
            private readonly CultureInfo _current = CultureInfo.CurrentCulture;
            private readonly CultureInfo _currentUi = CultureInfo.CurrentUICulture;
            private readonly CultureInfo? _defaultThread = CultureInfo.DefaultThreadCurrentCulture;
            private readonly CultureInfo? _defaultThreadUi = CultureInfo.DefaultThreadCurrentUICulture;

            public void Restore()
            {
                CultureInfo.DefaultThreadCurrentCulture = _defaultThread;
                CultureInfo.DefaultThreadCurrentUICulture = _defaultThreadUi;
                CultureInfo.CurrentCulture = _current;
                CultureInfo.CurrentUICulture = _currentUi;
            }
        }
    }
}
