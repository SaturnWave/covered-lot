using System;
using System.Collections.Generic;
using CoveredLot.Core.Session;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// What the board shows of a session at one moment (design section 9): the lot's size, its pillars, a tile for each
    /// vehicle still in the lot and, on a covered level, a ghost for each vehicle that has left, and the arrow of each Ahead
    /// tag. The engine decides all of it, and the picture only describes it: the boxes come from the vehicles' footprints,
    /// the faces from what the session shows covered, the departures from the attempt's state, the blocker mark from the
    /// first blocker the engine named for the last tap, and each arrow from the first vehicle on the tagged vehicle's path
    /// at the level's start. A picture is made anew after every tap.
    /// </summary>
    public sealed class BoardPicture
    {
        private BoardPicture(
            LotSize size,
            ReadOnlyArray<PillarPicture> pillars,
            ReadOnlyArray<TilePicture> tiles,
            ReadOnlyArray<GhostPicture> ghosts,
            ReadOnlyArray<AheadLinkPicture> aheadLinks)
        {
            Size = size;
            Pillars = pillars;
            Tiles = tiles;
            Ghosts = ghosts;
            AheadLinks = aheadLinks;
        }

        /// <summary>The lot's width and height in cells.</summary>
        public LotSize Size { get; }

        /// <summary>The pillars, by cell index.</summary>
        public ReadOnlyArray<PillarPicture> Pillars { get; }

        /// <summary>One tile for each vehicle still in the lot, in PieceId order.</summary>
        public ReadOnlyArray<TilePicture> Tiles { get; }

        /// <summary>On a covered level, one ghost for each vehicle that has left, in PieceId order; none on a level without covers.</summary>
        public ReadOnlyArray<GhostPicture> Ghosts { get; }

        /// <summary>One arrow for each Ahead tag, in the level's order of its tags.</summary>
        public ReadOnlyArray<AheadLinkPicture> AheadLinks { get; }

        public static BoardPicture Of(GameSession session)
        {
            LotLevel level = session.Entry.Level;
            LotState state = session.State;
            CluePicture tags = CluePicture.Of(level);
            var marks = new BumpMarks(session.LastTap);
            var tiles = new List<TilePicture>();
            var ghosts = new List<GhostPicture>();
            for (int index = 0; index < level.VehicleCount; index++)
            {
                var piece = new PieceId((byte)index);
                if (!state.HasExited(piece))
                {
                    tiles.Add(new TilePicture(level, piece, FaceOf(session, piece), tags.BadgeOf(piece), marks.OfTile(piece)));
                }
                else if (level.IsCoveredLevel)
                {
                    ghosts.Add(new GhostPicture(level, piece, tags.BadgeOf(piece)));
                }
            }

            return new BoardPicture(level.Size, PillarsOf(level, marks), ReadOnlyArray.CopyOf(tiles), ReadOnlyArray.CopyOf(ghosts), AheadLinksOf(level));
        }

        /// <summary>Covered while the session shows the vehicle covered; otherwise open, in the vehicle's colour.</summary>
        private static TileFace FaceOf(GameSession session, PieceId piece)
        {
            if (session.IsShownCovered(piece))
            {
                return new TileFace.Covered();
            }

            return new TileFace.Open(session.Entry.Level.VehicleOf(piece).Colour);
        }

        private static ReadOnlyArray<PillarPicture> PillarsOf(LotLevel level, BumpMarks marks)
        {
            var pillars = new List<PillarPicture>();
            for (int index = 0; index < level.Size.CellCount; index++)
            {
                var cell = new LotCell((byte)index);
                if ((level.Pillars & cell.Bit) != 0)
                {
                    pillars.Add(new PillarPicture(cell, level.Size, marks.OfPillar(cell)));
                }
            }

            return ReadOnlyArray.CopyOf(pillars);
        }

        private static ReadOnlyArray<AheadLinkPicture> AheadLinksOf(LotLevel level)
        {
            var links = new List<AheadLinkPicture>();
            foreach (LotClue clue in level.Clues)
            {
                if (IsAhead(clue) && AheadLinkPicture.Of(level, clue.On).TryGet(out AheadLinkPicture? link))
                {
                    links.Add(link);
                }
            }

            return ReadOnlyArray.CopyOf(links);
        }

        /// <summary>Whether the tag is an Ahead tag. Every kind of tag is named, so that a kind a later engine adds reaches the discard arm.</summary>
        private static bool IsAhead(LotClue clue) => clue switch
        {
            LotClue.Ahead => true,
            LotClue.Not or LotClue.Touch => false,
            var other => throw new UnknownEngineCaseException(other),
        };

        /// <summary>
        /// The marks of one picture: when the last tap bumped, the first blocker the engine named for it, a vehicle or a
        /// pillar's cell, is marked Blocker, and everything else Plain. Every kind of tap and of blocker is named, so that a
        /// kind a later engine adds reaches a discard arm.
        /// </summary>
        private sealed class BumpMarks
        {
            private readonly Option<PieceId> _vehicle;
            private readonly Option<LotCell> _pillar;

            public BumpMarks(TapFeedback lastTap)
            {
                (_vehicle, _pillar) = lastTap switch
                {
                    TapFeedback.Bumped bumped => bumped.Blocker switch
                    {
                        Blocker.Piece vehicle => (Option.Some(vehicle.Id), Option.None<LotCell>()),
                        Blocker.Pillar pillar => (Option.None<PieceId>(), Option.Some(pillar.Cell)),
                        var other => throw new UnknownEngineCaseException(other),
                    },
                    TapFeedback.None or TapFeedback.Moved or TapFeedback.Ignored => (Option.None<PieceId>(), Option.None<LotCell>()),
                    _ => throw new ArgumentOutOfRangeException(nameof(lastTap), "The board has no mark for this kind of tap feedback."),
                };
            }

            public TileMark OfTile(PieceId piece) =>
                _vehicle.TryGet(out PieceId bumpedInto) && bumpedInto == piece ? TileMark.Blocker : TileMark.Plain;

            public TileMark OfPillar(LotCell cell) =>
                _pillar.TryGet(out LotCell bumpedInto) && bumpedInto == cell ? TileMark.Blocker : TileMark.Plain;
        }
    }
}
