namespace CoveredLot.Shared.Samples
{
    /// <summary>
    /// Integer arithmetic in a checked context and outside one, one instruction per method, for the self-tests of the scan
    /// for overflow-checked arithmetic (HygieneTests). Each method opens its own context, so the samples do not depend on
    /// the overflow checks this test assembly is built with. Compiled into the test assembly, which the self-tests read.
    /// </summary>
    internal static class OverflowSamples
    {
        /// <summary>add.ovf.</summary>
        public static int Next(int value) => checked(value + 1);

        /// <summary>add.ovf.un.</summary>
        public static uint NextUnsigned(uint value) => checked(value + 1U);

        /// <summary>sub.ovf.</summary>
        public static int Previous(int value) => checked(value - 1);

        /// <summary>sub.ovf.un.</summary>
        public static uint PreviousUnsigned(uint value) => checked(value - 1U);

        /// <summary>mul.ovf.</summary>
        public static int Doubled(int value) => checked(value * 2);

        /// <summary>mul.ovf.un.</summary>
        public static uint DoubledUnsigned(uint value) => checked(value * 2U);

        /// <summary>A plain add, which wraps instead of throwing.</summary>
        public static int NextWrapping(int value) => unchecked(value + 1);
    }
}
