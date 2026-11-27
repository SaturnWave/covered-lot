using CoveredLot.Core.Pictures;
using PuzzleCore;
using UnityEngine.UIElements;

namespace CoveredLot.Client
{
    /// <summary>
    /// The number of a tagged vehicle (design section 9), on its tile, on its ghost and on its row of the tags list. It
    /// shows the number its picture gives, and is hidden for a vehicle that carries no tag.
    /// </summary>
    internal sealed class BadgeLabel : Label
    {
        public BadgeLabel()
        {
            AddToClassList(StyleNames.Badge);
            pickingMode = PickingMode.Ignore;
        }

        /// <summary>Shows the number, written by Core; None hides the badge.</summary>
        public void Show(Option<ClueNumber> number)
        {
            if (number.TryGet(out ClueNumber shown))
            {
                text = shown.Text;
                style.display = DisplayStyle.Flex;
            }
            else
            {
                text = string.Empty;
                style.display = DisplayStyle.None;
            }
        }
    }
}
