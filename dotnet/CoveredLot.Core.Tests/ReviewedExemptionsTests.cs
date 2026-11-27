using System.Collections.Generic;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The member bans' one reviewed exemption (ReviewedExemptions, design section 10.4): it covers the reads of
    /// CultureInfo.CurrentCulture and CultureInfo.CurrentUICulture inside the own methods of CoveredLot.Client.DevCultureSwitch,
    /// and nothing else. The same read in another type, in a type nested in that one or in a type whose name only starts
    /// the same, and every other banned member in that type, stay refused. UnityChecks shows on Unity's build of the
    /// Client that the reads the exemption covers are really there.
    /// </summary>
    public sealed class ReviewedExemptionsTests
    {
        private const string CurrentCultureRead = "System.Globalization.CultureInfo::get_CurrentCulture(): culture-dependent text";
        private const string CurrentUiCultureRead = "System.Globalization.CultureInfo::get_CurrentUICulture(): culture-dependent text";

        [Theory]
        [InlineData("CoveredLot.Client.DevCultureSwitch::.ctor+IL_0001", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::.ctor+IL_000C", CurrentUiCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::CurrentName+IL_0000", CurrentCultureRead)]
        public void The_exemption_covers_the_two_culture_reads_inside_DevCultureSwitch(string location, string reason)
        {
            var violation = new Violation { Location = location, Reason = reason };

            Assert.Equal(new[] { violation }, ReviewedExemptions.Covered(new[] { violation }));
            Assert.Empty(ReviewedExemptions.Without(new[] { violation }));
        }

        [Theory]
        [InlineData("CoveredLot.Client.ScreenHost::RefreshDevLine+IL_0010", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch/Nested::Run+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch+Nested::Run+IL_0000", CurrentUiCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitchTwo::Run+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Editor.DevCultureSwitch::Run+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::Next+IL_0004", "System.StringComparer::get_CurrentCulture(): text in a culture's order")]
        [InlineData("CoveredLot.Client.DevCultureSwitch::Next+IL_0004", "System.Int32::ToString(): culture-dependent text")]
        [InlineData("type reference System.Random", CurrentCultureRead)]
        public void The_exemption_leaves_every_other_violation_refused(string location, string reason)
        {
            var violation = new Violation { Location = location, Reason = reason };

            Assert.Empty(ReviewedExemptions.Covered(new[] { violation }));
            Assert.Equal(new[] { violation }, ReviewedExemptions.Without(new[] { violation }));
        }

        [Fact]
        public void Without_keeps_the_order_of_the_violations_it_leaves()
        {
            var first = new Violation { Location = "CoveredLot.Client.ScreenHost::A+IL_0000", Reason = CurrentCultureRead };
            var exempt = new Violation { Location = "CoveredLot.Client.DevCultureSwitch::CurrentName+IL_0000", Reason = CurrentCultureRead };
            var last = new Violation { Location = "CoveredLot.Client.PlayView::B+IL_0000", Reason = CurrentUiCultureRead };

            IReadOnlyList<Violation> left = ReviewedExemptions.Without(new[] { first, exempt, last });

            Assert.Equal(new[] { first, last }, left);
        }
    }
}
