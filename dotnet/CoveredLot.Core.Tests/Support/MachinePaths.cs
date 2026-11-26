using System.Text.RegularExpressions;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Paths that tie a file to one machine or one person, which no committed file may hold: the repository is public.
    /// The patterns read Unity's YAML and JSON, where a backslash may be written twice, and C# sources, so each one
    /// matches only at the start of a path: a project path through a folder named Home, a package's Documentation~
    /// folder or an escaped backslash is not a machine path. MachinePathsTests holds the forms each pattern must and
    /// must not match. The patterns are described in words, so this file passes tools/scan-before-push.sh.
    /// </summary>
    internal static class MachinePaths
    {
        /// <summary>
        /// A drive-letter path as Windows writes it (a letter, a colon, a slash or backslash), as Git Bash writes it (a
        /// slash, a letter and a slash, not inside a longer path) or as WSL writes it (/mnt/, a letter and a slash); or a
        /// network share: two backslashes, or the four that JSON writes for them, not inside a longer path, then a host
        /// name and a backslash.
        /// </summary>
        public static Regex DriveLetterOrSharePath { get; } = new(
            @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]|(?<![A-Za-z0-9._~/-])/[A-Za-z]/|/mnt/[A-Za-z]/|(?<![\\A-Za-z0-9._-])\\\\(?:\\\\)?[A-Za-z0-9._$-]+\\",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// A users or home folder at the start of a path, between slashes or backslashes, in any case; or a home-relative
        /// path, a tilde and a slash, at the start of a path.
        /// </summary>
        public static Regex UserFolder { get; } = new(
            @"(?<![A-Za-z0-9._~-])[\\/](users|home)[\\/]|(?<![A-Za-z0-9._-])~[\\/]",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
