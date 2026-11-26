using System;
using System.Collections.Immutable;
using CoveredLot.Core.Catalogue;
using PuzzleCore;
using PuzzleCore.Packs;
using PuzzleCore.Records;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Level texts held in memory, a test-only host: the texts the game ships, or none, with texts put in, replaced or taken
    /// out one at a time. Each kind of text is a member of its own that only its own methods change, through a copy, and a
    /// level's text is named by its LevelId, so no text can take another's place or stand where its name should.
    /// </summary>
    internal sealed record FakeLevelTexts : ILevelTexts
    {
        private FakeLevelTexts()
        {
        }

        /// <summary>No text at all.</summary>
        public static FakeLevelTexts Nothing { get; } = new FakeLevelTexts();

        /// <summary>presentation.json's text; None, a missing file, unless a method puts one in.</summary>
        private Option<string> Presentation { get; init; }

        /// <summary>The Example's text; None, a missing file, unless a method puts one in.</summary>
        private Option<string> Example { get; init; }

        private ImmutableDictionary<LevelId, string> Levels { get; init; } = ImmutableDictionary<LevelId, string>.Empty;

        /// <summary>The texts the game ships: presentation.json, the Example and the text of every level presentation.json lists.</summary>
        public static FakeLevelTexts Shipped()
        {
            ImmutableDictionary<LevelId, string>.Builder levels = ImmutableDictionary.CreateBuilder<LevelId, string>();
            foreach (PresentationLevel level in ShippedLevels.Listed())
            {
                levels.Add(level.Level, ShippedLevels.Text(level.Level.Value));
            }

            return new FakeLevelTexts
            {
                Presentation = Option.Some(ShippedLevels.PresentationText()),
                Example = Option.Some(ShippedLevels.Text(ShippedLevels.ExampleName)),
                Levels = levels.ToImmutable(),
            };
        }

        /// <summary>The level id a test names, as the engine parses it.</summary>
        public static LevelId Id(string levelId) =>
            LevelId.Parse(levelId).TryGet(out LevelId id) ? id : throw new XunitException(levelId + " is no level id.");

        public FakeLevelTexts WithPresentation(string json) => this with { Presentation = Option.Some(json) };

        public FakeLevelTexts WithoutPresentation() => this with { Presentation = Option.None<string>() };

        public FakeLevelTexts WithExample(string text) => this with { Example = Option.Some(text) };

        public FakeLevelTexts WithoutExample() => this with { Example = Option.None<string>() };

        public FakeLevelTexts WithLevel(LevelId id, string text) => this with { Levels = Levels.SetItem(id, text) };

        public FakeLevelTexts WithoutLevel(LevelId id) => this with { Levels = Levels.Remove(id) };

        /// <summary>The text this fake holds for the level; a test that changes a shipped text starts from it.</summary>
        public string LevelText(LevelId id) =>
            Levels.TryGetValue(id, out string? text) ? text : throw new XunitException("The fake holds no text for " + id.Value + ".");

        public Option<string> Read(LevelTextName name) => name switch
        {
            LevelTextName.Presentation => Presentation,
            LevelTextName.Example => Example,
            LevelTextName.Level level => Levels.TryGetValue(level.Id, out string? text) ? Option.Some(text) : Option.None<string>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), "The fake has no texts of the kind " + name.GetType().Name + "."),
        };
    }
}
