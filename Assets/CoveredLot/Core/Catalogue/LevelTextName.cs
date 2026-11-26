using PuzzleCore.Records;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>
    /// A level text the catalogue asks the host for (design section 5): a level the presentation file lists, named by its
    /// level id; the presentation file; or the Example. Each carries both names a host may need: its path for
    /// Resources.Load, which takes no extension ("Levels/campaign-jam-0000"), and its file name in
    /// Assets/Generated/Resources/Levels, where tools/sync-engine.sh writes it ("campaign-jam-0000.txt"). Closed: only the
    /// catalogue names texts.
    /// </summary>
    public abstract class LevelTextName
    {
        private const string ResourcesFolder = "Levels/";
        private const string TextExtension = ".txt";
        private const string JsonExtension = ".json";

        /// <summary>The text's name in the Levels folder, without its extension: a level id, "presentation" or "six-by-six".</summary>
        private readonly string _assetName;

        private LevelTextName(string assetName) => _assetName = assetName;

        /// <summary>The path Resources.Load takes for the text: the Levels folder and the text's name, without an extension.</summary>
        public string ResourcesPath => ResourcesFolder + _assetName;

        /// <summary>The text's file name in Assets/Generated/Resources/Levels, with its extension.</summary>
        public abstract string FileName { get; }

        /// <summary>The text of a level the presentation file lists, which tools/sync-engine.sh names by the level's id.</summary>
        public sealed class Level : LevelTextName
        {
            internal Level(LevelId id)
                : base(id.Value) => Id = id;

            public LevelId Id { get; }

            public override string FileName => _assetName + TextExtension;
        }

        /// <summary>The pack's presentation file: the index of its levels, with each level's slot, fingerprint and grades.</summary>
        public sealed class Presentation : LevelTextName
        {
            internal Presentation()
                : base("presentation")
            {
            }

            public override string FileName => _assetName + JsonExtension;
        }

        /// <summary>The Example: the brief's covered 6x6 rules board, which the level list shows first.</summary>
        public sealed class Example : LevelTextName
        {
            internal Example()
                : base("six-by-six")
            {
            }

            public override string FileName => _assetName + TextExtension;
        }
    }
}
