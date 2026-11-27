using System;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// Puts a swatch's class on an element and takes every other swatch's class off it (StyleNames.Of), so an element
    /// shows one swatch at most and none it showed before survives a repaint. CoveredLot.uss says what each class paints
    /// on each kind of element: an open tile's or a filled bay's fill, a ghost's or a pill's outline.
    /// </summary>
    internal static class SwatchClasses
    {
        private static readonly Swatch[] Every = (Swatch[])Enum.GetValues(typeof(Swatch));

        /// <summary>Gives the element the swatch's class and no other swatch's; None takes every swatch's class off it.</summary>
        public static void Paint(VisualElement element, Option<Swatch> swatch)
        {
            bool hasSwatch = swatch.TryGet(out Swatch shown);
            foreach (Swatch each in Every)
            {
                element.EnableInClassList(StyleNames.Of(each), hasSwatch && each == shown);
            }
        }
    }
}
