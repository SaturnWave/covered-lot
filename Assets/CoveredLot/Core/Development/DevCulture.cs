namespace CoveredLot.Core.Development
{
    /// <summary>
    /// The culture a development build runs in (design sections 9 and 16): the device's own, tr-TR or sv-SE. The level
    /// list's culture button, which only development builds have, steps through them (DevCultures.Next), so that the
    /// player's Turkish and Swedish culture tables can be tried on a phone without changing the phone's language.
    /// </summary>
    public enum DevCulture
    {
        Device,
        Turkish,
        Swedish,
    }
}
