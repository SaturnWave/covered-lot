using PuzzleCore;

namespace CoveredLot.Core.Session
{
    /// <summary>
    /// The most bays in use at once in an attempt, the peak that earns the stars (design section 8.3): counted when a
    /// vehicle parks, before its set leaves. The engine parks a vehicle in the leftmost free bay, so a vehicle that parks in
    /// bay i, counted from 0, makes i + 1 bays in use. default(Peak), no bays, is the peak before any move. A type of its
    /// own, so that a peak cannot take the place of a par or of any other count of bays.
    /// </summary>
    public readonly struct Peak
    {
        public Peak(CapacityCount bays) => Bays = bays;

        /// <summary>The bays in use at the peak.</summary>
        public CapacityCount Bays { get; }

        /// <summary>The peak once a vehicle has parked in <paramref name="bay"/>: this one, or the bays then in use if they are more.</summary>
        internal Peak Including(SlotIndex bay)
        {
            var inUse = new CapacityCount(bay.Value + 1);
            return inUse > Bays ? new Peak(inUse) : this;
        }
    }
}
