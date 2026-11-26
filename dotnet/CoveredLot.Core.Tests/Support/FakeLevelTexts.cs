using System;
using System.Collections.Immutable;
using System.Linq;
using CoveredLot.Core.Catalogue;
using PuzzleCore;
using PuzzleCore.Records;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Level texts held in memory, a test-only host: the texts the game ships, or none, with texts put in, replaced or taken
    /// out one at a time. Each kind of text has methods of its own, and a level's text is named by its LevelId, so a text
    /// can never stand where its name should.
    /// </summary>
    internal sealed class FakeLevelTexts : ILevelTexts
    {
        private readonly Option<string> _presentation;
        private readonly Option<string> _example;
        private readonly ImmutableDictionary<LevelId, string> _levels;

        private FakeLevelTexts(Option<string> presentation, Option<string> example, ImmutableDictionary<LevelId, string> levels)
        {
            _presentation = presentation;
            _example = example;
            _levels = levels;
        }

        /// <summary>No text at all.</summary>
        public static FakeLevelTexts Nothing { get; } =
            new FakeLevelTexts(Option.None<string>(), Option.None<string>(), ImmutableDictionary<LevelId, string>.Empty);

        /// <summary>The texts the game ships: presentation.json, the Example and the text of every level presentation.json lists.</summary>
        public static FakeLevelTexts Shipped() => new FakeLevelTexts(
            Option.Some(ShippedLevels.PresentationText()),
            Option.Some(ShippedLevels.Text(ShippedLevels.ExampleName)),
            ShippedLevels.Listed().ToImmutableDictionary(level => level.Level, level => ShippedLevels.Text(level.Level.Value)));

        /// <summary>The level id a test names, as the engine parses it.</summary>
        public static LevelId Id(string levelId) =>
            LevelId.Parse(levelId).TryGet(out LevelId id) ? id : throw new XunitException(levelId + " is no level id.");

        public FakeLevelTexts WithPresentation(string json) => new FakeLevelTexts(Option.Some(json), _example, _levels);

        public FakeLevelTexts WithoutPresentation() => new FakeLevelTexts(Option.None<string>(), _example, _levels);

        public FakeLevelTexts WithExample(string text) => new FakeLevelTexts(_presentation, Option.Some(text), _levels);

        public FakeLevelTexts WithoutExample() => new FakeLevelTexts(_presentation, Option.None<string>(), _levels);

        public FakeLevelTexts WithLevel(LevelId id, string text) => new FakeLevelTexts(_presentation, _example, _levels.SetItem(id, text));

        public FakeLevelTexts WithoutLevel(LevelId id) => new FakeLevelTexts(_presentation, _example, _levels.Remove(id));

        /// <summary>The text this fake holds for the level; a test that changes a shipped text starts from it.</summary>
        public string LevelText(LevelId id) =>
            _levels.TryGetValue(id, out string? text) ? text : throw new XunitException("The fake holds no text for " + id.Value + ".");

        public Option<string> Read(LevelTextName name) => name switch
        {
            LevelTextName.Presentation => _presentation,
            LevelTextName.Example => _example,
            LevelTextName.Level level => _levels.TryGetValue(level.Id, out string? text) ? Option.Some(text) : Option.None<string>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), "The fake has no texts of the kind " + name.GetType().Name + "."),
        };
    }
}
