using System;
using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The rules every string the game shows keeps (design section 9), development lines included: printable ASCII only,
    /// and none of the words the brief keeps out of the game's text, in any letter case and inside longer words too, so
    /// "blocked" breaks the rule as "block" does. Piece 7 gives Core these rules as TextRules.
    /// </summary>
    internal static class CopyRules
    {
        public static IReadOnlyList<string> BannedWords { get; } = new[] { "jam", "parking", "bus", "tray", "board", "block" };

        /// <summary>Each rule the line breaks, in words; empty when it keeps them all.</summary>
        public static IReadOnlyList<string> BrokenBy(string line)
        {
            var broken = new List<string>();
            if (line.Any(character => character < ' ' || character > '~'))
            {
                broken.Add("holds a character that is not printable ASCII");
            }

            broken.AddRange(BannedWords
                .Where(word => line.Contains(word, StringComparison.OrdinalIgnoreCase))
                .Select(word => "holds the word " + word));
            return broken;
        }
    }
}
