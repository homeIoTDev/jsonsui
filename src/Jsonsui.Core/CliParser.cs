using System.Reflection;

namespace Jsonsui.Core;

public enum CliCommand
{
    None,
    Open,
    Diff,
    Help,
    Version
}

public sealed class CliArguments
{
    public CliCommand Command { get; set; } = CliCommand.None;
    public string? JsonPath { get; set; }
    public string? SchemaPath { get; set; }
    public string? DiffFileA { get; set; }
    public string? DiffFileB { get; set; }
    public string? Language { get; set; }
    public string? Error { get; set; }
}

public static class CliParser
{
    public static string GetUsage(string appName)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "Usage:",
            $"  {appName} <json> [--schema <path>] [--lang <code>]",
            $"  {appName} <json> --schema <path>",
            $"  {appName} --diff <fileA> <fileB>",
            $"  {appName} --help",
            $"  {appName} --version",
        });
    }

    public static string GetVersion(string appName)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        return $"{appName} {version}";
    }

    public static CliArguments Parse(string[] args)
    {
        var result = new CliArguments();
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--lang=", StringComparison.Ordinal))
            {
                var language = args[i]["--lang=".Length..];
                if (string.IsNullOrWhiteSpace(language))
                    result.Error = "Option '--lang' requires a language argument (e.g. en, de).";
                else
                    result.Language = language;
                continue;
            }

            switch (args[i])
            {
                case "--diff":
                    result.Command = CliCommand.Diff;
                    break;

                case "--lang":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        result.Language = args[++i];
                    else
                        result.Error = "Option '--lang' requires a language argument (e.g. en, de).";
                    break;

                case "--schema":
                    if (i + 1 < args.Length)
                        result.SchemaPath = args[++i];
                    else
                        result.Error = "Option '--schema' requires a path argument.";
                    break;

                case "--help":
                case "-h":
                    result.Command = CliCommand.Help;
                    break;

                case "--version":
                case "-v":
                    result.Command = CliCommand.Version;
                    break;

                default:
                    var arg = args[i];
                    if (arg.StartsWith('-') && arg.Length > 1)
                        result.Error = $"Unknown option '{arg}'.";
                    else
                        positional.Add(arg);
                    break;
            }
        }

        if (result.Command == CliCommand.Diff)
        {
            if (positional.Count >= 2)
            {
                result.DiffFileA = positional[0];
                result.DiffFileB = positional[1];
            }
            else if (result.Error == null)
            {
                result.Error = "Option '--diff' requires two file arguments.";
            }
        }
        else if (result.Command != CliCommand.Help && result.Command != CliCommand.Version)
        {
            if (positional.Count > 0)
                result.JsonPath = positional[0];

            if (result.JsonPath != null || result.SchemaPath != null)
                result.Command = CliCommand.Open;
        }

        return result;
    }
}
