using UnityEngine;

namespace CoveredLot.Client
{
    /// <summary>
    /// Makes Unity's floats whole and non-negative before they become Core's pixel and panel-unit values, whose axis types
    /// throw on a negative value (design section 6): Screen.safeArea, the panel's and the frame's layout sizes.
    /// A layout value is NaN before the element's first layout, and a NaN, a negative value or zero gives 0. A value
    /// beyond the largest int gives the largest int.
    /// </summary>
    public static class Whole
    {
        /// <summary>The largest whole number at most the value, never negative: 343.6 gives 343.</summary>
        public static int Floor(float value)
        {
            if (!(value > 0f))
            {
                return 0;
            }

            return value >= int.MaxValue ? int.MaxValue : Mathf.FloorToInt(value);
        }

        /// <summary>The smallest whole number at least the value, never negative: 62.4 gives 63.</summary>
        public static int Ceiling(float value)
        {
            if (!(value > 0f))
            {
                return 0;
            }

            return value >= int.MaxValue ? int.MaxValue : Mathf.CeilToInt(value);
        }
    }
}
