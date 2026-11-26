using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CoveredLot.Core.Catalogue;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The closed sets the catalogue switches over or makes, pinned case by case (design section 8.4), so that a case the
    /// engine adds at a later sync fails here, by name, instead of reaching a discard arm on a phone. The engine's enums
    /// are pinned with their values, which grow only at the end; the engine's result hierarchies by every type of its
    /// assembly that derives from them; Core's own sets also by their private constructors, which keep any other assembly
    /// from adding a case, and by the cases' constructors, none of them public, since only the catalogue makes them.
    /// </summary>
    public sealed class ClosedSetPinTests
    {
        private const BindingFlags AnyInstanceConstructor = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Fact]
        public void LotTextProblem_has_exactly_these_ten_values_in_this_order()
        {
            AssertValues<LotTextProblem>(
                "WrongHeader", "UnsupportedRulesVersion", "MissingOrMisplacedLine", "BadNumber", "BadSize", "BadVehicle", "BadColour",
                "BadCover", "BadClue", "TrailingText");
        }

        [Fact]
        public void LoadProblem_has_exactly_these_thirteen_values_in_this_order()
        {
            AssertValues<LoadProblem>(
                "BlockingCycle", "PillarBlocksPath", "AheadWithoutBlocker", "ColourTotalNotMultipleOfThree", "BayCountOutOfRange",
                "PieceOutOfLot", "PiecesOverlap", "SizeOutOfRange", "VehicleCountOutOfRange", "PiecesNotInAnchorOrder",
                "PillarOutOfLot", "UnknownPiece", "ClueOnOpenVehicle");
        }

        [Fact]
        public void LotTextResult_has_exactly_the_cases_Read_and_Malformed()
        {
            AssertEngineCases(typeof(LotTextResult), "Malformed", "Read");
        }

        [Fact]
        public void LoadResult_has_exactly_the_cases_Loaded_and_Rejected()
        {
            AssertEngineCases(typeof(LoadResult), "Loaded", "Rejected");
        }

        [Fact]
        public void PresentationReadResult_has_exactly_the_cases_Read_UnsupportedVersion_and_Invalid()
        {
            AssertEngineCases(typeof(PresentationReadResult), "Invalid", "Read", "UnsupportedVersion");
        }

        [Fact]
        public void PeakParResult_has_exactly_the_cases_Found_Unsolvable_and_BudgetExceeded()
        {
            AssertEngineCases(typeof(PeakParResult), "BudgetExceeded", "Found", "Unsolvable");
        }

        [Fact]
        public void EntryTitle_is_closed_with_exactly_the_cases_Example_and_Numbered()
        {
            AssertCoreCases(typeof(EntryTitle), "Example", "Numbered");
        }

        [Fact]
        public void CatalogueProblem_is_closed_with_exactly_its_seven_cases()
        {
            AssertCoreCases(typeof(CatalogueProblem),
                "ExamplePeakParNotFound", "LevelRejected", "LevelTextMalformed", "LevelTextMissing", "NotAJamPack", "PeakParMissing",
                "PresentationUnreadable");
        }

        [Fact]
        public void CatalogueResult_is_closed_with_exactly_the_cases_Ready_and_Broken()
        {
            AssertCoreCases(typeof(CatalogueResult), "Broken", "Ready");
        }

        [Fact]
        public void LevelSource_is_closed_with_exactly_the_cases_Example_and_Listed()
        {
            AssertCoreCases(typeof(LevelSource), "Example", "Listed");
        }

        [Fact]
        public void LevelTextName_is_closed_with_exactly_the_cases_Level_Presentation_and_Example()
        {
            AssertCoreCases(typeof(LevelTextName), "Example", "Level", "Presentation");
        }

        /// <summary>The enum's names in value order, and its values 0 to n-1, so each value's place is pinned too.</summary>
        private static void AssertValues<TEnum>(params string[] names)
            where TEnum : struct, Enum
        {
            TEnum[] values = Enum.GetValues<TEnum>();

            Assert.Equal(names, values.Select(value => value.ToString()));
            Assert.Equal(Enumerable.Range(0, names.Length), values.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)));
        }

        /// <summary>Every type of the set's assembly that derives from it, by name in ordinal order: each sealed and nested in the set.</summary>
        private static void AssertEngineCases(Type closedSet, params string[] cases)
        {
            IReadOnlyList<Type> derived = closedSet.Assembly.GetTypes().Where(type => type.IsSubclassOf(closedSet)).ToList();

            Assert.True(closedSet.IsAbstract, closedSet.FullName + " is not abstract.");
            Assert.Equal(cases, derived.Select(type => type.DeclaringType == closedSet ? type.Name : type.FullName).Order(StringComparer.Ordinal));
            Assert.All(derived, type => Assert.True(type.IsSealed, type.FullName + " is not sealed."));
        }

        /// <summary>The engine's checks, and: every constructor of the set is private, and no case has a public constructor.</summary>
        private static void AssertCoreCases(Type closedSet, params string[] cases)
        {
            AssertEngineCases(closedSet, cases);

            ConstructorInfo[] constructors = closedSet.GetConstructors(AnyInstanceConstructor);
            Assert.NotEmpty(constructors);
            Assert.All(constructors, constructor => Assert.True(constructor.IsPrivate, closedSet.FullName + " has a constructor that is not private."));
            Assert.All(closedSet.GetNestedTypes().Where(type => type.IsSubclassOf(closedSet)),
                type => Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)));
        }
    }
}
