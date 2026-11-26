using System;
using System.Collections.Generic;
using System.IO;
using CoveredLot.Core.Tests.Support;
using CoveredLot.Shared;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The log rule (<see cref="UnityEditorLog"/>), which UnityChecks applies to the editor logs of the four Unity test runs,
    /// shown on doctored logs (dotnet/Shared/Samples/UnityLogs): warnings in packages and in other folders, and lines that
    /// name our folders without a warning, pass; each warning about a file under Assets/CoveredLot or Assets/Tests is
    /// reported with its line, its file and its code, whichever slash the path is written with.
    /// </summary>
    public sealed class UnityEditorLogTests
    {
        private const string Logs = "dotnet/Shared/Samples/UnityLogs/";

        [Fact]
        public void A_log_with_warnings_only_in_packages_and_other_folders_passes()
        {
            IReadOnlyList<string> lines = Lines("clean.txt");

            IReadOnlyList<string> found = UnityEditorLog.OwnCompilerWarnings(lines);

            // The log holds compiler warnings and names our folders, just never both on one line.
            Assert.Contains(lines, line => line.Contains("warning CS", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("Assets/CoveredLot/", StringComparison.Ordinal));
            Assert.Contains(lines, line => line.Contains("Assets/Tests/", StringComparison.Ordinal));
            Assert.True(found.Count == 0, "clean.txt is refused: " + string.Join("; ", found));
        }

        [Fact]
        public void Each_warning_about_our_code_is_reported_with_its_line_file_and_code()
        {
            IReadOnlyList<string> found = UnityEditorLog.OwnCompilerWarnings(Lines("own-warnings.txt"));

            Assert.Equal(
                new[]
                {
                    "line 4: Assets/CoveredLot/Client/GameRoot.cs warning CS0168",
                    "line 5: Assets/Tests/EditMode/CoreInUnityTests.cs warning CS0219",
                    @"line 6: Assets\CoveredLot\Core\Copy.cs warning CS0414",
                },
                found);
        }

        private static IReadOnlyList<string> Lines(string log) => File.ReadAllLines(RepositoryLayout.FullPath(Logs + log));
    }
}
