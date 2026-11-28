using System.Globalization;
using CoveredLot.Core.Development;

namespace CoveredLot.Client
{
    /// <summary>
    /// The culture a development build runs in, which the level list's culture button steps through (design section 9):
    /// the device's own, then tr-TR, then sv-SE. Each step sets the current, UI and default-thread cultures together, and
    /// the device's step puts back the four this switch found when it was made; so does <see cref="Restore"/>, which the
    /// game calls when it ends, so that a culture picked in the editor's Play mode does not stay in the editor. Only two
    /// of its methods read the culture the game runs in, <see cref="CurrentName"/> for the development line and
    /// ReadCultures to keep the device's: those reads are the member bans' one reviewed exemption (design section 10.4).
    /// It holds no lambda, so no type the compiler writes for one carries the exemption.
    /// </summary>
    internal sealed class DevCultureSwitch
    {
        /// <summary>The device's four cultures, as this switch found them.</summary>
        private readonly CultureSet _device;

        public DevCultureSwitch()
        {
            _device = ReadCultures();
            Current = DevCulture.Device;
        }

        /// <summary>The step set now: the device's culture until the button is first clicked.</summary>
        public DevCulture Current { get; private set; }

        /// <summary>The name of the culture the game runs in now, such as "tr-TR"; empty for the invariant culture.</summary>
        public static string CurrentName() => CultureInfo.CurrentCulture.Name;

        /// <summary>Sets the next step's cultures: after the device's tr-TR, after tr-TR sv-SE, and after sv-SE the device's again.</summary>
        public void Next()
        {
            Current = DevCultures.Next(Current);
            CultureSet next = DevCultures.NameOf(Current).TryGet(out string? name) ? CultureSet.AllOf(CultureInfo.GetCultureInfo(name)) : _device;
            next.Apply();
        }

        /// <summary>Puts back the device's four cultures, so the next step starts again at tr-TR.</summary>
        public void Restore()
        {
            Current = DevCulture.Device;
            _device.Apply();
        }

        /// <summary>The four cultures the game runs in now; a default-thread culture may be unset (null).</summary>
        private static CultureSet ReadCultures() => new CultureSet(
            current: CultureInfo.CurrentCulture,
            currentUi: CultureInfo.CurrentUICulture,
            defaultThread: CultureInfo.DefaultThreadCurrentCulture,
            defaultThreadUi: CultureInfo.DefaultThreadCurrentUICulture);

        /// <summary>
        /// The current, UI and default-thread cultures, held and set together, so that none can be set in another's place.
        /// Its one call with four different cultures names each argument. It sets them through the setters alone, which the
        /// member bans leave free.
        /// </summary>
        private sealed class CultureSet
        {
            private readonly CultureInfo _current;
            private readonly CultureInfo _currentUi;

            /// <summary>The default culture of new threads; null when unset, as the runtime starts.</summary>
            private readonly CultureInfo? _defaultThread;

            /// <summary>The default UI culture of new threads; null when unset, as the runtime starts.</summary>
            private readonly CultureInfo? _defaultThreadUi;

            public CultureSet(CultureInfo current, CultureInfo currentUi, CultureInfo? defaultThread, CultureInfo? defaultThreadUi)
            {
                _current = current;
                _currentUi = currentUi;
                _defaultThread = defaultThread;
                _defaultThreadUi = defaultThreadUi;
            }

            /// <summary>The same culture in all four places.</summary>
            public static CultureSet AllOf(CultureInfo culture) => new CultureSet(culture, culture, culture, culture);

            public void Apply()
            {
                CultureInfo.DefaultThreadCurrentCulture = _defaultThread;
                CultureInfo.DefaultThreadCurrentUICulture = _defaultThreadUi;
                CultureInfo.CurrentCulture = _current;
                CultureInfo.CurrentUICulture = _currentUi;
            }
        }
    }
}
