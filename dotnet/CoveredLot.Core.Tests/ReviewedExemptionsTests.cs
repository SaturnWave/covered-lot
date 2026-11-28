using System.Collections.Generic;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The member bans' one reviewed exemption (ReviewedExemptions, design section 10.4): it covers the reads of
    /// CultureInfo.CurrentCulture and CultureInfo.CurrentUICulture inside two methods of CoveredLot.Client.DevCultureSwitch,
    /// CurrentName and ReadCultures, and nothing else. The same read in another method of that type (its constructor
    /// included), in a type nested in it, in another type or in a type whose name only starts the same, and every other
    /// banned member in those two methods, stay refused. UnityChecks pins, on Unity's build of the Client, the exact reads
    /// the exemption covers.
    /// </summary>
    public sealed class ReviewedExemptionsTests
    {
        private const string CurrentCultureRead = "System.Globalization.CultureInfo::get_CurrentCulture(): culture-dependent text";
        private const string CurrentUiCultureRead = "System.Globalization.CultureInfo::get_CurrentUICulture(): culture-dependent text";

        [Theory]
        [InlineData("CoveredLot.Client.DevCultureSwitch::CurrentName+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::ReadCultures+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::ReadCultures+IL_0005", CurrentUiCultureRead)]
        public void The_exemption_covers_the_culture_reads_of_CurrentName_and_ReadCultures(string location, string reason)
        {
            var violation = new Violation { Location = location, Reason = reason };

            Assert.Equal(new[] { violation }, ReviewedExemptions.Covered(new[] { violation }));
            Assert.Empty(ReviewedExemptions.Without(new[] { violation }));
        }

        [Theory]
        [InlineData("CoveredLot.Client.DevCultureSwitch::.ctor+IL_0001", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::Next+IL_0010", CurrentUiCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::CurrentNameOf+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.ScreenHost::RefreshDevLine+IL_0010", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch/CultureSet::Apply+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch+CultureSet::Apply+IL_0000", CurrentUiCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitchTwo::CurrentName+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Editor.DevCultureSwitch::CurrentName+IL_0000", CurrentCultureRead)]
        [InlineData("CoveredLot.Client.DevCultureSwitch::CurrentName+IL_0004", "System.StringComparer::get_CurrentCulture(): text in a culture's order")]
        [InlineData("CoveredLot.Client.DevCultureSwitch::ReadCultures+IL_0004", "System.Int32::ToString(): culture-dependent text")]
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
