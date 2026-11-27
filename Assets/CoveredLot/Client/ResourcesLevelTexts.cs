using CoveredLot.Core.Catalogue;
using PuzzleCore;
using UnityEngine;

namespace CoveredLot.Client
{
    /// <summary>
    /// The game's host of the level texts (design section 5): the TextAssets that Unity's Resources hold under
    /// Assets/Generated/Resources/Levels, which ship in the player. Each text is loaded by its Resources path, handed over
    /// unchanged, and the asset is unloaded once its text is read, whether or not the read succeeds. This is the one place
    /// where a missing asset, a null from Resources.Load, becomes None.
    /// </summary>
    public sealed class ResourcesLevelTexts : ILevelTexts
    {
        public Option<string> Read(LevelTextName name)
        {
            TextAsset? asset = Resources.Load<TextAsset>(name.ResourcesPath);
            if (asset == null)
            {
                return Option.None<string>();
            }

            try
            {
                return Option.Some(asset.text);
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }
    }
}
