using System.Collections.Generic;
using System.Linq;
using CoveredLot.Shared;
using CoveredLot.UnityChecks.Support;
using Xunit;

namespace CoveredLot.UnityChecks
{
    /// <summary>
    /// The code-standard rules of design section 10.4 on what Unity built: no assembly of the game or its tests has a
    /// generic or bare catch block, with or without a when-filter, an empty catch block, or a type reference to
    /// System.Random or UnityEngine.Random; Core, the Client and the Editor have no async state machine, which holds a
    /// compiler-written generic catch; and Core has no catch block at all and no static field that can be reassigned. A
    /// catch of a specific type with a when-filter passes outside Core. Unity's build of Core also holds overflow-checked
    /// arithmetic (add.ovf, sub.ovf or mul.ovf, not counting the mul.ovf.un the compiler writes for a stackalloc's size in
    /// any context), which shows that Unity applied the -checked+ of Core's csc.rsp: no Core source holds the word checked,
    /// so none opens a checked context of its own (CoveredLot.Core.Tests checks the sources). CoveredLot.Core.Tests shows
    /// each rule on planted samples (HygieneTests there); these apply them to Unity's compiler output.
    /// </summary>
    public sealed class HygieneTests
    {
        private const string Core = "CoveredLot.Core";

        public static TheoryData<string> EveryAssembly => new(UnityOutputs.Assemblies().Select(assembly => assembly.Name));

        public static TheoryData<string> GameAssemblies => new() { Core, "CoveredLot.Client", "CoveredLot.Editor" };

        [Theory]
        [MemberData(nameof(EveryAssembly))]
        public void No_catch_block_is_generic_or_bare_with_or_without_a_filter_or_empty(string name)
        {
            using AssemblyScanner scanner = UnityOutputs.Scan(name);

            AssertNone(name, scanner.GenericOrEmptyCatchBlocks());
        }

        [Theory]
        [MemberData(nameof(EveryAssembly))]
        public void No_type_reference_names_a_random_type(string name)
        {
            using AssemblyScanner scanner = UnityOutputs.Scan(name);

            IReadOnlyList<string> referenced = CodeHygiene.RandomTypeReferences(scanner);

            Assert.True(referenced.Count == 0, name + " references " + string.Join(" and ", referenced) + ".");
        }

        [Theory]
        [MemberData(nameof(GameAssemblies))]
        public void No_async_state_machine(string name)
        {
            using AssemblyScanner scanner = UnityOutputs.Scan(name);

            AssertNone(name, scanner.AsyncStateMachines());
        }

        [Fact]
        public void Core_has_no_catch_block_at_all()
        {
            using AssemblyScanner core = UnityOutputs.Scan(Core);

            AssertNone(Core, core.CatchBlocks());
        }

        [Fact]
        public void Core_holds_overflow_checked_arithmetic_so_Unity_applied_its_checked_option()
        {
            using AssemblyScanner core = UnityOutputs.Scan(Core);

            IReadOnlyList<LocatedInstruction> found = core.OverflowCheckedArithmetic();

            Assert.True(found.Count > 0, "Unity's build of " + Core
                + " holds no add.ovf, sub.ovf or mul.ovf, so Unity built it without the -checked+ of Assets/CoveredLot/Core/csc.rsp.");
        }

        [Fact]
        public void Core_has_no_static_field_that_can_be_reassigned()
        {
            using AssemblyScanner core = UnityOutputs.Scan(Core);

            AssertNone(Core, core.ReassignableStaticFields());
        }

        private static void AssertNone(string assembly, IReadOnlyList<Violation> violations) =>
            Assert.True(violations.Count == 0, "Unity's build of " + assembly + " breaks the code standard:\n" + string.Join("\n", violations.Select(violation => "  " + violation)));
    }
}
