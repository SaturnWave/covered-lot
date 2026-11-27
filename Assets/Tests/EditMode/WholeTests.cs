using CoveredLot.Client;
using CoveredLot.Tests.Support;
using NUnit.Framework;

namespace CoveredLot.Tests.EditMode
{
    /// <summary>
    /// How the Client makes Unity's floats whole and non-negative before they become Core's pixel and panel-unit values,
    /// which throw on a negative value (Whole): Floor rounds down and Ceiling up, and a NaN, as a layout gives before its
    /// first pass, a negative value and zero give 0; a value beyond the largest int gives the largest int.
    /// </summary>
    [HostileCulture]
    public sealed class WholeTests
    {
        [Test]
        public void Floor_rounds_down_and_gives_0_for_NaN_negative_and_zero_and_the_largest_int_beyond_it()
        {
            int[] floors =
            {
                Whole.Floor(float.NaN), Whole.Floor(-1.5f), Whole.Floor(0f), Whole.Floor(343.6f), Whole.Floor(344f),
                Whole.Floor(float.PositiveInfinity),
            };

            Assert.That(floors, Is.EqualTo(new[] { 0, 0, 0, 343, 344, int.MaxValue }), "Floor of NaN, -1.5, 0, 343.6, 344 and infinity");
        }

        [Test]
        public void Ceiling_rounds_up_and_gives_0_for_NaN_negative_and_zero_and_the_largest_int_beyond_it()
        {
            int[] ceilings =
            {
                Whole.Ceiling(float.NaN), Whole.Ceiling(-0.5f), Whole.Ceiling(0f), Whole.Ceiling(62.4f), Whole.Ceiling(63f),
                Whole.Ceiling(float.PositiveInfinity),
            };

            Assert.That(ceilings, Is.EqualTo(new[] { 0, 0, 0, 63, 63, int.MaxValue }), "Ceiling of NaN, -0.5, 0, 62.4, 63 and infinity");
        }
    }
}
