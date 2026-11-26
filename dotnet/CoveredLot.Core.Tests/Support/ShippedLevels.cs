using System.Collections.Generic;
using System.IO;
using System.Text;
using PuzzleCore.Packs;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// The level texts the game ships in Assets/Generated/Resources/Levels: one per level that presentation.json
    /// lists, named by its level id, and six-by-six.txt, the Example.
    /// </summary>
    internal static class ShippedLevels
    {
        public const string Folder = "Assets/Generated/Resources/Levels";
        public const string ExampleName = "six-by-six";
        public const string PresentationFileName = "presentation.json";

        /// <summary>Strict UTF-8 that keeps a byte-order mark as U+FEFF, so the engine's readers see every byte of a file.</summary>
        public static Encoding StrictUtf8 { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>The levels presentation.json lists, in its order.</summary>
        public static IReadOnlyList<PresentationLevel> Listed() =>
            PresentationJson.Read(PresentationText()) switch
            {
                PresentationReadResult.Read read => read.File.Levels,
                var unreadable => throw new XunitException("presentation.json does not read: " + unreadable),
            };

        /// <summary>The full path of the shipped text with the name: a level id, or the Example's name.</summary>
        public static string TextPath(string name) => RepositoryLayout.FullPath(Folder + "/" + name + ".txt");

        /// <summary>The shipped text with the name (a level id, or the Example's name), decoded as the engine's readers see it.</summary>
        public static string Text(string name) => StrictUtf8.GetString(File.ReadAllBytes(TextPath(name)));

        /// <summary>presentation.json, decoded as the engine's reader sees it.</summary>
        public static string PresentationText() => StrictUtf8.GetString(File.ReadAllBytes(PresentationPath()));

        public static string PresentationPath() => RepositoryLayout.FullPath(Folder + "/" + PresentationFileName);
    }
}
