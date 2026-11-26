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
    /// loads the PlayMode tests as well, so this one EditMode guard sees both test assemblies and the support assembly. A type
    /// of theirs that does not load fails the guard, which names its assembly.
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
            var fixtures = new List<Type>();
            foreach (Assembly assembly in TestAssemblies())
            {
                foreach (Type type in TypesOf(assembly))
                {
                    if (IsFixture(type))
                    {
                        fixtures.Add(type);
                    }
                }
            }

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

        /// <summary>
        /// The assembly's types. One that does not load, such as a fixture whose base type or attribute comes from an assembly
        /// that is missing, fails the guard with the assembly's name; the loader's message lists each type that did not load.
        /// </summary>
        private static Type[] TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                throw new AssertionException(NameOf(assembly) + " holds types that do not load: " + exception.Message, exception);
            }
        }

        /// <summary>
        /// Whether NUnit or Unity builds tests from the class. It runs in plain loops, so an attribute that does not load fails
        /// the guard here, not inside a query that the failure's stack would show instead of the type.
        /// </summary>
        private static bool IsFixture(Type type)
        {
            if (!type.IsClass)
            {
                return false;
            }

            if (CarriesFixtureAttribute(type))
            {
                return true;
            }

            foreach (MethodInfo method in type.GetMethods(AnyMethod))
            {
                if (IsTestMethod(method))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CarriesFixtureAttribute(Type type) => type.GetCustomAttributes(inherit: true).Any(attribute => attribute is IFixtureBuilder);

        /// <summary>NUnit builds a test from a method whose attribute is a test builder; Unity's [UnityTest] is one too.</summary>
        private static bool IsTestMethod(MethodInfo method) =>
            method.GetCustomAttributes(inherit: true).Any(attribute => attribute is ITestBuilder || attribute is ISimpleTestBuilder);

        private static IReadOnlyList<string> FixtureNamesIn(IReadOnlyList<Type> fixtures, string assembly) =>
            fixtures.Where(fixture => NameOf(fixture.Assembly) == assembly).Select(fixture => fixture.FullName ?? fixture.Name).ToList();

        private static string NameOf(Assembly assembly) => assembly.GetName().Name ?? string.Empty;
    }
}
