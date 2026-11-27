using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using PuzzleCore.Jam;
using PuzzleCore.Packs;
using PuzzleCore.Play;
using PuzzleCore.Rules;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The closed sets Core switches over or makes (design section 8.4), pinned case by case, so that a case the engine
    /// adds at a later sync fails here, by name, instead of reaching a discard arm on a phone: the engine's results and
    /// events that the catalogue and the session switch over, the values their cases carry (FailKind, InvalidReason), the
    /// sets that the pictures and copy of later pieces will switch over (Facing, AheadRelation, LotClue), and Core's own.
    /// The engine's enums are pinned with their values, which grow only at the end; the engine's result hierarchies by
    /// every type of its assembly that derives from them, a generic one through its generic definition; Core's own sets
    /// also by their private constructors, which keep any other assembly from adding a case, and by the cases'
    /// constructors, none of them public, since only Core makes them.
    /// </summary>
    public sealed class ClosedSetPinTests
    {
        private const BindingFlags AnyInstanceConstructor = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Fact]
        public void FailKind_has_exactly_these_two_values_in_this_order()
        {
            AssertValues<FailKind>("NoLegalMove", "SlotsFull");
        }

        [Fact]
        public void InvalidReason_has_exactly_these_eight_values_in_this_order()
        {
            AssertValues<InvalidReason>(
                "IllegalMove", "SpareOutsideFail", "SecondSpare", "RewindOutsideFail", "RewindOutOfRange", "BoosterInCompetitiveMode",
                "InsightLimit", "ActionAfterEnd");
        }

        [Fact]
        public void Facing_has_exactly_these_four_values_in_this_order()
        {
            AssertValues<Facing>("North", "East", "South", "West");
        }

        [Fact]
        public void AheadRelation_has_exactly_these_two_values_in_this_order()
        {
            AssertValues<AheadRelation>("Same", "Differs");
        }

        [Fact]
        public void StepResult_of_ExitRejection_has_exactly_the_cases_Done_Illegal_and_Refused()
        {
            AssertGenericEngineCases(typeof(StepResult<ExitRejection>), "Done", "Illegal", "Refused");
        }

        [Fact]
        public void ExitRejection_has_exactly_the_cases_NotInLot_NotInPlay_and_Blocked()
        {
            AssertEngineCases(typeof(ExitRejection), "Blocked", "NotInLot", "NotInPlay");
        }

        [Fact]
        public void Blocker_has_exactly_the_cases_Piece_and_Pillar()
        {
            AssertEngineCases(typeof(Blocker), "Piece", "Pillar");
        }

        [Fact]
        public void Outcome_has_exactly_the_cases_InPlay_Won_and_Failed()
        {
            AssertEngineCases(typeof(Outcome), "Failed", "InPlay", "Won");
        }

        [Fact]
        public void PuzzleEvent_has_exactly_its_seven_cases()
        {
            AssertEngineCases(typeof(PuzzleEvent), "CoverLifted", "Exited", "Failed", "Rewound", "SetCleared", "SpareAdded", "Won");
        }

        [Fact]
        public void LotClue_has_exactly_the_cases_Not_Ahead_and_Touch()
        {
            AssertEngineCases(typeof(LotClue), "Ahead", "Not", "Touch");
        }

        [Fact]
        public void TapFeedback_is_closed_with_exactly_the_cases_None_Moved_Bumped_and_Ignored()
        {
            AssertCoreCases(typeof(TapFeedback), "Bumped", "Ignored", "Moved", "None");
        }

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

        /// <summary>
        /// The engine's checks for a generic set such as StepResult&lt;ExitRejection&gt;: its cases are nested in the generic
        /// definition and derive from it over their own type parameter, so no type of the assembly derives from the set
        /// itself. The cases are found by the definition their base type is made from, and each, made over the set's type
        /// arguments, derives from the set.
        /// </summary>
        private static void AssertGenericEngineCases(Type closedSet, params string[] cases)
        {
            Type definition = closedSet.GetGenericTypeDefinition();
            IReadOnlyList<Type> derived = definition.Assembly.GetTypes().Where(type => DerivesFromDefinition(type, definition)).ToList();

            Assert.True(definition.IsAbstract, definition.FullName + " is not abstract.");
            Assert.Equal(cases, derived.Select(type => type.DeclaringType == definition ? type.Name : type.FullName).Order(StringComparer.Ordinal));
            Assert.All(derived, type => Assert.True(type.IsSealed, type.FullName + " is not sealed."));
            Assert.All(derived, type => Assert.True(type.MakeGenericType(closedSet.GetGenericArguments()).IsSubclassOf(closedSet),
                type.FullName + " over " + string.Join(", ", closedSet.GetGenericArguments().Select(argument => argument.Name)) + " does not derive from the set."));
        }

        /// <summary>Whether a base type of the type, at any depth, is made from the generic definition.</summary>
        private static bool DerivesFromDefinition(Type type, Type definition)
        {
            for (Type? baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == definition)
                {
                    return true;
                }
            }

            return false;
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
