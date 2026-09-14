using System;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Jsonsui.Core.Services;

namespace Jsonsui.UI.ViewModels;

public partial class DiffViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ObservableCollection<JsonDiffLine> DiffLines { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public void Load(JsonNode original, JsonNode current)
    {
        ErrorMessage = null;
        DiffLines = new ObservableCollection<JsonDiffLine>(JsonDiffService.ComputeDiff(original, current));
    }

    public void LoadFiles(string fileA, string fileB)
    {
        try
        {
            var a = JsonFileService.LoadFromFile(fileA);
            var b = JsonFileService.LoadFromFile(fileB);
            Load(a, b);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            DiffLines = [];
        }
    }
}
