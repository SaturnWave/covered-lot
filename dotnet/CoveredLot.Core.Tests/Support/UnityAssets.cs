using System;
using System.Linq;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// What Unity leaves out when it imports the Assets folder: any file or folder whose name starts with '.' or ends
    /// with '~', any named cvs, and any file with the .tmp extension (Unity manual, Special folder names, hidden
    /// assets). The .NET build has no such rule, so the guards use this to keep both builds on the same files.
    /// </summary>
    internal static class UnityAssets
    {
        /// <summary>Whether Unity skips the path (written with '/' separators) because one of its names is hidden.</summary>
        public static bool IsHidden(string path) =>
            path.Split('/').Any(IsHiddenName) || path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);

        private static bool IsHiddenName(string name) =>
            name.StartsWith('.') || name.EndsWith('~') || string.Equals(name, "cvs", StringComparison.OrdinalIgnoreCase);
    }
}
