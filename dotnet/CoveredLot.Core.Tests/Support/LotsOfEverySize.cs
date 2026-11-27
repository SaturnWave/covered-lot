using System.Collections.Generic;
using System.Globalization;
using CoveredLot.Core.Catalogue;
using PuzzleCore;
using PuzzleCore.Jam;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Every lot size the engine allows, and a plain level of each, for the geometry tests: the shipped levels are all
    /// square, and only a lot whose width and height differ shows a width taken for a height. Each level is loaded by the
    /// catalogue as the Example of a pack that lists no level, so a session is made from it as the game makes one.
    /// </summary>
    internal static class LotsOfEverySize
    {
        /// <summary>The 16 sizes, widths and heights each of 5 to 8 cells (LotSize.MinSide to MaxSide), the width outermost.</summary>
        public static IReadOnlyList<LotSize> Sizes()
        {
            var sizes = new List<LotSize>();
            for (int width = LotSize.MinSide; width <= LotSize.MaxSide; width++)
            {
                for (int height = LotSize.MinSide; height <= LotSize.MaxSide; height++)
                {
                    sizes.Add(LotSize.Of(width, height).TryGet(out LotSize size)
                        ? size
                        : throw new XunitException("The engine refuses a lot of " + Number(width) + "x" + Number(height) + "."));
                }
            }

            return sizes;
        }

        /// <summary>
        /// The boxes of the level's six vehicles in PieceId order, as (row,column,rows x columns) for a lot W wide and H high:
        /// (0,0,2x1), (0,W-3,1x3), (2,0,1x2), (H-3,W-1,3x1), (H-2,2,2x1) and (H-1,0,1x2), so that a box stands in each corner
        /// and the boxes run both ways, two and three cells long.
        /// </summary>
        public static IReadOnlyList<string> BoxesOf(LotSize size)
        {
            int width = size.Width;
            int height = size.Height;
            return new[]
            {
                "(0,0,2x1)",
                "(0," + Number(width - 3) + ",1x3)",
                "(2,0,1x2)",
                "(" + Number(height - 3) + "," + Number(width - 1) + ",3x1)",
                "(" + Number(height - 2) + ",2,2x1)",
                "(" + Number(height - 1) + ",0,1x2)",
            };
        }

        /// <summary>
        /// A plain level of this size with four bays, whose boxes BoxesOf lists: three red and three blue vehicles, each at
        /// an edge of the lot and facing out of it, so that no vehicle waits on another and the level is won by driving out
        /// the reds and then the blues.
        /// </summary>
        public static string Text(LotSize size)
        {
            int width = size.Width;
            int height = size.Height;
            string vehicles = "R:0:2:N;R:" + Number(width - 3) + ":3:E;B:" + Number(2 * width) + ":2:W;B:" + Number(((height - 2) * width) - 1)
                + ":3:S;R:" + Number(((height - 2) * width) + 2) + ":2:S;B:" + Number((height - 1) * width) + ":2:W";
            return "pc-lot\nr=1\nsize=" + size.ToString() + "\nbays=4\npillars=\nvehicles=" + vehicles + "\ncovers=\nclues=";
        }

        /// <summary>The catalogue's entry for the level of this size, loaded as the Example of a pack that lists no level.</summary>
        public static CatalogueEntry Entry(LotSize size)
        {
            FakeLevelTexts texts = FakeLevelTexts.Nothing.WithPresentation(FakePresentations.EmptyJamPack).WithExample(Text(size));
            ReadOnlyArray<CatalogueEntry> entries = CatalogueDescriptions.EntriesOf(LevelCatalogue.Load(texts));
            return entries.Count == 1 && entries[0].Title is EntryTitle.Example
                ? entries[0]
                : throw new XunitException("The catalogue gave " + Number(entries.Count) + " entries for the one level of " + size.ToString() + ".");
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
