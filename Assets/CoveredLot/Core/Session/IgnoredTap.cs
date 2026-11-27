namespace CoveredLot.Core.Session
{
    /// <summary>Why a tap was ignored: it moved no vehicle and bumped none.</summary>
    public enum IgnoredTap
    {
        /// <summary>No vehicle with that id is in the lot: it has already left, or the level has none.</summary>
        NotInLot,

        /// <summary>The level is over, won or failed, so no tap moves anything.</summary>
        LevelOver,
    }
}
