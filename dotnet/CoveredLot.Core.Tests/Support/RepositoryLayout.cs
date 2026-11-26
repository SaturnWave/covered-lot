using System;
using System.IO;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Locates the repository root by walking up from the test assembly's folder to the first folder that holds
    /// dotnet/global.json, so the tests read the repository they were built from, wherever it is cloned.
    /// </summary>
    internal static class RepositoryLayout
    {
        private const string RootMarker = "dotnet/global.json";

        public static string Root { get; } = FindRoot();

        /// <summary>The full path of a repository-relative path written with '/' separators.</summary>
        public static string FullPath(string relativePath) =>
            Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>The path of <paramref name="fullPath"/> relative to <paramref name="relativeTo"/>, with '/' separators.</summary>
        public static string Relative(string relativeTo, string fullPath) =>
            Path.GetRelativePath(relativeTo, fullPath).Replace(Path.DirectorySeparatorChar, '/');

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
