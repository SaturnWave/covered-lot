using System.IO;
using CoveredLot.Core.Catalogue;
using PuzzleCore;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The level texts as the files in Assets/Generated/Resources/Levels hold them, a test-only host: each text is read when
    /// the catalogue asks for it, by the file name its <see cref="LevelTextName"/> gives, and decoded with strict UTF-8 that
    /// keeps every byte, a byte-order mark included. A name with no file gives None.
    /// </summary>
    internal sealed class DiskLevelTexts : ILevelTexts
    {
        public Option<string> Read(LevelTextName name)
        {
            string path = RepositoryLayout.FullPath(ShippedLevels.Folder + "/" + name.FileName);
            return File.Exists(path) ? Option.Some(ShippedLevels.StrictUtf8.GetString(File.ReadAllBytes(path))) : Option.None<string>();
        }
    }
}
