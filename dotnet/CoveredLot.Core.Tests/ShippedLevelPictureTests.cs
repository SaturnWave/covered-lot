using System;
using System.Collections.Generic;
using System.Linq;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Session;
using CoveredLot.Core.Tests.Support;
using PuzzleCore;
using PuzzleCore.Jam;
using PuzzleCore.Rules;
using Xunit;
using static CoveredLot.Core.Tests.Support.PictureDescriptions;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// BoardPicture on every level the game ships, the Example and the levels presentation.json lists, as the catalogue
    /// loads them from the shipped texts (design sections 8.3 and 9). Along the winning order the engine's par search finds
    /// for each, the picture at the start and after every tap has one tile for each vehicle still in the lot and none for
    /// any other; a tile the session shows covered is Covered, with "?" for its letter, so it carries no colour, and every
    /// other tile is open in its vehicle's own swatch and letter; and a covered level has a ghost for each vehicle that has
    /// left, a level without covers none. Each Ahead tag of the shipped levels links its vehicle's front cell, the cell
    /// next to the first cell of its path, to the first cell of that path that the vehicle ahead stands on, both worked out
    /// again through the engine's geometry. Every count is asserted, so no check can pass on an empty walk.
    /// </summary>
    public sealed class ShippedLevelPictureTests
    {
        /// <summary>The node budget of the par search, as the session tests give it: every shipped level needs far fewer.</summary>
        private const int ParSearchNodes = 200_000;

        /// <summary>How many of the disagreements a failure lists; it gives their count as well.</summary>
        private const int ShownDisagreements = 20;

        /// <summary>The swatch of each colour id from 1, in the tests' own words.</summary>
        private static readonly string[] SwatchNames = { "Red", "Blue", "Yellow", "Green", "Pink", "Orange" };

        [Fact]
        public void Along_each_shipped_level_s_par_path_a_tile_shown_covered_never_carries_a_colour()
        {
            ReadOnlyArray<CatalogueEntry> entries = ShippedCatalogue.Entries();

            var tally = new WalkTally();
            foreach (CatalogueEntry entry in entries)
            {
                new ParPathWalk(entry, tally).Walk();
            }

            Assert.Equal(ShippedCatalogue.ListedLevelCount + 1, entries.Count);
            Assert.Equal(entries.Count, tally.Levels);
            Assert.Equal(entries.Count(entry => entry.Level.IsCoveredLevel), tally.CoveredLevels);
            // Every vehicle leaves once in a winning order, so the orders tap each vehicle of each level once.
            Assert.Equal(entries.Sum(entry => entry.Level.VehicleCount), tally.Taps);
            Assert.Equal(tally.Levels + tally.Taps, tally.Pictures);
            Assert.True(tally.CoveredLevels > 0 && tally.CoveredTiles > 0 && tally.Ghosts > 0,
                "The walk met " + Number(tally.CoveredLevels) + " covered levels, " + Number(tally.CoveredTiles) + " covered tiles and "
                + Number(tally.Ghosts) + " ghosts.");
            Assert.True(tally.OpenTiles > 0, "The walk met no open tile.");
            Assert.True(tally.Wrong.Count == 0, "The pictures disagree with the engine " + Number(tally.Wrong.Count) + " times; the first "
                + Number(Math.Min(ShownDisagreements, tally.Wrong.Count)) + ":\n" + string.Join("\n", tally.Wrong.Take(ShownDisagreements)));
        }

        [Fact]
        public void Each_Ahead_tag_of_the_shipped_levels_links_its_vehicle_s_front_cell_to_the_first_cell_of_the_vehicle_ahead()
        {
            ReadOnlyArray<CatalogueEntry> entries = ShippedCatalogue.Entries();

            var wrong = new List<string>();
            var facings = new List<Facing>();
            int links = 0;
            foreach (CatalogueEntry entry in entries)
            {
                LotLevel level = entry.Level;
                BoardPicture board = BoardPicture.Of(new GameSession(entry));
                var expected = new List<string>();
                foreach (LotClue clue in level.Clues)
                {
                    if (clue is LotClue.Ahead ahead)
                    {
                        expected.Add(ExpectedLink(level, ahead.On));
                        facings.Add(level.VehicleOf(ahead.On).Facing);
                    }
                }

                IReadOnlyList<string> actual = board.AheadLinks.Select(link => Link(link, level.Size)).ToList();
                links += actual.Count;
                if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    wrong.Add(CatalogueDescriptions.Entry(entry) + ": the links are [" + string.Join("; ", actual) + "], the engine's geometry gives ["
                        + string.Join("; ", expected) + "]");
                }
            }

            Assert.Equal(ShippedCatalogue.ListedLevelCount + 1, entries.Count);
            Assert.NotEmpty(facings);
            Assert.Equal(facings.Count, links);
            // The premise, from the engine: the shipped tags are on vehicles facing each way the front-cell rule tells apart,
            // north or west (the lowest cell of the footprint) and east or south (the highest).
            Assert.Contains(facings, facing => facing is Facing.North or Facing.West);
            Assert.Contains(facings, facing => facing is Facing.East or Facing.South);
            Assert.True(wrong.Count == 0, "These links are not where the engine's geometry puts them:\n" + string.Join("\n", wrong));
        }

        /// <summary>
        /// The link of an Ahead tag, worked out through the engine's geometry apart from the picture: it starts at the cell of
        /// the vehicle's footprint from which one step along its facing reaches the first cell of its path, and it ends at
        /// the first cell of the path that the first vehicle on it at the start stands on.
        /// </summary>
        private static string ExpectedLink(LotLevel level, PieceId tagged)
        {
            IReadOnlyList<LotCell> path = level.PathOf(tagged);
            Facing facing = level.VehicleOf(tagged).Facing;
            ulong footprint = level.FootprintOf(tagged);
            var fronts = new List<int>();
            for (int index = 0; index < level.Size.CellCount; index++)
            {
                var cell = new LotCell((byte)index);
                if ((footprint & cell.Bit) != 0 && path.Count > 0 && level.Size.Step(cell, facing) == Option.Some(path[0]))
                {
                    fronts.Add(index);
                }
            }

            Option<PieceId> ahead = level.FirstOnPathAtStart(tagged);
            var references = new List<int>();
            foreach (LotCell cell in path)
            {
                if (ahead.HasValue && level.PieceAt(cell) == ahead)
                {
                    references.Add(cell.Index);
                }
            }

            string front = fronts.Count == 1 ? "cell " + Number(fronts[0]) : "no single front cell";
            string reference = references.Count > 0 ? "cell " + Number(references[0]) : "no cell of a vehicle ahead";
            return front + " to " + reference;
        }

        /// <summary>
        /// The walk of one level: a new session taps the winning order the par search finds, and the picture is checked at
        /// the start and after every tap.
        /// </summary>
        private sealed class ParPathWalk
        {
            private readonly CatalogueEntry _entry;
            private readonly WalkTally _tally;
            private readonly GameSession _session;
            private int _taps;

            public ParPathWalk(CatalogueEntry entry, WalkTally tally)
            {
                _entry = entry;
                _tally = tally;
                _session = new GameSession(entry);
            }

            private LotLevel Level => _entry.Level;

            private string Name => CatalogueDescriptions.Entry(_entry);

            public void Walk()
            {
                _tally.Levels++;
                if (Level.IsCoveredLevel)
                {
                    _tally.CoveredLevels++;
                }

                Check("at the start");
                if (!(LotSolver.PeakPar(Level, Level.Bays.AsCapacity, new NodeBudget(ParSearchNodes)) is PeakParResult.Found found))
                {
                    _tally.Wrong.Add(Name + ": the par search found no winning order");
                    return;
                }

                foreach (Exit exit in found.Path)
                {
                    TapFeedback feedback = _session.Tap(exit.Piece);
                    _taps++;
                    _tally.Taps++;
                    string moment = "after tap " + Number(_taps) + " (vehicle " + Number(exit.Piece.Value) + ")";
                    if (!(feedback is TapFeedback.Moved))
                    {
                        _tally.Wrong.Add(Name + " " + moment + ": the tap gave " + feedback.GetType().Name);
                    }

                    Check(moment);
                }
            }

            private void Check(string moment)
            {
                BoardPicture board = BoardPicture.Of(_session);
                _tally.Pictures++;
                LotState state = _session.State;

                IReadOnlyList<int> inLot = Pieces(piece => !state.HasExited(piece));
                IReadOnlyList<int> tiles = board.Tiles.Select(tile => (int)tile.Piece.Value).ToList();
                if (!tiles.SequenceEqual(inLot))
                {
                    _tally.Wrong.Add(Name + " " + moment + ": tiles for vehicles " + Listed(tiles) + ", in the lot " + Listed(inLot));
                }

                foreach (TilePicture tile in board.Tiles)
                {
                    CheckTile(tile, moment);
                }

                IReadOnlyList<int> left = Level.IsCoveredLevel ? Pieces(piece => state.HasExited(piece)) : Array.Empty<int>();
                IReadOnlyList<int> ghosts = board.Ghosts.Select(ghost => (int)ghost.Piece.Value).ToList();
                _tally.Ghosts += ghosts.Count;
                if (!ghosts.SequenceEqual(left))
                {
                    _tally.Wrong.Add(Name + " " + moment + ": ghosts of vehicles " + Listed(ghosts) + ", where " + Listed(left) + " are due");
                }
            }

            /// <summary>
            /// A tile the session shows covered is Covered, with "?" before its facing's glyph; any other is open, in its
            /// vehicle's swatch, with its colour's letter before the glyph.
            /// </summary>
            private void CheckTile(TilePicture tile, string moment)
            {
                Vehicle vehicle = Level.VehicleOf(tile.Piece);
                bool isShownCovered = _session.IsShownCovered(tile.Piece);
                string letter = isShownCovered ? "?" : LotText.ColourLetters.Substring(vehicle.Colour.Value - 1, 1);
                string face = isShownCovered ? "Covered" : "Open(" + SwatchNames[vehicle.Colour.Value - 1] + ", " + letter + ")";
                string label = letter + GlyphOf(vehicle.Facing);
                if (isShownCovered)
                {
                    _tally.CoveredTiles++;
                }
                else
                {
                    _tally.OpenTiles++;
                }

                if (Face(tile.Face) != face || tile.Label != label)
                {
                    _tally.Wrong.Add(Name + " " + moment + ": tile " + Number(tile.Piece.Value) + " is " + Face(tile.Face) + " " + tile.Label
                        + ", where the session shows it " + face + " " + label);
                }
            }

            /// <summary>The level's vehicles that the test holds, by id in ascending order.</summary>
            private IReadOnlyList<int> Pieces(Func<PieceId, bool> holds)
            {
                var pieces = new List<int>();
                for (int index = 0; index < Level.VehicleCount; index++)
                {
                    if (holds(new PieceId((byte)index)))
                    {
                        pieces.Add(index);
                    }
                }

                return pieces;
            }

            private static string GlyphOf(Facing facing) => facing switch
            {
                Facing.North => "^",
                Facing.East => ">",
                Facing.South => "v",
                Facing.West => "<",
                _ => throw new ArgumentOutOfRangeException(nameof(facing), "No glyph for this facing."),
            };

            private static string Listed(IReadOnlyList<int> pieces) => "[" + string.Join(",", pieces.Select(Number)) + "]";
        }

        /// <summary>What the walks did, counted so that no check passes on an empty walk, and what they found wrong.</summary>
        private sealed class WalkTally
        {
            public int Levels { get; set; }

            public int CoveredLevels { get; set; }

            public int Taps { get; set; }

            public int Pictures { get; set; }

            public int CoveredTiles { get; set; }

            public int OpenTiles { get; set; }

            public int Ghosts { get; set; }

            public List<string> Wrong { get; } = new();
        }
    }
}
