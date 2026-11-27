using System.Globalization;
using CoveredLot.Core.Development;

namespace CoveredLot.Client
{
    /// <summary>
    /// The culture a development build runs in, which the level list's culture button steps through (design section 9):
    /// the device's own, then tr-TR, then sv-SE. Each step sets the current, UI and default-thread cultures together, and
    /// the device's step puts back the four this switch found when it was made. It is the one Client type that reads the
    /// culture the game runs in, to keep the device's and to name it on the development line: those reads are the member
    /// bans' one reviewed exemption (design section 10.4). It holds no lambda, so no type the compiler writes for one
    /// carries the exemption.
    /// </summary>
    internal sealed class DevCultureSwitch
    {
        private readonly CultureInfo _deviceCulture;
        private readonly CultureInfo _deviceUiCulture;

        /// <summary>The device's default culture for new threads; null when none was set, as the runtime starts.</summary>
        private readonly CultureInfo? _deviceDefaultThreadCulture;

        /// <summary>The device's default UI culture for new threads; null when none was set, as the runtime starts.</summary>
        private readonly CultureInfo? _deviceDefaultThreadUiCulture;

        public DevCultureSwitch()
        {
            _deviceCulture = CultureInfo.CurrentCulture;
            _deviceUiCulture = CultureInfo.CurrentUICulture;
            _deviceDefaultThreadCulture = CultureInfo.DefaultThreadCurrentCulture;
            _deviceDefaultThreadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            Current = DevCulture.Device;
        }

        /// <summary>The step set now: the device's culture until the button is first clicked.</summary>
        public DevCulture Current { get; private set; }

        /// <summary>The name of the culture the game runs in now, such as "tr-TR"; empty for the invariant culture.</summary>
        public static string CurrentName() => CultureInfo.CurrentCulture.Name;

        /// <summary>Sets the next step's culture: after the device's tr-TR, after tr-TR sv-SE, and after sv-SE the device's again.</summary>
        public void Next()
        {
            Current = DevCultures.Next(Current);
            if (DevCultures.NameOf(Current).TryGet(out string? name))
            {
                CultureInfo named = CultureInfo.GetCultureInfo(name);
                Set(named, named, named, named);
            }
            else
            {
                Set(_deviceCulture, _deviceUiCulture, _deviceDefaultThreadCulture, _deviceDefaultThreadUiCulture);
            }
        }

        private static void Set(CultureInfo current, CultureInfo currentUi, CultureInfo? defaultThread, CultureInfo? defaultThreadUi)
        {
            CultureInfo.DefaultThreadCurrentCulture = defaultThread;
            CultureInfo.DefaultThreadCurrentUICulture = defaultThreadUi;
            CultureInfo.CurrentCulture = current;
            CultureInfo.CurrentUICulture = currentUi;
        }
    }
}
