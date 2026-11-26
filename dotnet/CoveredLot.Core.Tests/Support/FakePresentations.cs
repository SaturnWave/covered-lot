namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Presentation files the engine's reader accepts or refuses, for the catalogue tests. The catalogue never compares a
    /// level's fingerprint with its text (only the tests do), so the fakes give each level a fingerprint of zeros.
    /// </summary>
    internal static class FakePresentations
    {
        /// <summary>A Jam pack that lists no level.</summary>
        public const string EmptyJamPack =
            """{"format":"pc-presentation","formatVersion":1,"game":"Jam","levels":[],"packId":"campaign-jam"}""";

        /// <summary>A Sort pack that lists no level.</summary>
        public const string EmptySortPack =
            """{"format":"pc-presentation","formatVersion":1,"game":"Sort","levels":[],"packId":"campaign-sort"}""";

        /// <summary>A Sort pack that lists one Plan level, campaign-sort-0000 in slot 0, without a par, as no Sort level has one.</summary>
        public const string SortPackOfOneLevel =
            $$"""{"format":"pc-presentation","formatVersion":1,"game":"Sort","levels":[{{SortLevel0000}}],"packId":"campaign-sort"}""";

        /// <summary>A Sort pack that lists campaign-sort-1234 and campaign-sort-12345, whose slots take four and five digits.</summary>
        public const string SortPackOfLongSlots =
            $$"""{"format":"pc-presentation","formatVersion":1,"game":"Sort","levels":[{{SortLevel1234}},{{SortLevel12345}}],"packId":"campaign-sort"}""";

        /// <summary>A Jam pack whose one level has no peakPar, which the engine's reader refuses: every Jam level has a par.</summary>
        public const string JamPackWithALevelWithoutPar =
            $$"""{"format":"pc-presentation","formatVersion":1,"game":"Jam","levels":[{{JamLevel0000WithoutPar}}],"packId":"campaign-jam"}""";

        private const string Zeros = "0000000000000000000000000000000000000000000000000000000000000000";

        private const string SortLevel0000 =
            $$"""{"fingerprint":"{{Zeros}}","kind":"Plan","levelId":"campaign-sort-0000","plan":{"deadEnd":0,"minFreeCapacity":1,"par":15,"spareNeed":0},"slot":0}""";

        private const string SortLevel1234 =
            $$"""{"fingerprint":"{{Zeros}}","kind":"Plan","levelId":"campaign-sort-1234","plan":{"deadEnd":0,"minFreeCapacity":1,"par":15,"spareNeed":0},"slot":1234}""";

        private const string SortLevel12345 =
            $$"""{"fingerprint":"{{Zeros}}","kind":"Plan","levelId":"campaign-sort-12345","plan":{"deadEnd":0,"minFreeCapacity":1,"par":15,"spareNeed":0},"slot":12345}""";

        private const string JamLevel0000WithoutPar =
            $$"""{"fingerprint":"{{Zeros}}","kind":"Plan","levelId":"campaign-jam-0000","plan":{"deadEnd":452,"minFreeCapacity":2,"par":9,"spareNeed":0},"slot":0}""";
    }
}
