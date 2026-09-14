using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Jsonsui.Core;
using Jsonsui.UI.ViewModels;
using Jsonsui.UI.Views;

namespace Jsonsui.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var cli = StartupArguments.Cli ?? CliParser.Parse(desktop.Args ?? []);

            if (cli.Command == CliCommand.Diff)
            {
                // Diff mode: only the diff view is shown; closing it terminates the app.
                var diffVm = new DiffViewModel();
                diffVm.LoadFiles(cli.DiffFileA ?? "", cli.DiffFileB ?? "");
                desktop.MainWindow = new DiffWindow(diffVm);
            }
            else
            {
                var vm = new MainWindowViewModel();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = vm,
                };
                vm.RefreshUI();

                if (cli.JsonPath != null)
                    vm.LoadJsonFromFile(cli.JsonPath, cli.SchemaPath);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
