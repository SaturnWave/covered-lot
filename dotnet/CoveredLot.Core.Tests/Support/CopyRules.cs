using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The text rules of Core's <see cref="TextRules"/> (design section 9), as the tests report them: each rule a line
    /// breaks, in words, so a failure names the line and what is wrong with it.
    /// </summary>
    internal static class CopyRules
    {
        /// <summary>Each rule the line breaks, in words: "holds a character that is not printable ASCII", "holds the word jam"; empty when it keeps them all.</summary>
        public static IReadOnlyList<string> BrokenBy(string line)
        {
            var broken = new List<string>();
            if (!TextRules.IsPrintableAscii(line))
            {
                broken.Add("holds a character that is not printable ASCII");
            }

            broken.AddRange(TextRules.BannedWordsIn(line).Select(word => "holds the word " + word));
            return broken;
        }
    }
}
