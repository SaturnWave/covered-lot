using System.Text.RegularExpressions;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Paths that tie a file to one machine or one person, which no committed file may hold: the repository is public.
    /// The patterns are described in words, so this file passes tools/scan-before-push.sh.
    /// </summary>
    internal static class MachinePaths
    {
        /// <summary>
        /// A drive-letter path as Windows writes it (a letter, a colon, a slash or backslash) or as Git Bash writes it (a
        /// slash, a letter, a slash), or a network share (two backslashes).
        /// </summary>
        public static Regex DriveLetterOrSharePath { get; } = new(
            @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]|(?<![A-Za-z0-9._~/-])/[A-Za-z]/|\\\\", RegexOptions.CultureInvariant);

        /// <summary>A users or home folder between slashes or backslashes, in any case, or a home-relative path (a tilde and a slash).</summary>
        public static Regex UserFolder { get; } = new(@"[\\/](users|home)[\\/]|~[\\/]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
