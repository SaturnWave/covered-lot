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
    /// commits it.
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
}
