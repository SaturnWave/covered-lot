using PuzzleCore;
using PuzzleCore.Packs;
using PuzzleCore.Records;

namespace CoveredLot.Core.Catalogue
{
    /// <summary>Which level a catalogue problem is about: the Example, or a level the presentation file lists. Closed.</summary>
    public abstract class LevelSource
    {
        private LevelSource()
        {
        }

        /// <summary>The Example, six-by-six.txt.</summary>
        public sealed class Example : LevelSource
        {
            internal Example()
            {
            }
        }

        /// <summary>
        /// A level the presentation file lists: its id, which names its text, and its slot in the pack, which the
        /// development line shows in four digits or more ("slot 0003"). Players never see the id, which names the game's pack.
        /// </summary>
        public sealed class Listed : LevelSource
        {
            internal Listed(PresentationLevel level)
            {
                Id = level.Level;
                Slot = level.Slot;
            }

            public LevelId Id { get; }

            public LevelIndex Slot { get; }
        }
    }
}
