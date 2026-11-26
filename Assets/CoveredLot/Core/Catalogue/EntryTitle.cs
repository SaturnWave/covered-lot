namespace CoveredLot.Core.Catalogue
{
    /// <summary>What the level list and the top bar call a catalogue entry (design section 9): the Example, or Level n. Closed.</summary>
    public abstract class EntryTitle
    {
        private EntryTitle()
        {
        }

        /// <summary>The Example, the brief's rules board, which comes first.</summary>
        public sealed class Example : EntryTitle
        {
            internal Example()
            {
            }
        }

        /// <summary>A listed level, Level n.</summary>
        public sealed class Numbered : EntryTitle
        {
            internal Numbered(LevelNumber number) => Number = number;

            public LevelNumber Number { get; }
        }
    }
}
