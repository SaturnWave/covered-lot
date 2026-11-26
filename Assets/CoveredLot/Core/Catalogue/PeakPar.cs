using PuzzleCore;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// A level's par for the bays: the fewest bays it can be won with, which is also the lowest peak of bays in use over
    /// every winning order. A listed level's par is its presentation entry's plan.peakPar; the Example's comes from the
    /// engine's solver. A type of its own, so that a par cannot take the place of a peak or of any other count of bays.
    /// </summary>
    public readonly struct PeakPar
    {
        public PeakPar(CapacityCount bays) => Bays = bays;

        public CapacityCount Bays { get; }
    }
}
