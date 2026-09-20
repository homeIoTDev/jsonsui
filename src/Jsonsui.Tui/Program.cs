using System.Globalization;
using Jsonsui.Core;
using Jsonsui.Core.Services;

namespace Jsonsui.Tui;

internal static class Program
{
    private const string AppName = "jsonsui-tui";

    public static int Main(string[] args)
    {
        var cli = CliParser.Parse(args);

        if (cli.Error != null)
        {
            Console.Error.WriteLine($"Error: {cli.Error}");
            PrintUsage();
            return 2;
        }

        ApplyLanguage(cli.Language);

        return cli.Command switch
        {
            CliCommand.Help => PrintUsage(),
            CliCommand.Version => PrintVersion(),
            CliCommand.Diff => RunDiff(cli),
            CliCommand.Open => RunOpen(cli),
            _ => PrintUsage()
        };
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

    private static int RunDiff(CliArguments cli)
    {
        try
        {
            var original = JsonFileService.LoadFromFile(cli.DiffFileA!);
            var current = JsonFileService.LoadFromFile(cli.DiffFileB!);

            foreach (var line in JsonDiffService.ComputeDiff(original, current))
                Console.WriteLine($"{line.Marker} {line.Text}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int RunOpen(CliArguments cli)
    {
        if (cli.JsonPath == null)
        {
            Console.Error.WriteLine("Error: no JSON file specified.");
            PrintUsage();
            return 2;
        }

        try
        {
            var node = JsonFileService.LoadFromFile(cli.JsonPath);
            Console.WriteLine(JsonDocumentService.ToFormattedJson(node));

            if (cli.SchemaPath != null)
            {
                var loader = new SchemaLoader();
                var schemaDoc = loader.LoadFromString(File.ReadAllText(cli.SchemaPath));
                var model = new SchemaParser().Parse(schemaDoc);
                Console.WriteLine();
                Console.WriteLine($"Schema: {cli.SchemaPath} ({model.Properties.Count} properties)");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int PrintUsage()
    {
        Console.WriteLine(CliParser.GetUsage(AppName));
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine(CliParser.GetVersion(AppName));
        return 0;
    }
}
