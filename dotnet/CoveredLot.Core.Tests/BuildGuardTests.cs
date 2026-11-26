using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The build rules of dotnet/Directory.Build.targets (error PUZZLE001) hold only while MSBuild imports that file
    /// and nothing loosens what it pins. Ported from the engine's ArchitectureGuardTests: the guard fires on loosened
    /// settings and accepts the committed ones, and its Unity-bound condition names exactly CoveredLot.Core. No other
    /// MSBuild settings file may exist, because a nearer Directory.Build.targets ends MSBuild's upward search and the
    /// guard is then never imported. No other analyzer config file may exist either, because a nested .editorconfig
    /// or a .globalconfig can turn the culture and catch rules off, and the root .editorconfig must keep them errors.
    /// </summary>
    public sealed class BuildGuardTests
    {
        private const string DotnetFolder = "dotnet";
        private const string BuildTargets = "dotnet/Directory.Build.targets";
        private const string CoreProject = "CoveredLot.Core/CoveredLot.Core.csproj";
        private const string CoreAssembly = "CoveredLot.Core";
        private const string EditorConfig = ".editorconfig";
        private const string CSharpSection = "*.cs";
        private const string CscRsp = "csc.rsp";
        private const string CoveredLotAssets = "Assets/CoveredLot/";

        /// <summary>Long enough for a cold MSBuild start; a guard run that hangs fails instead of stalling the check.</summary>
        private static readonly TimeSpan GuardRunLimit = TimeSpan.FromMinutes(3);

        private static readonly string[] SharedBuildFiles =
        {
            "dotnet/Directory.Build.props", "dotnet/Directory.Build.targets", "dotnet/Directory.Packages.props",
        };

        /// <summary>The rules the root .editorconfig makes errors; the recommended analysis level leaves them warnings or off.</summary>
        private static readonly string[] RulesKeptAsErrors = { "CA1304", "CA1305", "CA1307", "CA1309", "CA1310", "CA1311", "CA1031" };

        /// <summary>Each loosened setting, as a command-line property, with the words of the PUZZLE001 message that names it.</summary>
        private static readonly LoosenedSetting[] LoosenedSettings =
        {
            new("TreatWarningsAsErrors=false", "TreatWarningsAsErrors must be true"),
            new("Nullable=disable", "Nullable must be enable"),
            new("CheckForOverflowUnderflow=false", "CheckForOverflowUnderflow must be true"),
            new("InvariantGlobalization=true", "InvariantGlobalization must not be true"),
            new("AllowUnsafeBlocks=true", "AllowUnsafeBlocks must not be true"),
            new("NoWarn=CS0219", "NoWarn must stay"),
            new("WarningsNotAsErrors=CS0219", "WarningsNotAsErrors must be empty"),
            new("AnalysisLevel=none", "AnalysisLevel must be 10.0-recommended"),
            new("EnableNETAnalyzers=false", "EnableNETAnalyzers must be true"),
            new("RunAnalyzers=false", "RunAnalyzers must not be false"),
            new("RunAnalyzersDuringBuild=false", "RunAnalyzersDuringBuild must not be false"),
            new("EnforceCodeStyleInBuild=false", "EnforceCodeStyleInBuild must be true"),
            new("DiscoverEditorConfigFiles=false", "DiscoverEditorConfigFiles must not be false"),
            new("CodeAnalysisRuleSet=loose.ruleset", "CodeAnalysisRuleSet must be empty"),
            new("WarningLevel=0", "WarningLevel must not be below 4"),
            new("Features=run-nullable-analysis=never", "Features must not change nullable analysis"),
            new("TargetFramework=netstandard2.0", "TargetFramework must be netstandard2.1"),
            new("LangVersion=10.0", "LangVersion must be 9.0"),
        };

        private static readonly Regex UnityBoundCondition = new(
            "<PuzzleIsUnityBound Condition=\"([^\"]*)\">true</PuzzleIsUnityBound>", RegexOptions.CultureInvariant);

        private static readonly Regex ProjectNameTest = new("'\\$\\(MSBuildProjectName\\)' == '([^']+)'", RegexOptions.CultureInvariant);

        private static readonly Regex SectionLine = new(@"^\s*\[(?<section>.*)\]\s*$", RegexOptions.CultureInvariant);

        private static readonly Regex SeverityLine = new(
            @"^\s*dotnet_diagnostic\.(?<rule>[A-Za-z]+[0-9]+)\.severity\s*=\s*(?<severity>[A-Za-z]+)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        [Fact]
        public void The_build_guard_rejects_loosened_settings()
        {
            ProcessResult result = RunGuard(LoosenedSettings.Select(setting => "-p:" + setting.Property));

            IReadOnlyList<string> unnamed = LoosenedSettings
                .Select(setting => setting.Message)
                .Where(message => !result.Output.Contains(message, StringComparison.Ordinal))
                .ToList();

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("PUZZLE001", result.Output, StringComparison.Ordinal);
            Assert.True(unnamed.Count == 0, "PUZZLE001 did not name: " + string.Join("; ", unnamed) + "\n" + result.Output);
        }

        [Fact]
        public void The_build_guard_accepts_the_core_project_as_committed()
        {
            ProcessResult result = RunGuard(Array.Empty<string>());

            Assert.True(result.ExitCode == 0, result.Output);
        }

        /// <summary>
        /// The build checks netstandard2.1, C# 9 and the absence of packages only for the projects this condition names,
        /// so one left out would lose those checks without any error.
        /// </summary>
        [Fact]
        public void The_build_marks_exactly_CoveredLot_Core_as_unity_bound()
        {
            string targets = File.ReadAllText(RepositoryLayout.FullPath(BuildTargets));

            Match condition = Assert.Single(UnityBoundCondition.Matches(targets));
            IEnumerable<string> named = ProjectNameTest.Matches(condition.Groups[1].Value).Select(match => match.Groups[1].Value);

            Assert.Equal(new[] { CoreAssembly }, named);
        }

        [Fact]
        public void No_extra_msbuild_file_can_override_the_shared_build_rules()
        {
            IReadOnlyList<string> files = RepositoryLayout.Files();

            IReadOnlyList<string> offenders = files
                .Where(path => IsMsBuildSettingsFile(Path.GetFileName(path)))
                .Where(path => !SharedBuildFiles.Contains(path, StringComparer.Ordinal) && !IsCscRspBesideACoveredLotAsmdef(path, files))
                .ToList();

            Assert.True(offenders.Count == 0,
                "Only dotnet/Directory.Build.props, dotnet/Directory.Build.targets and dotnet/Directory.Packages.props may hold build"
                + " settings, and a csc.rsp may only sit beside a CoveredLot asmdef:\n" + string.Join("\n", offenders));
        }

        [Fact]
        public void The_root_editorconfig_is_the_only_analyzer_config_file()
        {
            IReadOnlyList<string> offenders = RepositoryLayout.Files()
                .Where(path => IsAnalyzerConfigFile(Path.GetFileName(path)) && path != EditorConfig)
                .ToList();

            Assert.True(offenders.Count == 0,
                "A nested .editorconfig or a .globalconfig can change the analyzer rules; only the root .editorconfig may:\n"
                + string.Join("\n", offenders));
        }

        [Fact]
        public void The_root_editorconfig_makes_the_culture_and_catch_rules_errors_and_nothing_overrides_them()
        {
            IReadOnlyList<SeveritySetting> settings = SeveritySettings(RepositoryLayout.FullPath(EditorConfig));

            IReadOnlyList<string> loose = RulesKeptAsErrors.Where(rule => !IsErrorForEveryCSharpFile(settings, rule)).ToList();

            Assert.True(loose.Count == 0,
                EditorConfig + " must set each of these to error once, in [" + CSharpSection + "], and nowhere else: " + string.Join(", ", loose));
        }

        private static bool IsMsBuildSettingsFile(string fileName) =>
            fileName.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Directory.Build.", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Directory.Packages.", StringComparison.OrdinalIgnoreCase);

        /// <summary>A csc.rsp in a folder under Assets/CoveredLot that holds an asmdef: Unity's options for that assembly.</summary>
        private static bool IsCscRspBesideACoveredLotAsmdef(string path, IReadOnlyList<string> files)
        {
            if (!path.StartsWith(CoveredLotAssets, StringComparison.Ordinal) || Path.GetFileName(path) != CscRsp)
            {
                return false;
            }

            string folder = path[..^CscRsp.Length];
            return files.Any(file => file.StartsWith(folder, StringComparison.Ordinal)
                && file.EndsWith(".asmdef", StringComparison.Ordinal)
                && !file[folder.Length..].Contains('/', StringComparison.Ordinal));
        }

        private static bool IsAnalyzerConfigFile(string fileName) =>
            string.Equals(fileName, EditorConfig, StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".globalconfig", StringComparison.OrdinalIgnoreCase);

        private static bool IsErrorForEveryCSharpFile(IReadOnlyList<SeveritySetting> settings, string rule)
        {
            IReadOnlyList<SeveritySetting> ofRule = settings.Where(setting => string.Equals(setting.Rule, rule, StringComparison.OrdinalIgnoreCase)).ToList();
            return ofRule.Count == 1
                && ofRule[0].Section == CSharpSection
                && string.Equals(ofRule[0].Severity, "error", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Every dotnet_diagnostic severity the file sets, with the section it is set in.</summary>
        private static IReadOnlyList<SeveritySetting> SeveritySettings(string editorConfig)
        {
            var settings = new List<SeveritySetting>();
            string section = string.Empty;
            foreach (string line in File.ReadAllLines(editorConfig))
            {
                if (SectionLine.Match(line) is { Success: true } header)
                {
                    section = header.Groups["section"].Value;
                }
                else if (SeverityLine.Match(line) is { Success: true } setting)
                {
                    settings.Add(new SeveritySetting(section, setting.Groups["rule"].Value, setting.Groups["severity"].Value));
                }
            }

            return settings;
        }

        private static ProcessResult RunGuard(IEnumerable<string> properties) => RunDotnet(
            new[] { "msbuild", CoreProject, "-nologo", "-nodeReuse:false", "-t:PuzzleGuardProjectRules" }.Concat(properties).ToList());

        /// <summary>Runs dotnet in dotnet/, so that dotnet/global.json picks the SDK, as tools/check-dotnet.sh does.</summary>
        private static ProcessResult RunDotnet(IReadOnlyList<string> arguments)
        {
            string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } path ? path : "dotnet";
            var start = new ProcessStartInfo(host)
            {
                WorkingDirectory = RepositoryLayout.FullPath(DotnetFolder),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + host);
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GuardRunLimit))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("dotnet " + string.Join(" ", arguments) + " did not finish within "
                    + GuardRunLimit.ToString("c", CultureInfo.InvariantCulture) + ".");
            }

            return new ProcessResult(process.ExitCode, output.Result + errors.Result);
        }

        private sealed record LoosenedSetting(string Property, string Message);

        private sealed record SeveritySetting(string Section, string Rule, string Severity);

        private sealed record ProcessResult(int ExitCode, string Output);
    }
}
