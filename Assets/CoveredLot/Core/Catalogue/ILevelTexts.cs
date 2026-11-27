using PuzzleCore;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// The host's part of loading the levels: it reads the text that a <see cref="LevelTextName"/> names and hands it over
    /// unchanged, or None when it holds no such text. The game's host, the Client's ResourcesLevelTexts, reads Unity's
    /// Resources; the tests read the files on disk, Resources or texts of their own. What a text means is the catalogue's business, so a host neither
    /// checks nor changes one.
    /// </summary>
    public interface ILevelTexts
    {
        Option<string> Read(LevelTextName name);
    }
}
