using System;
using PuzzleCore;

namespace CoveredLot.Core.Development
{
    /// <summary>The culture button's cycle, and the name of the culture each of its steps sets.</summary>
    public static class DevCultures
    {
        /// <summary>The culture the button sets after this one: the device's, then tr-TR, then sv-SE, then the device's again.</summary>
        public static DevCulture Next(DevCulture culture) => culture switch
        {
            DevCulture.Device => DevCulture.Turkish,
            DevCulture.Turkish => DevCulture.Swedish,
            DevCulture.Swedish => DevCulture.Device,
            _ => throw new ArgumentOutOfRangeException(nameof(culture), "The culture cycle has no step after this culture."),
        };

        /// <summary>The name of the culture to set, "tr-TR" or "sv-SE"; None for the device's own, which the host keeps from its start.</summary>
        public static Option<string> NameOf(DevCulture culture) => culture switch
        {
            DevCulture.Device => Option.None<string>(),
            DevCulture.Turkish => Option.Some("tr-TR"),
            DevCulture.Swedish => Option.Some("sv-SE"),
            _ => throw new ArgumentOutOfRangeException(nameof(culture), "The culture cycle has no name for this culture."),
        };
    }
}
