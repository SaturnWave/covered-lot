using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Locates the repository root by walking up from the test assembly's folder to the first folder that holds
    /// dotnet/global.json, so the tests read the repository they were built from, wherever it is cloned. Lists the
    /// files git knows about as the engine's RepositoryLayout does: tracked files plus untracked files that
    /// .gitignore does not exclude, so build output is never inspected while a new file is checked before anyone
    /// commits it. Lists, too, every file on disk under a folder whose extension is a given one, in any letter case.
    /// </summary>
    internal static class RepositoryLayout
    {
        private const string RootMarker = "dotnet/global.json";

        private static readonly Lazy<IReadOnlyList<string>> KnownFiles = new(ListFilesKnownToGit);

        public static string Root { get; } = FindRoot();

        /// <summary>The full path of a repository-relative path written with '/' separators.</summary>
        public static string FullPath(string relativePath) =>
            Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>The path of <paramref name="fullPath"/> relative to the repository root, with '/' separators.</summary>
        public static string Relative(string fullPath) =>
            Path.GetRelativePath(Root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

        /// <summary>Repository-relative paths, with '/' separators, of every file git knows about; sorted ordinally.</summary>
        public static IReadOnlyList<string> Files() => KnownFiles.Value;

        /// <summary>
        /// The full paths of every file on disk under the folder (repository-relative, '/' separators), ignored ones
        /// included, whose extension is the given one in any letter case. MSBuild on Windows compiles Copy.CS as it
        /// compiles Copy.cs, so a guard that compared extensions exactly would miss a file the build takes.
        /// </summary>
        public static IReadOnlyList<string> FilesWithExtension(string relativeFolder, FileExtension extension) => Directory
            .EnumerateFiles(FullPath(relativeFolder), "*", SearchOption.AllDirectories)
            .Where(path => HasExtension(path, extension))
            .ToList();

        /// <summary>Whether the path ends in the extension, in any letter case.</summary>
        public static bool HasExtension(string path, FileExtension extension) =>
            string.Equals(Path.GetExtension(path), extension.WithDot, StringComparison.OrdinalIgnoreCase);

        private static IReadOnlyList<string> ListFilesKnownToGit()
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // git writes paths as UTF-8; the platform default (a console code page on Windows) would garble them.
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
            };
            foreach (string argument in new[] { "ls-files", "-z", "--cached", "--others", "--exclude-standard" })
            {
                start.ArgumentList.Add(argument);
            }

            using Process git = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start git; the guard tests need git on PATH.");
            string output = git.StandardOutput.ReadToEnd();
            string errors = git.StandardError.ReadToEnd();
            git.WaitForExit();
            if (git.ExitCode != 0)
            {
                throw new InvalidOperationException("git ls-files failed in " + Root + ": " + errors);
            }

            // Paths deleted from the working tree but still in the index are skipped.
            return output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(path => File.Exists(FullPath(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static string FindRoot()
        {
            for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, RootMarker)))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException(
                "No " + RootMarker + " above " + AppContext.BaseDirectory + "; run the tests from inside the repository.");
        }
    }

    /// <summary>
    /// A file extension the guards look for, with its dot (".cs"). A type of its own, so that an extension cannot take the
    /// place of the path or the folder it is looked for in.
    /// </summary>
    internal readonly struct FileExtension
    {
        private FileExtension(string withDot) => WithDot = withDot;

        public static FileExtension CSharp { get; } = new(".cs");

        public static FileExtension Asmdef { get; } = new(".asmdef");

        public static FileExtension Asmref { get; } = new(".asmref");

        public static FileExtension Dll { get; } = new(".dll");

        /// <summary>The extension as Path.GetExtension gives it, dot included.</summary>
        public string WithDot { get; }
    }
}
