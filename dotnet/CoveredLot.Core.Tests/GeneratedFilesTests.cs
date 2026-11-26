using System;
using System.Collections.Generic;
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
    /// every file matches its line in engine-sync.sha256, nothing else is there but Unity's .meta files and the two
    /// records, and the provenance record holds the full commit and nothing that depends on the machine or the clock.
    /// </summary>
    public sealed class GeneratedFilesTests
    {
        private const string Generated = "Assets/Generated";
        private const string RecordName = "engine-sync.txt";
        private const string ChecksumsName = "engine-sync.sha256";
        private const string CommitKey = "engine-commit ";

        /// <summary>A sha256sum line: 64 lower-case hex digits, two spaces, a path relative to Assets/Generated.</summary>
        private static readonly Regex ChecksumLine = new(@"^(?<hash>[0-9a-f]{64})  (?<path>\S.*)$", RegexOptions.CultureInvariant);

        private static readonly Regex FullCommit = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

        private static readonly Regex DriveLetterPath = new(@"(?<![A-Za-z0-9])[A-Za-z]:[\\/]", RegexOptions.CultureInvariant);

        private static readonly Regex UserFolder = new(@"[\\/](users|home)[\\/]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>A date (2026-11-26), a clock time (05:57) or ten digits of Unix seconds.</summary>
        private static readonly Regex Time = new(@"\d{4}-\d{2}-\d{2}|(?<!\d)\d{1,2}:\d{2}(?!\d)|(?<![0-9A-Za-z])\d{10}(?![0-9A-Za-z])", RegexOptions.CultureInvariant);

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

        [Fact]
        public void Nothing_under_Assets_Generated_is_unlisted_except_meta_files_and_the_two_records()
        {
            ISet<string> listed = ChecksumEntries().Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);
            string root = RepositoryLayout.FullPath(Generated);

            IReadOnlyList<string> unlisted = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => RepositoryLayout.Relative(root, path))
                .Where(path => !listed.Contains(path) && !MayBeUnlisted(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            Assert.True(unlisted.Count == 0, "not in " + ChecksumsName + ": " + string.Join(", ", unlisted));
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

            Assert.DoesNotMatch(DriveLetterPath, record);
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

        private sealed record ChecksumEntry(string Hash, string Path);
    }
}
