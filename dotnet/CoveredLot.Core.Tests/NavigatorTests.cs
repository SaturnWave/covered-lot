using System.Collections.Generic;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Navigation;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using Xunit;
using static CoveredLot.Core.Tests.Support.SessionTaps;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The screen flow (design section 7), over the catalogue that the shipped texts load and over a broken one. A catalogue
    /// that loads starts on the level list of all its entries; a broken one goes to the problem screen, with the
    /// catalogue's problems, and no action leaves it. Open plays the entry at its index, and an index outside the list
    /// changes nothing, on the list or in a level. Next from the Example plays Level 1, and after the last entry it is None
    /// and changes nothing, as it is off a level; HasNext says which. Again plays the level once more with a fresh session:
    /// peak 0, no tap yet, and the cover of a vehicle that had left back on. BackToList shows the list again.
    /// </summary>
    public sealed class NavigatorTests
    {
        [Fact]
        public void A_catalogue_that_loads_starts_on_the_level_list_of_all_its_entries()
        {
            CatalogueResult catalogue = LevelCatalogue.Load(new DiskLevelTexts());

            var navigator = new Navigator(catalogue);

            AppScreen.LevelList list = Assert.IsType<AppScreen.LevelList>(navigator.Screen);
            Assert.Equal(ShippedCatalogue.ListedLevelCount + 1, list.Entries.Count);
            Assert.Equal(CatalogueDescriptions.EntriesOf(catalogue), list.Entries);
        }

        [Fact]
        public void A_broken_catalogue_goes_to_the_problem_screen_with_its_problems_and_no_action_leaves_it()
        {
            CatalogueResult catalogue = LevelCatalogue.Load(FakeLevelTexts.Shipped().WithoutExample());
            CatalogueResult.Broken broken = Assert.IsType<CatalogueResult.Broken>(catalogue);
            var navigator = new Navigator(catalogue);
            AppScreen.Problems problems = Assert.IsType<AppScreen.Problems>(navigator.Screen);

            navigator.Open(0);
            Option<AppScreen.Playing> next = navigator.Next();
            navigator.Again();
            navigator.BackToList();

            Assert.Equal(new[] { "LevelTextMissing(Example)" }, CatalogueDescriptions.ProblemsOf(catalogue));
            Assert.Equal(broken.Problems, problems.List);
            Assert.False(next.HasValue, "Next on the problem screen played a level.");
            Assert.False(navigator.HasNext);
            Assert.Same(problems, navigator.Screen);
        }

        [Fact]
        public void Open_plays_the_entry_at_its_index_with_a_new_session()
        {
            Navigator navigator = ShippedNavigator();
            ReadOnlyArray<CatalogueEntry> entries = Assert.IsType<AppScreen.LevelList>(navigator.Screen).Entries;

            navigator.Open(25);

            AppScreen.Playing level = Assert.IsType<AppScreen.Playing>(navigator.Screen);
            Assert.Equal(25, level.Index);
            Assert.Same(entries[25], level.Session.Entry);
            Assert.Equal("Level 25, slot 24, par 4", CatalogueDescriptions.Entry(level.Session.Entry));
            Assert.IsType<TapFeedback.None>(level.Session.LastTap);
        }

        [Fact]
        public void Open_out_of_range_changes_nothing_on_the_list_or_in_a_level()
        {
            Navigator navigator = ShippedNavigator();
            AppScreen list = navigator.Screen;

            navigator.Open(-1);
            navigator.Open(ShippedCatalogue.ListedLevelCount + 1);
            AppScreen onList = navigator.Screen;
            navigator.Open(0);
            AppScreen example = navigator.Screen;
            navigator.Open(-1);
            navigator.Open(ShippedCatalogue.ListedLevelCount + 1);

            Assert.IsType<AppScreen.LevelList>(list);
            Assert.Same(list, onList);
            Assert.IsType<AppScreen.Playing>(example);
            Assert.Same(example, navigator.Screen);
        }

        [Fact]
        public void Next_from_the_Example_plays_Level_1()
        {
            Navigator navigator = ShippedNavigator();
            navigator.Open(0);
            bool hasNext = navigator.HasNext;

            Option<AppScreen.Playing> next = navigator.Next();

            Assert.True(hasNext);
            Assert.True(next.TryGet(out AppScreen.Playing? level1), "Next from the Example gave None.");
            Assert.Same(level1, navigator.Screen);
            Assert.Equal(1, level1.Index);
            Assert.Equal("Level 1, slot 0, par 3", CatalogueDescriptions.Entry(level1.Session.Entry));
        }

        [Fact]
        public void Next_after_the_last_entry_is_None_and_changes_nothing()
        {
            Navigator navigator = ShippedNavigator();
            navigator.Open(ShippedCatalogue.ListedLevelCount);
            AppScreen.Playing last = Assert.IsType<AppScreen.Playing>(navigator.Screen);
            bool hasNext = navigator.HasNext;

            Option<AppScreen.Playing> next = navigator.Next();

            Assert.Equal("Level 98, slot 99, par 4", CatalogueDescriptions.Entry(last.Session.Entry));
            Assert.False(hasNext);
            Assert.False(next.HasValue, "Next after the last entry played a level.");
            Assert.Same(last, navigator.Screen);
        }

        [Fact]
        public void On_the_list_Next_is_None_and_Again_changes_nothing()
        {
            Navigator navigator = ShippedNavigator();
            AppScreen list = navigator.Screen;
            bool hasNext = navigator.HasNext;

            Option<AppScreen.Playing> next = navigator.Next();
            navigator.Again();

            Assert.False(hasNext);
            Assert.False(next.HasValue, "Next on the level list played a level.");
            Assert.Same(list, navigator.Screen);
        }

        [Fact]
        public void Again_plays_the_level_once_more_with_a_fresh_session_at_peak_0_and_its_covers_back()
        {
            Navigator navigator = ShippedNavigator();
            navigator.Open(0);
            AppScreen.Playing first = Assert.IsType<AppScreen.Playing>(navigator.Screen);
            IReadOnlyList<string> notMoved = TapAll(first.Session, 0, 3);

            navigator.Again();

            AppScreen.Playing again = Assert.IsType<AppScreen.Playing>(navigator.Screen);
            Assert.Empty(notMoved);
            Assert.Equal(2, first.Session.Peak.Bays.Value);
            Assert.True(first.Session.State.HasExited(new PieceId(3)), "Vehicle 3 did not leave before Again.");
            Assert.NotSame(first.Session, again.Session);
            Assert.Equal(0, again.Index);
            Assert.Same(first.Session.Entry, again.Session.Entry);
            Assert.Equal(0, again.Session.Peak.Bays.Value);
            Assert.IsType<TapFeedback.None>(again.Session.LastTap);
            Assert.False(again.Session.State.HasExited(new PieceId(3)), "Vehicle 3 is still gone after Again.");
            Assert.Equal(new[] { true, true }, new[] { again.Session.IsShownCovered(new PieceId(3)), again.Session.IsShownCovered(new PieceId(7)) });
        }

        [Fact]
        public void BackToList_from_a_level_shows_the_list_again()
        {
            Navigator navigator = ShippedNavigator();
            AppScreen list = navigator.Screen;
            navigator.Open(25);
            AppScreen level = navigator.Screen;

            navigator.BackToList();

            Assert.IsType<AppScreen.Playing>(level);
            Assert.Same(list, navigator.Screen);
        }

        private static Navigator ShippedNavigator() => new(LevelCatalogue.Load(new DiskLevelTexts()));
    }
}
