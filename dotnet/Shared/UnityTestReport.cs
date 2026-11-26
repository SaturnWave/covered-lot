using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The results rule of design section 10.4, over the NUnit 3 XML report that a Unity test run writes (<c>unity test
    /// --output</c>). The editor exits 0 when no test ran, so the exit code alone proves nothing: a report proves a run only
    /// when it holds at least one test, every test passed (none failed, none was skipped, ignored or inconclusive) and the
    /// culture guard's tests are among them and passed. UnityTestReportTests shows each problem on a doctored report
    /// (dotnet/Shared/Samples/UnityReports).
    /// </summary>
    internal static class UnityTestReport
    {
        private const string Passed = "Passed";

        /// <summary>How many test names a problem lists before it only counts the rest.</summary>
        private const int NamesShown = 20;

        private static readonly string[] Counts = { "total", "passed", "failed", "skipped", "inconclusive" };

        /// <summary>
        /// What keeps the report from proving that every test passed, the culture guard's included; empty when it proves it.
        /// <paramref name="cultureGuardClass"/> is the full name of the culture guard's fixture in the run's test assembly,
        /// for example CoveredLot.Tests.EditMode.CultureGuardTests, which starts the full name of each of its tests. Their
        /// classname is no help: it names the class that declares a test, which for the guard is the shared base class.
        /// </summary>
        public static IReadOnlyList<string> Problems(string xml, string cultureGuardClass)
        {
            XDocument report;
            try
            {
                report = XDocument.Parse(xml);
            }
            catch (XmlException error)
            {
                // A report cut short by a crash or a timeout: the parser's own message says where it ends.
                return new[] { "does not parse as XML: " + error.Message };
            }

            if (!(report.Root is XElement run) || run.Name.LocalName != "test-run")
            {
                return new[] { "is not an NUnit 3 report: its root element is not test-run" };
            }

            var problems = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string name in Counts)
            {
                if (CountOf(run, name) is int count)
                {
                    counts[name] = count;
                }
                else
                {
                    problems.Add("its test-run element has no whole-number " + name + " count");
                }
            }

            if (problems.Count > 0)
            {
                return problems;
            }

            IReadOnlyList<XElement> cases = run.Descendants("test-case").ToList();
            problems.AddRange(CountProblems(counts, cases));
            problems.AddRange(CultureGuardProblems(cases, cultureGuardClass));
            return problems;
        }

        private static IEnumerable<string> CountProblems(IReadOnlyDictionary<string, int> counts, IReadOnlyList<XElement> cases)
        {
            int total = counts["total"];
            if (total == 0)
            {
                yield return "ran no test (total is 0)";
            }

            if (counts["failed"] > 0)
            {
                yield return Text(counts["failed"]) + " failed: " + NamesWithResult(cases, "Failed");
            }

            if (counts["skipped"] > 0)
            {
                yield return Text(counts["skipped"]) + " skipped or ignored, which no check allows: " + NamesWithResult(cases, "Skipped");
            }

            if (counts["inconclusive"] > 0)
            {
                yield return Text(counts["inconclusive"]) + " inconclusive: " + NamesWithResult(cases, "Inconclusive");
            }

            if (counts["passed"] != total)
            {
                yield return Text(counts["passed"]) + " of " + Text(total) + " passed";
            }
        }

        private static IEnumerable<string> CultureGuardProblems(IReadOnlyList<XElement> cases, string cultureGuardClass)
        {
            IReadOnlyList<XElement> guards = cases
                .Where(testCase => ((string?)testCase.Attribute("fullname") ?? string.Empty).StartsWith(cultureGuardClass + ".", StringComparison.Ordinal))
                .ToList();
            if (guards.Count == 0)
            {
                yield return "holds no test of the culture guard " + cultureGuardClass;
            }

            foreach (XElement guard in guards.Where(testCase => (string?)testCase.Attribute("result") != Passed))
            {
                yield return "the culture guard's " + NameOf(guard) + " reported " + ((string?)guard.Attribute("result") ?? "no result");
            }
        }

        /// <summary>The count the attribute holds, written as a whole number without sign or separators; null when it holds none.</summary>
        private static int? CountOf(XElement run, string attribute) =>
            int.TryParse((string?)run.Attribute(attribute), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : null;

        private static string NamesWithResult(IReadOnlyList<XElement> cases, string result)
        {
            IReadOnlyList<string> names = cases.Where(testCase => (string?)testCase.Attribute("result") == result).Select(NameOf).ToList();
            string shown = string.Join(", ", names.Take(NamesShown));
            return names.Count > NamesShown ? shown + " and " + Text(names.Count - NamesShown) + " more" : shown;
        }

        private static string NameOf(XElement testCase) => (string?)testCase.Attribute("fullname") ?? (string?)testCase.Attribute("name") ?? "an unnamed test";

        private static string Text(int count) => count.ToString(CultureInfo.InvariantCulture);
    }
}
