using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The content check (<see cref="CompiledSources"/>), which UnityChecks applies to the portable PDB Unity writes beside
    /// each CoveredLot DLL, shown on a real portable PDB: the one the .NET build of Core writes beside its DLL, which records
    /// Core's sources from "/_/", where the build maps the repository root. The current Core sources are exactly the
    /// documents that PDB records in Assets/CoveredLot/Core, each with the SHA-256 of its bytes; a source added, deleted or
    /// edited since the build is refused by name, as is a document recorded with another checksum; and a PDB that the DLL
    /// does not name is refused before its documents are read.
    /// </summary>
    public sealed class CompiledSourcesTests
    {
        private const string CoreFolder = "Assets/CoveredLot/Core";
        private const string CopySource = CoreFolder + "/Copy.cs";

        /// <summary>How a portable PDB names SHA-1, which a compiler may use for a document's checksum instead of SHA-256.</summary>
        private static readonly Guid Sha1 = new("ff1816ec-aa5e-4d10-87f7-6f4963833460");

        /// <summary>The .NET build of Core, as the test project references it, and the PDB beside it.</summary>
        private static readonly BuildFiles CoreBuild = BuildFiles.Beside(typeof(UnknownEngineCaseException).Assembly.Location);

        [Fact]
        public void The_dotnet_build_of_Core_was_built_from_exactly_the_Core_sources_as_they_are_now()
        {
            IReadOnlyList<CompiledDocument> compiled = CompiledSources.Read(CoreBuild);
            CurrentSources current = CoreSourcesNow();

            IReadOnlyList<string> problems = CompiledSources.Problems(compiled, current);

            // The PDB records each Core source from "/_/" with a SHA-256, and documents the SDK generates elsewhere, which the
            // check leaves out.
            Assert.NotEmpty(current.Files);
            Assert.All(current.Files, source => Assert.Contains(compiled, document => document.Name == "/_/" + source.RelativePath && document.HashAlgorithm == CompiledSources.Sha256));
            Assert.Contains(compiled, document => !document.Name.StartsWith("/_/" + CoreFolder + "/", StringComparison.Ordinal));
            Assert.True(problems.Count == 0, "The .NET build of Core is refused: " + string.Join("; ", problems));
        }

        [Fact]
        public void A_source_added_since_the_build_is_refused_by_name()
        {
            CurrentSources current = CoreSourcesNow();
            var planted = new CurrentSource { RelativePath = CoreFolder + "/Planted.cs", Content = ImmutableArray.Create((byte)'x') };

            IReadOnlyList<string> problems = CompiledSources.Problems(CompiledSources.Read(CoreBuild), With(current, current.Files.Append(planted)));

            Assert.Equal(new[] { CoreFolder + "/Planted.cs is not among the sources the DLL was built from" }, problems);
        }

        [Fact]
        public void A_source_deleted_or_moved_away_since_the_build_is_refused_by_name()
        {
            CurrentSources current = CoreSourcesNow();

            IReadOnlyList<string> problems = CompiledSources.Problems(CompiledSources.Read(CoreBuild),
                With(current, current.Files.Where(source => source.RelativePath != CopySource)));

            Assert.Contains(current.Files, source => source.RelativePath == CopySource);
            Assert.Equal(new[] { "the DLL was built from " + CopySource + ", which is no longer there" }, problems);
        }

        [Fact]
        public void A_source_edited_since_the_build_is_refused_by_name()
        {
            CurrentSources current = CoreSourcesNow();

            IReadOnlyList<string> problems = CompiledSources.Problems(CompiledSources.Read(CoreBuild),
                With(current, current.Files.Select(source => source.RelativePath == CopySource ? Edited(source) : source)));

            Assert.Equal(new[] { CopySource + " differs from the content the DLL was built from" }, problems);
        }

        /// <summary>
        /// A checksum of another algorithm cannot show that the content is the same, so the source is refused. The document
        /// keeps its own bytes, which do match the source's SHA-256: only the algorithm it names differs.
        /// </summary>
        [Fact]
        public void A_source_recorded_with_another_checksum_is_refused_by_name()
        {
            CurrentSources current = CoreSourcesNow();
            IReadOnlyList<CompiledDocument> compiled = CompiledSources.Read(CoreBuild)
                .Select(document => document.Name == "/_/" + CopySource ? document with { HashAlgorithm = Sha1 } : document)
                .ToList();

            IReadOnlyList<string> problems = CompiledSources.Problems(compiled, current);

            Assert.Contains(compiled, document => document.HashAlgorithm == Sha1);
            Assert.Equal(new[] { CopySource + " is recorded with no SHA-256 of its content (its checksum's algorithm is "
                + Sha1.ToString("D", CultureInfo.InvariantCulture) + "), so its content cannot be compared" }, problems);
        }

        /// <summary>The test assembly's own PDB is a portable PDB too, but Core's DLL does not name it.</summary>
        [Fact]
        public void A_pdb_the_dll_does_not_name_is_refused()
        {
            var mismatched = new BuildFiles { Dll = CoreBuild.Dll, Pdb = BuildFiles.Beside(typeof(CompiledSourcesTests).Assembly.Location).Pdb };

            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => CompiledSources.Read(mismatched));

            Assert.Equal("CoveredLot.Core.Tests.pdb, read for CoveredLot.Core.dll, is not the PDB the DLL names, so it does not show what the DLL was built from.",
                refused.Message);
        }

        /// <summary>Every .cs file under Assets/CoveredLot/Core, as the .NET build compiles them, with its bytes as they are now.</summary>
        private static CurrentSources CoreSourcesNow()
        {
            var files = new List<CurrentSource>();
            foreach (string path in RepositoryLayout.FilesWithExtension(CoreFolder, ".cs"))
            {
                files.Add(new CurrentSource { RelativePath = RepositoryLayout.Relative(path), Content = File.ReadAllBytes(path).ToImmutableArray() });
            }

            return new CurrentSources { Folder = CoreFolder, Files = files };
        }

        private static CurrentSources With(CurrentSources current, IEnumerable<CurrentSource> files) => current with { Files = files.ToList() };

        private static CurrentSource Edited(CurrentSource source) => source with { Content = source.Content.Add((byte)' ') };
    }
}
