using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using Xunit;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// Every level text the game ships (each level that presentation.json lists, and six-by-six.txt, the Example) is
    /// the engine's canonical text of a valid level (design section 5): it reads and loads with the engine DLLs the
    /// game ships, writing the level back gives the file byte for byte, a listed level has its presentation
    /// fingerprint, its colours are the six that pc-lot r=1 names, and the file is printable ASCII with LF line ends,
    /// no byte-order mark and no trailing newline, which LotText.Read requires.
    /// </summary>
    public sealed class ShippedLevelTextTests
    {
        private const string LevelsFolder = "Assets/Generated/Resources/Levels";
        private const string ExampleName = "six-by-six";
        private const int ColourCount = 6;

        /// <summary>Strict UTF-8 that keeps a byte-order mark as U+FEFF, so LotText.Read sees every byte of the file.</summary>
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>The level id of every listed level, then the Example's name.</summary>
        public static TheoryData<string> ShippedTexts => new(ListedLevels().Select(level => level.Level.Value).Append(ExampleName));

        public static TheoryData<string> ListedLevelIds => new(ListedLevels().Select(level => level.Level.Value));

        [Theory]
        [MemberData(nameof(ShippedTexts))]
        public void A_shipped_text_reads_and_loads(string name)
        {
            Assert.NotNull(LoadedLevel(name));
        }

        [Theory]
        [MemberData(nameof(ShippedTexts))]
        public void Writing_a_shipped_level_back_gives_its_file_byte_for_byte(string name)
        {
            LotLevel level = LoadedLevel(name);

            Assert.Equal(File.ReadAllBytes(TextPath(name)), Encoding.UTF8.GetBytes(LotText.Write(level)));
        }

        [Theory]
        [MemberData(nameof(ListedLevelIds))]
        public void A_listed_level_has_the_fingerprint_its_presentation_entry_gives(string levelId)
        {
            PresentationLevel entry = Assert.Single(ListedLevels(), level => level.Level.Value == levelId);

            Assert.Equal(entry.Fingerprint.Hex, LotText.Fingerprint(LoadedLevel(levelId)));
        }

        [Theory]
        [MemberData(nameof(ShippedTexts))]
        public void Every_colour_id_of_a_shipped_level_is_one_to_six(string name)
        {
            LotLevel level = LoadedLevel(name);

            IEnumerable<ColourId> colours = level.Vehicles.Select(vehicle => vehicle.Colour).Concat(level.Clues.SelectMany(ColoursOf));

            Assert.All(colours, colour => Assert.InRange(colour.Value, 1, ColourCount));
        }

        [Theory]
        [MemberData(nameof(ShippedTexts))]
        public void A_shipped_text_is_printable_ascii_with_lf_line_ends_and_no_byte_order_mark_or_trailing_newline(string name)
        {
            IReadOnlyList<string> problems = FormatProblems(File.ReadAllBytes(TextPath(name)));

            Assert.True(problems.Count == 0, name + ".txt " + string.Join("; ", problems));
        }

        private static IReadOnlyList<string> FormatProblems(byte[] bytes)
        {
            var problems = new List<string>();
            if (bytes.Length == 0)
            {
                problems.Add("is empty");
                return problems;
            }

            if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
            {
                problems.Add("starts with a byte-order mark");
            }

            if (bytes.Contains((byte)'\r'))
            {
                problems.Add("holds a carriage return");
            }

            if (bytes.Any(value => value != '\n' && !IsPrintableAscii(value)))
            {
                problems.Add("holds a byte that is neither printable ASCII nor LF");
            }

            if (bytes[^1] == '\n')
            {
                problems.Add("ends with a newline");
            }

            return problems;
        }

        private static bool IsPrintableAscii(byte value) => value is >= 0x20 and <= 0x7E;

        private static IEnumerable<ColourId> ColoursOf(LotClue clue) => clue switch
        {
            LotClue.Not not => new[] { not.Colour },
            LotClue.Ahead => Array.Empty<ColourId>(),
            LotClue.Touch touch => new[] { touch.Colour },
            _ => throw new UnknownEngineCaseException(clue),
        };

        private static LotLevel LoadedLevel(string name)
        {
            LotDefinition definition = LotText.Read(StrictUtf8.GetString(File.ReadAllBytes(TextPath(name)))) switch
            {
                LotTextResult.Read read => read.Definition,
                var malformed => throw new XunitException(name + " is not a pc-lot text: " + malformed),
            };
            return LotLoader.Load(definition) switch
            {
                LoadResult.Loaded loaded => loaded.Level,
                var rejected => throw new XunitException(name + " is refused by LotLoader: " + rejected),
            };
        }

        private static IReadOnlyList<PresentationLevel> ListedLevels() =>
            PresentationJson.Read(StrictUtf8.GetString(File.ReadAllBytes(RepositoryLayout.FullPath(LevelsFolder + "/presentation.json")))) switch
            {
                PresentationReadResult.Read read => read.File.Levels,
                var unreadable => throw new XunitException("presentation.json does not read: " + unreadable),
            };

        private static string TextPath(string name) => RepositoryLayout.FullPath(LevelsFolder + "/" + name + ".txt");
    }
}
