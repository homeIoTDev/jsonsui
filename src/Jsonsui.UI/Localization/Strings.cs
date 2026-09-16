using System.Globalization;
using System.Resources;

namespace Jsonsui.UI.Localization;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("Jsonsui.UI.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key)
        => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
