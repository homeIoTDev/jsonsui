using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;

namespace Jsonsui.UI.ViewModels;

public partial class DiffViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DiffRow> Rows { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoChanges))]
    public partial bool HasChanges { get; set; }

    [ObservableProperty]
    public partial int AddedCount { get; set; }

    [ObservableProperty]
    public partial int RemovedCount { get; set; }

    [ObservableProperty]
    public partial int ModifiedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    public partial int ChangeCount { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasNoChanges => !HasChanges;
    public string SummaryText => ChangeCount == 1 ? "1 change" : $"{ChangeCount} changes";

    public void Open(JsonNode original, JsonNode current)
    {
        Load(original, current);
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public void Load(JsonNode original, JsonNode current)
    {
        ErrorMessage = null;

        var root = JsonDiffService.Compare(original, current);

        var rows = new List<DiffRow>();
        if (root != null)
            Flatten(root, rows);

        Rows = new ObservableCollection<DiffRow>(rows);
        AddedCount = root?.AddedCount ?? 0;
        RemovedCount = root?.RemovedCount ?? 0;
        ModifiedCount = root?.ModifiedCount ?? 0;
        ChangeCount = AddedCount + RemovedCount + ModifiedCount;
        HasChanges = ChangeCount > 0;
    }

    private static void Flatten(JsonDiffNode node, List<DiffRow> rows)
    {
        if (node.Kind == JsonChangeKind.Unchanged && node.IsContainer)
        {
            foreach (var child in node.Children)
                Flatten(child, rows);
            return;
        }

        rows.Add(new DiffRow
        {
            Kind = node.Kind,
            DisplayPath = node.DisplayPath,
            OldValue = node.OldValue,
            NewValue = node.NewValue
        });
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
            Rows = [];
            AddedCount = 0;
            RemovedCount = 0;
            ModifiedCount = 0;
            ChangeCount = 0;
            HasChanges = false;
        }
    }
}
