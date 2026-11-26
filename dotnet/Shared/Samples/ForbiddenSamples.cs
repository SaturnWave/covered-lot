using System;
using System.Globalization;
using System.Threading.Tasks;

// Deliberate violations for the scanner's self-tests; the analyzer would reject the generic catches.
#pragma warning disable CA1031

namespace CoveredLot.Shared.Samples
{
    /// <summary>
    /// Catch blocks the code standard forbids, one per method: ported from the engine's ForbiddenCatchSample
    /// (tests/PuzzleCore.Tests/Guards/Samples/CatchSamples.cs). Compiled into the test assembly, which the scanner
    /// self-tests read.
    /// </summary>
    internal static class ForbiddenCatchSample
    {
        public static int CatchesEverything(string text)
        {
            try
            {
                return int.Parse(text, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static int CatchesEverythingWithAFilter(string text, bool isLenient)
        {
            try
            {
                return int.Parse(text, CultureInfo.InvariantCulture);
            }
            catch (Exception) when (isLenient)
            {
                return -1;
            }
        }

        public static int SwallowsSilently(string text)
        {
            int result = 0;
            try
            {
                result = int.Parse(text, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
            }

            return result;
        }

        public static int BareCatch(string text)
        {
            try
            {
                return int.Parse(text, CultureInfo.InvariantCulture);
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>Draws a number from System.Random, which no Covered Lot code may use.</summary>
    internal static class RandomSample
    {
        public static int RollADie() => new Random(7).Next(1, 7);
    }

    /// <summary>An async method: the compiler writes a state machine for it, with a generic catch inside.</summary>
    internal static class AsyncSample
    {
        public static async Task<int> CountAfterYieldingAsync(int count)
        {
            await Task.Yield();
            return count;
        }
    }

    /// <summary>Mutable static state, which Core may not keep: a static field and a static auto-property with a setter.</summary>
    internal static class MutableStaticSample
    {
        private static int _count;

        public static int Total { get; set; }

        public static int Next()
        {
            _count = _count + 1;
            return _count;
        }
    }
}
