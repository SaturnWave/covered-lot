using System;
using System.Collections.Generic;
using PuzzleCore;

namespace CoveredLot.Core
{
    /// <summary>
    /// The rules every string the game shows keeps (design section 9), development lines included. It is printable ASCII,
    /// a space to a tilde, so it reads the same on every device. And it holds none of the banned words, in any letter case
    /// and inside longer words too, so "blocked" breaks the rule as "block" does: the brief keeps jam, parking and bus out
    /// of the game's names and copy, and the interim copy keeps tray, board and block, the block theme's words, until the
    /// theme is settled. The tests hold Copy's public strings, the catalogue's lines, and every string of the pictures along
    /// each shipped level's winning order and the seeded runs to these rules.
    /// </summary>
    public static class TextRules
    {
        /// <summary>The words no string the game shows may hold: jam, parking, bus, tray, board and block.</summary>
        public static ReadOnlyArray<string> BannedWords { get; } = ReadOnlyArray.Of("jam", "parking", "bus", "tray", "board", "block");

        /// <summary>Whether every character of the text is printable ASCII, from the space (U+0020) to the tilde (U+007E).</summary>
        public static bool IsPrintableAscii(string text)
        {
            foreach (char character in text)
            {
                if (character < ' ' || character > '~')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The banned words the text holds, in the order of <see cref="BannedWords"/>, each found by an ordinal comparison
        /// that ignores letter case, inside longer words too; none when it holds none.
        /// </summary>
        public static ReadOnlyArray<string> BannedWordsIn(string text)
        {
            var found = new List<string>();
            foreach (string word in BannedWords)
            {
                if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(word);
                }
            }

            return ReadOnlyArray.CopyOf(found);
        }
    }
}
