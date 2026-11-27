using System.Collections.Generic;
using CoveredLot.Core.Catalogue;
using CoveredLot.Core.Session;
using PuzzleCore;
using PuzzleCore.Jam;

namespace CoveredLot.Core.Pictures
{
    /// <summary>
    /// What the HUD shows of a session at one moment (design sections 8.3 and 9): the entry's title, the line of the bays
    /// that are free, the line of the peak against the par, the status line of the last tap, a picture of each bay from
    /// the left, and, on a covered level only, the ledger's pills and the tags list. The engine decides all of it: the bays
    /// and the colours waiting in them come from the attempt's state, the ledger's counts from the rules' IsCovered, and
    /// the status from what the last tap did. A picture is made anew after every tap.
    /// </summary>
    public sealed class HudPicture
    {
        private HudPicture(GameSession session)
        {
            CatalogueEntry entry = session.Entry;
            LotLevel level = entry.Level;
            LotState state = session.State;
            Title = Copy.TitleOf(entry.Title);
            BaysFreeLine = Copy.BaysFreeLine(state.FreeSlots);
            PeakLine = Copy.PeakLine(session.Peak, entry.PeakPar);
            StatusLine = Copy.StatusLine(session.LastTap);
            Bays = BaysOf(state);
            Ledger = level.IsCoveredLevel ? LedgerOf(level, state) : ReadOnlyArray.Empty<LedgerPill>();
            Tags = level.IsCoveredLevel ? Option.Some(CluePicture.Of(level)) : Option.None<CluePicture>();
        }

        /// <summary>The entry's title, which the top bar shows: "Example", "Level 12".</summary>
        public string Title { get; }

        /// <summary>"4 bays free", "1 bay free".</summary>
        public string BaysFreeLine { get; }

        /// <summary>The session's peak so far against the level's par: "Most bays used: 2 (par 3)".</summary>
        public string PeakLine { get; }

        /// <summary>
        /// What the last tap did, in words: "Cover lifted: blue.", "Three blue cleared." or both, "Something is in its
        /// way.", or nothing (an empty line) before the first tap, after a move that lifted no cover and cleared no set, and
        /// after an ignored tap.
        /// </summary>
        public string StatusLine { get; }

        /// <summary>A picture of each bay the state has, bay 0 on the left.</summary>
        public ReadOnlyArray<BayPicture> Bays { get; }

        /// <summary>
        /// On a covered level, one pill for each colour the level's vehicles have, in ColourId order, zeros included; none on
        /// a level without covers.
        /// </summary>
        public ReadOnlyArray<LedgerPill> Ledger { get; }

        /// <summary>On a covered level, the tags list; None on a level without covers.</summary>
        public Option<CluePicture> Tags { get; }

        public static HudPicture Of(GameSession session) => new HudPicture(session);

        private static ReadOnlyArray<BayPicture> BaysOf(LotState state)
        {
            var bays = new List<BayPicture>();
            for (int index = 0; index < state.Capacity.Value; index++)
            {
                bays.Add(BayPicture.Of(state.ColourIn(new SlotIndex((byte)index))));
            }

            return ReadOnlyArray.CopyOf(bays);
        }

        /// <summary>
        /// For each colour id that a vehicle of the level has, in ascending order, how many of its vehicles the engine's
        /// rules call covered in this state (JamRules.IsCovered): still under the cover they start with.
        /// </summary>
        private static ReadOnlyArray<LedgerPill> LedgerOf(LotLevel level, LotState state)
        {
            var rules = new JamRules(level);
            var pills = new List<LedgerPill>();
            for (int value = ColourId.MinValue; value <= ColourId.MaxValue; value++)
            {
                var colour = new ColourId((byte)value);
                int vehicles = 0;
                int covered = 0;
                foreach (Vehicle vehicle in level.Vehicles)
                {
                    if (vehicle.Colour != colour)
                    {
                        continue;
                    }

                    vehicles++;
                    if (rules.IsCovered(state, vehicle.Id))
                    {
                        covered++;
                    }
                }

                if (vehicles > 0)
                {
                    pills.Add(new LedgerPill(colour, covered));
                }
            }

            return ReadOnlyArray.CopyOf(pills);
        }
    }
}
