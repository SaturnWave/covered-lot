using System.Collections.Generic;
using CoveredLot.Core.Catalogue;
using PuzzleCore;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>A test-only host that hands each request on to another and records the names asked for, in order.</summary>
    internal sealed class RecordingLevelTexts : ILevelTexts
    {
        private readonly ILevelTexts _texts;
        private readonly List<LevelTextName> _asked = new List<LevelTextName>();

        public RecordingLevelTexts(ILevelTexts texts) => _texts = texts;

        public IReadOnlyList<LevelTextName> Asked => _asked;

        public Option<string> Read(LevelTextName name)
        {
            _asked.Add(name);
            return _texts.Read(name);
        }
    }
}
