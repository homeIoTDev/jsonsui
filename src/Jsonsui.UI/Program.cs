using Avalonia;
using System;
using System.Globalization;
using Jsonsui.Core;

namespace Jsonsui.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var cli = CliParser.Parse(args);
        StartupArguments.Cli = cli;
        ApplyLanguage(cli.Language);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void ApplyLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return;

        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            Console.Error.WriteLine($"Warning: unknown language '{language}', using system language.");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
