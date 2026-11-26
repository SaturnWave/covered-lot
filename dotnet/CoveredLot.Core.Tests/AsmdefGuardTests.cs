using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The Unity side of the compile rules (design sections 3 and 10a). Core's assembly definition keeps it
    /// Unity-free and lets it see only the two engine DLLs; no assembly allows unsafe code; and every CoveredLot
    /// assembly carries its own csc.rsp, because a csc.rsp at the Assets root would apply to every package as well.
    /// </summary>
    public sealed class AsmdefGuardTests
    {
        private const string CoreAsmdef = "Assets/CoveredLot/Core/CoveredLot.Core.asmdef";
        private const string CoreAssembly = "CoveredLot.Core";

        /// <summary>Core's asmdef, property by property, each value in compact JSON.</summary>
        private static readonly IReadOnlyDictionary<string, string> ExpectedCoreAsmdef = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = "\"CoveredLot.Core\"",
            ["rootNamespace"] = "\"CoveredLot.Core\"",
            ["references"] = "[]",
            ["includePlatforms"] = "[]",
            ["excludePlatforms"] = "[]",
            ["allowUnsafeCode"] = "false",
            ["overrideReferences"] = "true",
            ["precompiledReferences"] = "[\"PuzzleCore.dll\",\"PuzzleCore.Jam.dll\"]",
            ["autoReferenced"] = "false",
            ["defineConstraints"] = "[]",
            ["versionDefines"] = "[]",
            ["noEngineReferences"] = "true",
        };

        /// <summary>The csc.rsp options of every CoveredLot assembly; Core also checks overflow, like its .NET build.</summary>
        private static readonly string[] OptionsOfEveryAssembly = { "-warnaserror+", "-nullable:enable" };
        private const string CoreOnlyOption = "-checked+";

        /// <summary>A csc.rsp separates its options with spaces and line ends.</summary>
        private static readonly char[] OptionSeparators = { ' ', '\t', '\r', '\n' };

        [Fact]
        public void The_core_asmdef_is_unity_free_and_sees_only_the_two_engine_dlls()
        {
            IReadOnlyDictionary<string, string> actual = CompactProperties(RepositoryLayout.FullPath(CoreAsmdef));

            Assert.Equal(Sorted(ExpectedCoreAsmdef), Sorted(actual));
        }

        [Fact]
        public void No_asmdef_under_Assets_allows_unsafe_code()
        {
            IReadOnlyList<string> asmdefs = Asmdefs("Assets");
            var unsafeOnes = new List<string>();
            foreach (string asmdef in asmdefs)
            {
                if (CompactProperties(asmdef).TryGetValue("allowUnsafeCode", out string? value) && value != "false")
                {
                    unsafeOnes.Add(RepositoryLayout.Relative(RepositoryLayout.Root, asmdef));
                }
            }

            Assert.NotEmpty(asmdefs);
            Assert.True(unsafeOnes.Count == 0, "these asmdefs allow unsafe code: " + string.Join(", ", unsafeOnes));
        }

        [Fact]
        public void Every_CoveredLot_asmdef_folder_has_a_csc_rsp_with_exactly_the_approved_options()
        {
            IReadOnlyList<string> asmdefs = Asmdefs("Assets/CoveredLot");
            var problems = new List<string>();
            foreach (string asmdef in asmdefs)
            {
                string assembly = AssemblyName(asmdef);
                string rsp = Path.Join(Path.GetDirectoryName(asmdef.AsSpan()), "csc.rsp");
                if (!File.Exists(rsp))
                {
                    problems.Add(assembly + " has no csc.rsp beside its asmdef");
                    continue;
                }

                IReadOnlyList<string> options = Sorted(File.ReadAllText(rsp, Encoding.UTF8).Split(OptionSeparators, StringSplitOptions.RemoveEmptyEntries));
                IReadOnlyList<string> approved = Sorted(ApprovedOptions(assembly));
                if (!options.SequenceEqual(approved, StringComparer.Ordinal))
                {
                    problems.Add(assembly + "'s csc.rsp holds [" + string.Join(" ", options) + "], not [" + string.Join(" ", approved) + "]");
                }
            }

            Assert.NotEmpty(asmdefs);
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }

        [Fact]
        public void There_is_no_csc_rsp_at_the_Assets_root()
        {
            Assert.False(File.Exists(RepositoryLayout.FullPath("Assets/csc.rsp")),
                "Assets/csc.rsp applies to every assembly, packages included; give each CoveredLot asmdef its own csc.rsp.");
        }

        private static IEnumerable<string> ApprovedOptions(string assembly) =>
            assembly == CoreAssembly ? OptionsOfEveryAssembly.Append(CoreOnlyOption) : OptionsOfEveryAssembly;

        private static IReadOnlyList<string> Asmdefs(string relativeFolder) => Directory
            .EnumerateFiles(RepositoryLayout.FullPath(relativeFolder), "*.asmdef", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".asmdef", StringComparison.Ordinal))
            .ToList();

        private static string AssemblyName(string asmdef)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef, Encoding.UTF8));
            return document.RootElement.GetProperty("name").GetString() ?? asmdef;
        }

        /// <summary>The asmdef's top-level properties, each value written as compact JSON.</summary>
        private static IReadOnlyDictionary<string, string> CompactProperties(string asmdef)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef, Encoding.UTF8));
            return document.RootElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => Compact(property.Value), StringComparer.Ordinal);
        }

        private static string Compact(JsonElement value)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                value.WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static IReadOnlyList<KeyValuePair<string, string>> Sorted(IReadOnlyDictionary<string, string> properties) =>
            properties.OrderBy(property => property.Key, StringComparer.Ordinal).ToList();

        private static IReadOnlyList<string> Sorted(IEnumerable<string> options) =>
            options.OrderBy(option => option, StringComparer.Ordinal).ToList();
    }
}
