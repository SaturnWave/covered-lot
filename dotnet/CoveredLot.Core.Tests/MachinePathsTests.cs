using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The machine-path patterns that UnityProjectFilesTests and GeneratedFilesTests apply: each planted form of a
    /// machine path is reported, and the project text that only looks like one passes. The planted drive letters and
    /// users folders are split in two, so this file passes tools/scan-before-push.sh.
    /// </summary>
    public sealed class MachinePathsTests
    {
        [Theory]
        [InlineData("path: C" + @":\Work\Game\Assets")]
        [InlineData("path: C" + ":/Work/Game/Assets")]
        [InlineData("\"path\": \"C" + @":\\Work\\Game""")]
        [InlineData("file:///C" + ":/Work/Game")]
        [InlineData("cd /c/Work/Game")]
        [InlineData("\"/d/Unity/Cache\"")]
        [InlineData("/mnt/c/Work/Game")]
        [InlineData(@"path: \\buildserver\share\Game")]
        [InlineData(@"""\\\\buildserver\\share""")]
        public void A_drive_letter_path_or_a_network_share_is_reported(string text)
        {
            Assert.Matches(MachinePaths.DriveLetterOrSharePath, text);
        }

        [Theory]
        [InlineData("/Us" + "ers/someone/Game")]
        [InlineData(@"path: \Us" + @"ers\someone\Game")]
        [InlineData("/home/someone/Game")]
        [InlineData("/HOME/someone/Game")]
        [InlineData("~/Game")]
        [InlineData("\"~/Game\"")]
        public void A_user_folder_or_a_home_relative_path_is_reported(string text)
        {
            Assert.Matches(MachinePaths.UserFolder, text);
        }

        [Theory]
        [InlineData("m_Path: Assets/Settings/UniversalRP.asset")]
        [InlineData(@"""path"": ""Assets\\Settings\\UniversalRP.asset""")]
        [InlineData(@"userData: Assets\Settings")]
        [InlineData(@"string unix = path.Replace('\\', '/');")]
        [InlineData(@"var separator = new Regex(@""[\\/]"");")]
        [InlineData(@"""\\""")]
        [InlineData("m_Path: Assets/CoveredLot/UI/Home/Menu.uss")]
        [InlineData("@import url(\"../Home/Menu.uss\");")]
        [InlineData("// See com.unity.ui.test-framework/Documentation~/index.md")]
        [InlineData("m_Path: ../Shared/Levels/a/b.txt")]
        [InlineData("\"url\": \"https://example.com/u/someone/a/b\"")]
        [InlineData("background-image: url(\"project://database/Assets/CoveredLot/UI/CoveredLot.uss\");")]
        public void Project_text_that_only_looks_like_a_machine_path_passes(string text)
        {
            Assert.DoesNotMatch(MachinePaths.DriveLetterOrSharePath, text);
            Assert.DoesNotMatch(MachinePaths.UserFolder, text);
        }
    }
}
