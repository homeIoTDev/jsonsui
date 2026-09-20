using Avalonia;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Jsonsui.Core;

namespace Jsonsui.UI;

sealed class Program
{
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private const string AppName = "jsonsui";
    private static bool _consoleAttached;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        var cli = CliParser.Parse(args);

        if (cli.Error != null)
        {
            AttachConsole();
            Console.Error.WriteLine($"Error: {cli.Error}");
            Console.WriteLine(CliParser.GetUsage(AppName));
            return 2;
        }

        if (cli.Command == CliCommand.Help)
        {
            AttachConsole();
            Console.WriteLine(CliParser.GetUsage(AppName));
            return 0;
        }

        if (cli.Command == CliCommand.Version)
        {
            AttachConsole();
            Console.WriteLine(CliParser.GetVersion(AppName));
            return 0;
        }

        ApplyLanguage(cli.Language);
        StartupArguments.Cli = cli;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
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

    private static void AttachConsole()
    {
        if (!OperatingSystem.IsWindows() || _consoleAttached)
            return;

        _consoleAttached = true;

        if (!AttachConsoleNative(AttachParentProcess))
            return;

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    [DllImport("kernel32.dll", EntryPoint = "AttachConsole", SetLastError = true)]
    private static extern bool AttachConsoleNative(uint dwProcessId);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
