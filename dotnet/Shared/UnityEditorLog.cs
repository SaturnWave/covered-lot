using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The log rule of design section 10.4, over the editor log of a Unity test run (<c>-logFile</c>): no line reports a
    /// C# compiler warning ("warning CS" and its number) and names a file under Assets/CoveredLot or Assets/Tests, written
    /// with forward or back slashes. csc.rsp makes every warning in those assemblies an error, so this is a second line of
    /// defence, which holds for an assembly whose csc.rsp Unity does not apply. Warnings in packages and in other folders do
    /// not count.
    /// UnityEditorLogTests shows it on doctored logs (dotnet/Shared/Samples/UnityLogs).
    /// </summary>
    internal static class UnityEditorLog
    {
        private static readonly Regex WarningCode = new(@"\bwarning CS[0-9]+", RegexOptions.CultureInvariant);

        /// <summary>A path into our assemblies' folders, from Assets to the first space, colon or parenthesis.</summary>
        private static readonly Regex OwnSource = new(@"\bAssets[\\/](?:CoveredLot|Tests)[\\/][^\s:(]*", RegexOptions.CultureInvariant);

        /// <summary>
        /// Each line that reports a compiler warning in our code, as its line number in the log, the file and the warning
        /// code; the rest of the line is left out, so a report never repeats more of a log than that.
        /// </summary>
        public static IReadOnlyList<string> OwnCompilerWarnings(IEnumerable<string> lines)
        {
            var found = new List<string>();
            int number = 0;
            foreach (string line in lines)
            {
                number++;
                Match warning = WarningCode.Match(line);
                Match source = OwnSource.Match(line);
                if (warning.Success && source.Success)
                {
                    found.Add("line " + number.ToString(CultureInfo.InvariantCulture) + ": " + source.Value + " " + warning.Value);
                }
            }

            return found;
        }
    }
}
