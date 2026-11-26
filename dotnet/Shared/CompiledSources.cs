using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace CoveredLot.Shared
{
    /// <summary>A built assembly's DLL and the portable PDB the compiler wrote with it, each by its full path.</summary>
    internal sealed record BuildFiles
    {
        public required string Dll { get; init; }

        public required string Pdb { get; init; }

        /// <summary>The DLL and the PDB of the same name beside it, where Unity and the .NET SDK both write it.</summary>
        public static BuildFiles Beside(string dll) => new() { Dll = dll, Pdb = Path.ChangeExtension(dll, ".pdb") };
    }

    /// <summary>A source file the compiler read: the name it was given, and the checksum of the bytes it compiled.</summary>
    internal sealed record CompiledDocument
    {
        /// <summary>The path as the PDB records it: absolute for Unity, from "/_/" for the .NET build, which maps the repository root there.</summary>
        public required string Name { get; init; }

        /// <summary>The checksum's algorithm, as a portable PDB names it.</summary>
        public required Guid HashAlgorithm { get; init; }

        public required ImmutableArray<byte> Hash { get; init; }
    }

    /// <summary>A source file as it is now: its path from the repository root, with '/' separators, and its bytes.</summary>
    internal sealed record CurrentSource
    {
        public required string RelativePath { get; init; }

        public required ImmutableArray<byte> Content { get; init; }
    }

    /// <summary>The C# sources of one assembly as they are now: its asmdef's folder, from the repository root with '/' separators, and its .cs files.</summary>
    internal sealed record CurrentSources
    {
        public required string Folder { get; init; }

        public required IReadOnlyList<CurrentSource> Files { get; init; }
    }

    /// <summary>
    /// The content check of design section 10.4's inputs. The portable PDB a compiler writes beside a DLL records every source
    /// file it compiled (a Document: the path it was given and the SHA-256 of the bytes it read), and the DLL names that PDB
    /// by an id. So the PDB shows which sources, with which content, the DLL was built from, whatever the files' times say: a
    /// copy, an unpacked archive and git mv keep a file's old time, a deletion leaves no time at all, and an edit can be given
    /// its old time back. The documents in an assembly's folder must be exactly the .cs files the folder holds now, each with
    /// its present content; documents elsewhere, such as the code Unity's source generator adds, are not the folder's.
    /// CompiledSourcesTests shows each problem on the PDB of the .NET build of Core.
    /// </summary>
    internal static class CompiledSources
    {
        /// <summary>How a portable PDB names SHA-256 as a document's hash algorithm.</summary>
        public static readonly Guid Sha256 = new("8829d00f-11b8-4213-878b-770e8597ac16");

        /// <summary>The signature that starts every metadata block, and so every portable PDB.</summary>
        private static readonly byte[] MetadataSignature = { (byte)'B', (byte)'S', (byte)'J', (byte)'B' };

        /// <summary>
        /// The documents of the build's PDB. Throws <see cref="InvalidDataException"/>, naming both files, when the PDB is no
        /// portable PDB or not the one the DLL names, since its documents then say nothing of that DLL.
        /// </summary>
        public static IReadOnlyList<CompiledDocument> Read(BuildFiles build)
        {
            string names = Path.GetFileName(build.Pdb) + ", read for " + Path.GetFileName(build.Dll) + ",";
            using FileStream pdbStream = File.OpenRead(build.Pdb);
            if (!StartsWithMetadataSignature(pdbStream))
            {
                throw new InvalidDataException(names + " is not a portable PDB.");
            }

            using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream, MetadataStreamOptions.LeaveOpen);
            MetadataReader pdb = provider.GetMetadataReader();
            if (pdb.DebugMetadataHeader is not DebugMetadataHeader header)
            {
                throw new InvalidDataException(names + " holds metadata, but not a portable PDB's.");
            }

            if (!NamesPdb(build.Dll, new BlobContentId(header.Id)))
            {
                throw new InvalidDataException(names + " is not the PDB the DLL names, so it does not show what the DLL was built from.");
            }

            var documents = new List<CompiledDocument>();
            foreach (DocumentHandle handle in pdb.Documents)
            {
                Document document = pdb.GetDocument(handle);
                documents.Add(new CompiledDocument
                {
                    Name = pdb.GetString(document.Name),
                    HashAlgorithm = pdb.GetGuid(document.HashAlgorithm),
                    Hash = pdb.GetBlobContent(document.Hash),
                });
            }

            return documents;
        }

        /// <summary>
        /// What shows that the documents in the folder are not exactly the current sources with their current content: a
        /// source the build never read, a document whose source is no longer there, a source the build recorded with no
        /// SHA-256, and a source whose bytes have another SHA-256 than the build recorded. Empty when the build was made from
        /// exactly these sources.
        /// </summary>
        public static IReadOnlyList<string> Problems(IReadOnlyList<CompiledDocument> compiled, CurrentSources current)
        {
            var inFolder = new Dictionary<string, CompiledDocument>(StringComparer.Ordinal);
            foreach (CompiledDocument document in compiled)
            {
                if (RelativePathInFolder(document.Name, current.Folder) is string relativePath)
                {
                    inFolder[relativePath] = document;
                }
            }

            var problems = new List<string>();
            foreach (CurrentSource source in current.Files.OrderBy(source => source.RelativePath, StringComparer.Ordinal))
            {
                if (!inFolder.TryGetValue(source.RelativePath, out CompiledDocument? document))
                {
                    problems.Add(source.RelativePath + " is not among the sources the DLL was built from");
                }
                else if (document.HashAlgorithm != Sha256)
                {
                    problems.Add(source.RelativePath + " is recorded with no SHA-256 of its content (its checksum's algorithm is "
                        + document.HashAlgorithm.ToString("D", CultureInfo.InvariantCulture) + "), so its content cannot be compared");
                }
                else if (!SHA256.HashData(source.Content.AsSpan()).AsSpan().SequenceEqual(document.Hash.AsSpan()))
                {
                    problems.Add(source.RelativePath + " differs from the content the DLL was built from");
                }
            }

            ISet<string> present = current.Files.Select(source => source.RelativePath).ToHashSet(StringComparer.Ordinal);
            problems.AddRange(inFolder.Keys
                .Where(relativePath => !present.Contains(relativePath))
                .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
                .Select(relativePath => "the DLL was built from " + relativePath + ", which is no longer there"));
            return problems;
        }

        /// <summary>
        /// The document's path from the repository root when it lies in the folder; null otherwise. The PDB records the path
        /// the compiler was given, whose start differs between machines and builds, so the folder is found as a run of whole
        /// names in it.
        /// </summary>
        private static string? RelativePathInFolder(string documentName, string folder)
        {
            string path = documentName.Replace('\\', '/');
            int at = path.IndexOf("/" + folder + "/", StringComparison.Ordinal);
            return at < 0 ? null : path[(at + 1)..];
        }

        private static bool StartsWithMetadataSignature(FileStream stream)
        {
            var start = new byte[MetadataSignature.Length];
            int read = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
            stream.Position = 0;
            return read == start.Length && start.AsSpan().SequenceEqual(MetadataSignature);
        }

        /// <summary>Whether one of the DLL's CodeView entries names the PDB with this id: its GUID and its stamp.</summary>
        private static bool NamesPdb(string dllPath, BlobContentId pdbId)
        {
            using FileStream dllStream = File.OpenRead(dllPath);
            using var dll = new PEReader(dllStream, PEStreamOptions.LeaveOpen);
            foreach (DebugDirectoryEntry entry in dll.ReadDebugDirectory())
            {
                if (entry.Type == DebugDirectoryEntryType.CodeView
                    && new BlobContentId(dll.ReadCodeViewDebugDirectoryData(entry).Guid, entry.Stamp) == pdbId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
