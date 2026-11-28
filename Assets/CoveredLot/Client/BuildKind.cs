namespace CoveredLot.Client
{
    /// <summary>
    /// The kind of build the game runs in (design section 9): a release build, or a development build (Debug.isDebugBuild),
    /// the only kind that shows the development line, the level list's culture button and the problem screen's lines.
    /// </summary>
    public enum BuildKind
    {
        Release,
        Development,
    }
}
