using Jsonsui.Core;
using Jsonsui.Core.Services;

namespace Jsonsui.Tui;

internal static class Program
{
    public static int Main(string[] args)
    {
        var cli = CliParser.Parse(args);

        if (cli.Error != null)
        {
            Console.Error.WriteLine($"Error: {cli.Error}");
            PrintUsage();
            return 2;
        }

        return cli.Command switch
        {
            CliCommand.Help => PrintUsage(),
            CliCommand.Diff => RunDiff(cli),
            CliCommand.Open => RunOpen(cli),
            _ => PrintUsage()
        };
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
        Console.WriteLine("Usage:");
        Console.WriteLine("  jsonsui-tui <json>");
        Console.WriteLine("  jsonsui-tui <json> --schema <path>");
        Console.WriteLine("  jsonsui-tui --diff <fileA> <fileB>");
        Console.WriteLine("  jsonsui-tui --help");
        return 0;
    }
}
