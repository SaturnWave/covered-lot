using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CoveredLot.Shared.Samples
{
    /// <summary>
    /// Code that keeps every rule the scanner checks, written with constructs that compile to look-alikes of the
    /// planted forms: a specific catch that handles the error, a finally (from using), an iterator state machine and
    /// checked arithmetic. The scanner must report nothing here, so an over-broad rule shows up at once.
    /// </summary>
    internal static class CleanSample
    {
        /// <summary>A specific catch that turns the technical error into an answer: allowed (the engine's AllowedCatchSample).</summary>
        public static string ParseOrExplain(string text)
        {
            try
            {
                return int.Parse(text, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }
            catch (FormatException error)
            {
                return "not a number: " + error.Message;
            }
        }

        /// <summary>In a checked context this compiles to add.ovf, sub.ovf and mul.ovf.</summary>
        public static int CheckedArithmetic(int first, int second) => (first + second) * (first - second);

        /// <summary>An iterator: its compiler-written state machine is not an async one.</summary>
        public static IEnumerable<int> CountUpTo(int limit)
        {
            for (int value = 0; value < limit; value++)
            {
                yield return value;
            }
        }

        /// <summary>A using statement compiles to try/finally, which is no catch.</summary>
        public static int FirstByte(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            return stream.ReadByte();
        }
    }
}
