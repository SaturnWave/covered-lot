using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The log rule of design section 10.4, over the editor log of a Unity test run (<c>-logFile</c>):
    /// <list type="bullet">
    /// <item><description>the log is a finished log of the editor the project pins: its start line names that version
    /// ("Version is '6000.3.29f1 ..."), and the Test Framework wrote that the test run completed ("Test run completed.").
    /// An empty log, a log of another editor and a log cut short by a crash or a timeout prove nothing;</description></item>
    /// <item><description>no line reports a C# compiler warning ("warning CS" and its number) about a file under
    /// Assets/CoveredLot or Assets/Tests, written with forward or back slashes. Warnings in packages and other folders do
    /// not count, and neither does the output of a compile that the same log shows again later for the same assembly:
    /// Bee repeats a compile when its first pass ran on an out-of-date build graph ("Tundra requires additional run",
    /// after a change of an asmdef or a csc.rsp), and what Unity loads is what the last compile wrote.</description></item>
    /// </list>
    /// Bee writes a "Csc" line for each assembly it compiles, then what the compiler printed; Unity repeats the messages of
    /// the build after it. So the rule sees a warning only where Unity compiled during the run: when no source changed since
    /// Bee's last build, Bee copies what it built before and the log holds no compiler output at all.
    /// <see cref="UncompiledAssemblies"/> therefore names the assemblies that a set of logs never shows compiled:
    /// UnityChecks requires the four runs together to compile every CoveredLot assembly, and tools/check.sh deletes Bee's
    /// build of them so that its first run does. Even so, what keeps warnings out of our assemblies is csc.rsp's
    /// -warnaserror+, which AsmdefGuardTests pins; this rule is a second line of defence for an assembly whose csc.rsp
    /// Unity does not apply. UnityEditorLogTests shows each part on doctored logs (dotnet/Shared/Samples/UnityLogs).
    /// </summary>
    internal static class UnityEditorLog
    {
        /// <summary>The line the Test Framework writes when a command-line test run ends, whatever its verdict.</summary>
        private const string RunCompletedLine = "Test run completed.";

        private static readonly Regex WarningCode = new(@"\bwarning CS[0-9]+", RegexOptions.CultureInvariant);

        /// <summary>A path into our assemblies' folders, from Assets to the first space, colon or parenthesis.</summary>
        private static readonly Regex OwnSource = new(@"\bAssets[\\/](?:CoveredLot|Tests)[\\/][^\s:(]*", RegexOptions.CultureInvariant);

        /// <summary>
        /// Bee's line for one finished compile, which its output follows: "[571/722    2s] Csc Library/Bee/artifacts/1900b0aE.dag/CoveredLot.Client.dll
        /// (+2 others)". The "[BUSY       6s] Csc ..." line Bee writes while a long compile still runs is no compile of its own.
        /// </summary>
        private static readonly Regex CompileLine = new(
            @"^\[\s*[0-9]+/[0-9]+[^\]]*\] Csc Library[\\/]Bee[\\/]artifacts[\\/][^\\/\s]+[\\/](?<assembly>[^\\/\s]+)\.dll(?=\s|$)",
            RegexOptions.CultureInvariant);

        /// <summary>A line that ends a compile's output: Bee's next step ("[572/722    0s] CopyFiles ...") or a Tundra status line ("*** Tundra ...").</summary>
        private static readonly Regex EndOfCompileOutput = new(@"^(?:\[\s*[0-9]+/[0-9]+[^\]]*\] |\*\*\* )", RegexOptions.CultureInvariant);

        /// <summary>
        /// What keeps the log from proving a finished test run of the editor <paramref name="editorVersion"/>; empty when it
        /// proves one.
        /// </summary>
        public static IReadOnlyList<string> FinishedRunProblems(IReadOnlyList<string> lines, string editorVersion)
        {
            if (lines.Count == 0)
            {
                return new[] { "is empty" };
            }

            var startLine = new Regex("Version is '" + Regex.Escape(editorVersion) + "[ ']", RegexOptions.CultureInvariant);
            var problems = new List<string>();
            if (!lines.Any(startLine.IsMatch))
            {
                problems.Add("has no start line of editor " + editorVersion + " (\"Version is '" + editorVersion + " ...\"), so it is no log of the editor the project pins");
            }

            if (!lines.Any(line => line.Contains(RunCompletedLine, StringComparison.Ordinal)))
            {
                problems.Add("never records the end of a test run (\"" + RunCompletedLine + "\"): the run was cut short, or no test run started");
            }

            return problems;
        }

        /// <summary>
        /// Each line that reports a compiler warning about our code, leaving out the output of a compile that a later
        /// compile of the same assembly in this log supersedes; as its line number in the log, the file and the warning code.
        /// The rest of the line is left out, so a report never repeats more of a log than that.
        /// </summary>
        public static IReadOnlyList<string> OwnCompilerWarnings(IReadOnlyList<string> lines)
        {
            var outsideCompiles = new List<Finding>();
            var lastCompileOf = new Dictionary<string, List<Finding>>(StringComparer.Ordinal);
            List<Finding>? compileOutput = null;
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index];
                Match compile = CompileLine.Match(line);
                if (compile.Success)
                {
                    // A later compile of the assembly replaces the findings of an earlier one.
                    compileOutput = new List<Finding>();
                    lastCompileOf[compile.Groups["assembly"].Value] = compileOutput;
                }
                else if (EndOfCompileOutput.IsMatch(line))
                {
                    compileOutput = null;
                }
                else if (OwnWarningOn(line, index + 1) is Finding finding)
                {
                    (compileOutput ?? outsideCompiles).Add(finding);
                }
            }

            return lastCompileOf.Values.SelectMany(findings => findings)
                .Concat(outsideCompiles)
                .OrderBy(finding => finding.LineNumber)
                .Select(finding => finding.Text)
                .ToList();
        }

        /// <summary>The assemblies that the log shows Bee compiling, by the names their asmdefs give them, in ordinal order.</summary>
        public static IReadOnlyList<string> CompiledAssemblies(IEnumerable<string> lines) => lines
            .Select(line => CompileLine.Match(line))
            .Where(compile => compile.Success)
            .Select(compile => compile.Groups["assembly"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(assembly => assembly, StringComparer.Ordinal)
            .ToList();

        /// <summary>Those of <paramref name="assemblies"/> that none of the logs shows compiled, in the order given.</summary>
        public static IReadOnlyList<string> UncompiledAssemblies(IEnumerable<IReadOnlyList<string>> logs, IEnumerable<string> assemblies)
        {
            ISet<string> compiled = logs.SelectMany(CompiledAssemblies).ToHashSet(StringComparer.Ordinal);
            return assemblies.Where(assembly => !compiled.Contains(assembly)).ToList();
        }

        private static Finding? OwnWarningOn(string line, int lineNumber)
        {
            Match warning = WarningCode.Match(line);
            Match source = OwnSource.Match(line);
            return warning.Success && source.Success
                ? new Finding(lineNumber, "line " + lineNumber.ToString(CultureInfo.InvariantCulture) + ": " + source.Value + " " + warning.Value)
                : null;
        }

        /// <summary>A warning about our code: the log line it is on, from 1, and how a report names it.</summary>
        private sealed record Finding(int LineNumber, string Text);
    }
}
