using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CoveredLot.Shared.Samples
{
    /// <summary>
    /// Code that keeps every rule the engine-style scans check, written with constructs that compile to look-alikes
    /// of the planted forms: a specific catch that handles the error, a finally (from using), an iterator state
    /// machine, and static data that cannot be reassigned next to the static field the compiler writes to cache a
    /// lambda. The scanner must report nothing here, so an over-broad rule shows up at once. Only Core's stricter
    /// rule, no catch block at all, reports the specific catch.
    /// </summary>
    internal static class CleanSample
    {
        private const int Base = 10;

        private static readonly int[] Primes = { 2, 3, 5, 7 };

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

        /// <summary>A lambda that captures nothing: the compiler caches it in a static field of its own &lt;&gt;c type.</summary>
        public static IReadOnlyList<int> ScaledPrimes() => Primes.Select(prime => prime * Base).ToList();
    }
}
