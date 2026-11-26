using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// Assets/Generated holds exactly what tools/sync-engine.sh staged from one engine commit (design section 4):
    /// every file matches its line in engine-sync.sha256; nothing Unity imports is there besides those files, Unity's
    /// .meta files and the two records; the level texts are exactly the listed levels and the Example; and the
    /// provenance record is the seven lines the script writes, with the full commit and the level count, and holds
    /// nothing that depends on the machine or the clock.
    /// </summary>
    public sealed class GeneratedFilesTests
    {
        private const string Generated = "Assets/Generated";
        private const string RecordName = "engine-sync.txt";
        private const string ChecksumsName = "engine-sync.sha256";
        private const string CommitKey = "engine-commit ";
        private const string LevelsKey = "levels ";

        /// <summary>A sha256sum line: 64 lower-case hex digits, two spaces, a path relative to Assets/Generated.</summary>
        private static readonly Regex ChecksumLine = new(@"^(?<hash>[0-9a-f]{64})  (?<path>\S.*)$", RegexOptions.CultureInvariant);

        private static readonly Regex FullCommit = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

        /// <summary>The record's lines in the order tools/sync-engine.sh writes them, each with the shape of its value.</summary>
        private static readonly Regex[] RecordLineShapes =
        {
            new(@"^engine-repository https://github\.com/SaturnWave/puzzle-engine$", RegexOptions.CultureInvariant),
            new("^engine-commit [0-9a-f]{40}$", RegexOptions.CultureInvariant),
            new(@"^dotnet-sdk [0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant),
            new("^configuration Release$", RegexOptions.CultureInvariant),
            new("^gen campaign-jam [0-9]+$", RegexOptions.CultureInvariant),
            // A path inside the engine repository: no drive, no leading '/' or '~', no backslash.
            new(@"^example [A-Za-z0-9_-][A-Za-z0-9._-]*(/[A-Za-z0-9._-]+)*$", RegexOptions.CultureInvariant),
            new("^levels [0-9]+$", RegexOptions.CultureInvariant),
        };

        /// <summary>
        /// A drive-letter path as Windows writes it (a letter, a colon, a slash or backslash) or as Git Bash writes it (a
        /// slash, a letter, a slash), or a network share (two backslashes). Described in words, so this file passes the scan.
        /// </summary>
        private static readonly Regex DriveLetterOrSharePath = new(
            @"(?<![A-Za-z0-9])[A-Za-z]:[\\/]|(?<![A-Za-z0-9._~/-])/[A-Za-z]/|\\\\", RegexOptions.CultureInvariant);

        /// <summary>A users or home folder between slashes or backslashes, in any case, or a home-relative path (a tilde and a slash).</summary>
        private static readonly Regex UserFolder = new(@"[\\/](users|home)[\\/]|~[\\/]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>A date (2026-11-26, 26.11.2026), a clock time (05:57), or Unix seconds (ten digits) or milliseconds (thirteen).</summary>
        private static readonly Regex Time = new(
            @"\d{4}-\d{2}-\d{2}|(?<![\d.])\d{1,2}[./]\d{1,2}[./]\d{4}(?!\d)|(?<!\d)\d{1,2}:\d{2}(?!\d)|(?<![0-9A-Za-z])(\d{10}|\d{13})(?![0-9A-Za-z])",
            RegexOptions.CultureInvariant);

        [Fact]
        public void Every_checksum_line_matches_the_sha256_of_its_file()
        {
            IReadOnlyList<ChecksumEntry> entries = ChecksumEntries();
            var mismatches = new List<string>();
            foreach (ChecksumEntry entry in entries)
            {
                string path = GeneratedPath(entry.Path);
                if (!File.Exists(path))
                {
                    mismatches.Add(entry.Path + " is missing");
                }
                else if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != entry.Hash)
                {
                    mismatches.Add(entry.Path + " does not match its checksum");
                }
            }

            Assert.NotEmpty(entries);
            Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
        }

        /// <summary>Files Unity does not import (a Finder .DS_Store, an editor's x~ backup) are not checked.</summary>
        [Fact]
        public void Nothing_Unity_imports_from_Assets_Generated_is_unlisted_except_meta_files_and_the_two_records()
        {
            ISet<string> listed = ChecksumEntries().Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);

            IReadOnlyList<string> unlisted = Directory.EnumerateFiles(RepositoryLayout.FullPath(Generated), "*", SearchOption.AllDirectories)
                .Select(RelativeToGenerated)
                .Where(path => !UnityAssets.IsHidden(path) && !listed.Contains(path) && !MayBeUnlisted(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.True(unlisted.Count == 0, "not in " + ChecksumsName + ": " + string.Join(", ", unlisted));
        }

        [Fact]
        public void The_level_texts_are_the_listed_levels_and_the_example_and_the_record_counts_the_listed_ones()
        {
            IReadOnlyList<string> listedIds = ShippedLevels.Listed().Select(level => level.Level.Value).ToList();

            IEnumerable<string> texts = Directory.EnumerateFiles(RepositoryLayout.FullPath(ShippedLevels.Folder))
                .Where(path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.Ordinal) && !UnityAssets.IsHidden(Path.GetFileName(path)))
                .Select(path => Path.GetFileNameWithoutExtension(path));

            Assert.Equal(Sorted(listedIds.Append(ShippedLevels.ExampleName)), Sorted(texts));
            Assert.Equal(LevelsKey + listedIds.Count.ToString(CultureInfo.InvariantCulture),
                Assert.Single(RecordLines(), line => line.StartsWith(LevelsKey, StringComparison.Ordinal)));
        }

        [Fact]
        public void The_record_holds_exactly_the_lines_sync_engine_writes()
        {
            IReadOnlyList<string> lines = RecordLines();

            Assert.True(lines.Count == RecordLineShapes.Length,
                RecordName + " has " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, not the "
                + RecordLineShapes.Length.ToString(CultureInfo.InvariantCulture) + " that tools/sync-engine.sh writes.");
            Assert.All(lines.Zip(RecordLineShapes), pair => Assert.Matches(pair.Second, pair.First));
        }

        [Fact]
        public void The_record_names_a_full_engine_commit()
        {
            string commitLine = Assert.Single(RecordLines(), line => line.StartsWith(CommitKey, StringComparison.Ordinal));

            Assert.Matches(FullCommit, commitLine.Substring(CommitKey.Length));
        }

        [Fact]
        public void The_record_holds_no_drive_letter_user_folder_or_time()
        {
            string record = File.ReadAllText(GeneratedPath(RecordName));

            Assert.DoesNotMatch(DriveLetterOrSharePath, record);
            Assert.DoesNotMatch(UserFolder, record);
            Assert.DoesNotMatch(Time, record);
        }

        private static bool MayBeUnlisted(string path) =>
            path.EndsWith(".meta", StringComparison.Ordinal) || path == RecordName || path == ChecksumsName;

        private static IReadOnlyList<ChecksumEntry> ChecksumEntries()
        {
            var entries = new List<ChecksumEntry>();
            foreach (string line in File.ReadAllLines(GeneratedPath(ChecksumsName)))
            {
                Match match = ChecksumLine.Match(line);
                Assert.True(match.Success, ChecksumsName + " holds a line that is not in sha256sum format: " + line);
                entries.Add(new ChecksumEntry(match.Groups["hash"].Value, match.Groups["path"].Value));
            }

            return entries;
        }

        private static IReadOnlyList<string> RecordLines() => File.ReadAllLines(GeneratedPath(RecordName));

        private static string GeneratedPath(string relativePath) => RepositoryLayout.FullPath(Generated + "/" + relativePath);

        /// <summary>The path relative to Assets/Generated, as engine-sync.sha256 writes it.</summary>
        private static string RelativeToGenerated(string fullPath) => RepositoryLayout.Relative(fullPath)[(Generated.Length + 1)..];

        private static List<string> Sorted(IEnumerable<string> names) => names.OrderBy(name => name, StringComparer.Ordinal).ToList();

        private sealed record ChecksumEntry(string Hash, string Path);
    }
}
