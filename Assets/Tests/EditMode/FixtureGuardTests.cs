using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CoveredLot.Tests.Support;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// Every fixture of the CoveredLot.Tests assemblies carries [HostileCulture] (design section 10.3), itself or through a
    /// base class, so no test runs under this machine's culture by accident. A fixture is a class that carries a fixture
    /// attribute such as [TestFixture], or has a test method: one marked with any attribute that makes NUnit or Unity build a
    /// test ([Test], [TestCase], [TestCaseSource], [Theory], [UnityTest] and the like), declared or inherited. The editor
    /// loads the PlayMode tests as well, so this one EditMode guard sees both test assemblies and the support assembly.
    /// </summary>
    [HostileCulture]
    public sealed class FixtureGuardTests
    {
        private const string TestAssemblyPrefix = "CoveredLot.Tests.";
        private const string EditModeAssembly = "CoveredLot.Tests.EditMode";
        private const string PlayModeAssembly = "CoveredLot.Tests.PlayMode";

        private const BindingFlags AnyMethod = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void Every_fixture_of_the_CoveredLot_test_assemblies_carries_HostileCulture()
        {
            IReadOnlyList<Type> fixtures = TestAssemblies().SelectMany(assembly => assembly.GetTypes()).Where(IsFixture).ToList();

            IReadOnlyList<string> unguarded = fixtures
                .Where(fixture => !fixture.IsDefined(typeof(HostileCultureAttribute), inherit: true))
                .Select(fixture => fixture.FullName ?? fixture.Name)
                .ToList();

            Assert.That(FixtureNamesIn(fixtures, EditModeAssembly), Is.Not.Empty, "the fixtures found in " + EditModeAssembly);
            Assert.That(FixtureNamesIn(fixtures, PlayModeAssembly), Is.Not.Empty, "the fixtures found in " + PlayModeAssembly);
            Assert.That(unguarded, Is.Empty, "these fixtures do not carry [HostileCulture]");
        }

        /// <summary>The loaded CoveredLot.Tests assemblies, the PlayMode tests' loaded by name if the editor has not loaded them.</summary>
        private static IReadOnlyList<Assembly> TestAssemblies()
        {
            List<Assembly> loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => NameOf(assembly).StartsWith(TestAssemblyPrefix, StringComparison.Ordinal))
                .ToList();
            if (!loaded.Any(assembly => NameOf(assembly) == PlayModeAssembly))
            {
                loaded.Add(Assembly.Load(PlayModeAssembly));
            }

            return loaded;
        }

        private static bool IsFixture(Type type) =>
            type.IsClass && (CarriesFixtureAttribute(type) || type.GetMethods(AnyMethod).Any(IsTestMethod));

        private static bool CarriesFixtureAttribute(Type type) => type.GetCustomAttributes(inherit: true).Any(attribute => attribute is IFixtureBuilder);

        /// <summary>NUnit builds a test from a method whose attribute is a test builder; Unity's [UnityTest] is one too.</summary>
        private static bool IsTestMethod(MethodInfo method) =>
            method.GetCustomAttributes(inherit: true).Any(attribute => attribute is ITestBuilder || attribute is ISimpleTestBuilder);

        private static IReadOnlyList<string> FixtureNamesIn(IReadOnlyList<Type> fixtures, string assembly) =>
            fixtures.Where(fixture => NameOf(fixture.Assembly) == assembly).Select(fixture => fixture.FullName ?? fixture.Name).ToList();

        private static string NameOf(Assembly assembly) => assembly.GetName().Name ?? string.Empty;
    }
}
