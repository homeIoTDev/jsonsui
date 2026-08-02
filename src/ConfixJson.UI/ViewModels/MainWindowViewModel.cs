using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConfixJson.Core.Models;
using ConfixJson.Core.Services;

namespace ConfixJson.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly UndoRedoService _undoRedo = new();
    private readonly EditorState _state = new();

    // --- UI-only Observable Properties ---

    [ObservableProperty]
    public partial bool Dark { get; set; } = true;

    [ObservableProperty]
    public partial bool TextMode { get; set; }

    [ObservableProperty]
    public partial bool ShowDiff { get; set; }

    [ObservableProperty]
    public partial bool ShowSaved { get; set; }

    [ObservableProperty]
    public partial string DocumentFilename { get; set; } = "";

    [ObservableProperty]
    public partial string SelectedPathString { get; set; } = "";

    [ObservableProperty]
    public partial string CardArrayPathString { get; set; } = "";

    [ObservableProperty]
    public partial string DetailPathString { get; set; } = "";

    [ObservableProperty]
    public partial string TextEditorPathString { get; set; } = "";

    [ObservableProperty]
    public partial string TextContent { get; set; } = "";

    [ObservableProperty]
    public partial string TextParseError { get; set; } = "";

    [ObservableProperty]
    public partial bool HasTextParseError { get; set; }

    [ObservableProperty]
    public partial int ErrorCount { get; set; }

    [ObservableProperty]
    public partial bool HasErrors { get; set; }

    [ObservableProperty]
    public partial bool CanUndo { get; set; }

    [ObservableProperty]
    public partial bool CanRedo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsObjectMode))]
    [NotifyPropertyChangedFor(nameof(IsArrayMode))]
    [NotifyPropertyChangedFor(nameof(IsScalarMode))]
    [NotifyPropertyChangedFor(nameof(IsNotTextMode))]
    public partial EditorMode CurrentEditorMode { get; set; } = EditorMode.Empty;

    public bool IsObjectMode => CurrentEditorMode == EditorMode.Object;
    public bool IsArrayMode => CurrentEditorMode == EditorMode.ArraySplit;
    public bool IsScalarMode => CurrentEditorMode == EditorMode.Scalar;
    public bool IsNotTextMode => CurrentEditorMode != EditorMode.Text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasObjectFields))]
    public partial ObservableCollection<FieldRow> ObjectFields { get; set; } = [];

    public bool HasObjectFields => ObjectFields.Count > 0;

    [ObservableProperty]
    public partial NestedContext? NestedCtx { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CardItem> CardItems { get; set; } = [];

    [ObservableProperty]
    public partial JsonEditorNode? ScalarNode { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<JsonDiffLine> DiffLines { get; set; } = [];

    [ObservableProperty]
    public partial SchemaFieldInfo HelpInfo { get; set; } = new();

    public ObservableCollection<JsonEditorNode> TreeRootNodes { get; } = [];

    public MainWindowViewModel()
    {
        RefreshUI();
    }

    // --- Refresh UI from EditorState ---

    public void RefreshUI()
    {
        // Sync state-derived observable properties
        SelectedPathString = EditorLogic.GetSelectedPathString(_state);
        CardArrayPathString = EditorLogic.GetCardArrayPathString(_state);
        DetailPathString = EditorLogic.GetDetailPathString(_state);
        CurrentEditorMode = EditorLogic.GetEditorMode(_state, TextMode);
        ErrorCount = _state.Errors.Length;
        HasErrors = ErrorCount > 0;
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
        NestedCtx = _state.NestedCtx;
        HelpInfo = EditorLogic.GetHelpInfo(_state);

        // Rebuild tree
        var root = EditorLogic.BuildTree(_state);
        EditorLogic.MarkErrors(root, _state.Errors);
        TreeRootNodes.Clear();
        TreeRootNodes.Add(root);

        // Reset all mode-specific content
        ObjectFields.Clear();
        CardItems.Clear();
        ScalarNode = new ConfixJson.Core.Models.JsonEditorNode { Label = "", Path = [], NodeType = "scalar" };
        TextContent = "";
        TextEditorPathString = "";

        // Build mode-specific content
        var mode = CurrentEditorMode;
        if (mode == EditorMode.Text)
        {
            TextEditorPathString = SelectedPathString;
            var selected = EditorLogic.GetSelectedValue(_state);
            TextContent = JsonDocumentService.ToFormattedJson(selected ?? _state.Json);
        }
        else if (mode == EditorMode.ArraySplit)
        {
            var (cardArrayPath, _) = EditorLogic.GetCardArrayContext(_state);
            CardItems = new ObservableCollection<CardItem>(EditorLogic.BuildCardItems(_state, cardArrayPath));

            var detailPath = EditorLogic.GetDetailItemPath(_state);
            if (detailPath != null)
            {
                var detailValue = EditorLogic.GetDetailItemValue(_state);
                if (detailValue is JsonObject)
                    ObjectFields = new ObservableCollection<FieldRow>(EditorLogic.BuildObjectFields(_state, detailPath));
                else if (detailValue is JsonValue)
                    ScalarNode = EditorLogic.BuildScalarNode(_state, detailPath, detailValue);
            }
        }
        else if (mode == EditorMode.Object)
        {
            ObjectFields = new ObservableCollection<FieldRow>(EditorLogic.BuildObjectFields(_state, _state.SelectedPath));
        }
        else if (mode == EditorMode.Scalar)
        {
            var selected = EditorLogic.GetSelectedValue(_state);
            ScalarNode = EditorLogic.BuildScalarNode(_state, _state.SelectedPath, selected);
        }
    }

    // --- Commands ---

    [RelayCommand]
    private void SelectNode(JsonEditorNode? node)
    {
        if (node == null) return;
        EditorLogic.SelectNode(_state, node);
        RefreshUI();
    }

    [RelayCommand]
    private void ToggleExpand(JsonEditorNode? node)
    {
        if (node == null) return;
        EditorLogic.ToggleExpand(_state, node);
        RefreshUI();
    }

    [RelayCommand]
    private void ChangeField(object? parameter)
    {
        if (parameter is not object[] args || args.Length < 2) return;
        if (args[0] is not string[] path) return;

        var jsonNode = EditorLogic.ConvertToJsonNode(args[1]);
        if (jsonNode != null)
        {
            EditorLogic.SetValueAtPath(_state, path, jsonNode, _undoRedo);
            RefreshUI();
        }
    }

    [RelayCommand]
    private void AddCard()
    {
        var arrayPath = _state.NestedCtx?.ArrayPath ?? _state.SelectedPath;
        EditorLogic.AddArrayItem(_state, arrayPath, _undoRedo);
        RefreshUI();
    }

    [RelayCommand]
    private void DeleteCard(int? index)
    {
        if (index == null) return;
        var arrayPath = _state.NestedCtx?.ArrayPath ?? _state.SelectedPath;
        EditorLogic.RemoveArrayItem(_state, arrayPath, index.Value, _undoRedo);
        RefreshUI();
    }

    [RelayCommand]
    private void SelectCard(int? index)
    {
        EditorLogic.SelectCard(_state, index);
        RefreshUI();
    }

    [RelayCommand]
    private void DrillIntoArray(string[]? path)
    {
        if (path == null) return;
        EditorLogic.DrillIntoArray(_state, path);
        RefreshUI();
    }

    [RelayCommand]
    private void ExitNestedArray()
    {
        EditorLogic.ExitNestedArray(_state);
        RefreshUI();
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            EditorLogic.SaveDocument(DocumentFilename, _state.Json);
            _state.OriginalJson = _state.Json.DeepClone();
            ShowSaved = true;
            Task.Delay(2000).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowSaved = false);
            });
        }
        catch (Exception ex)
        {
            TextParseError = $"Save error: {ex.Message}";
            HasTextParseError = true;
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        Dark = !Dark;
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = Dark
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
    }

    [RelayCommand]
    private void ToggleTextMode()
    {
        TextMode = !TextMode;
        RefreshUI();
    }

    [RelayCommand]
    private void OpenDiff()
    {
        ShowDiff = true;
        DiffLines = new ObservableCollection<JsonDiffLine>(EditorLogic.GetDiffLines(_state));
    }

    [RelayCommand]
    private void CloseDiff()
    {
        ShowDiff = false;
    }

    [RelayCommand]
    private void FocusField(string[]? path)
    {
        EditorLogic.FocusField(_state, path);
        HelpInfo = EditorLogic.GetHelpInfo(_state);
    }

    [RelayCommand]
    private void ParseText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            TextContent = "";
            return;
        }

        TextContent = text;
        try
        {
            EditorLogic.ParseText(_state, text);
            HasTextParseError = false;
            TextParseError = "";
            RefreshUI();
        }
        catch (JsonException ex)
        {
            HasTextParseError = true;
            TextParseError = ex.Message;
        }
    }

    [RelayCommand]
    private void Undo()
    {
        EditorLogic.Undo(_state, _undoRedo);
        RefreshUI();
    }

    [RelayCommand]
    private void Redo()
    {
        EditorLogic.Redo(_state, _undoRedo);
        RefreshUI();
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open JSON File",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });

        if (files.Count > 0)
        {
            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            var node = await JsonFileService.LoadFromStreamAsync(stream);
            EditorLogic.InitDocument(_state, node, file.Name);
            DocumentFilename = file.Name;
            _undoRedo.Clear();
            RefreshUI();
        }
    }

    public void LoadJsonFromFile(string filePath)
    {
        var node = EditorLogic.LoadDocument(filePath);
        EditorLogic.InitDocument(_state, node, System.IO.Path.GetFileName(filePath));
        DocumentFilename = System.IO.Path.GetFileName(filePath);
        _undoRedo.Clear();
        RefreshUI();
    }

    [RelayCommand]
    private async Task LoadSchema()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Schema File",
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("JSON Schema") { Patterns = ["*.json", "*.schema.json"] }]
        });

        if (files.Count > 0)
        {
            var file = files[0];
            var loader = new SchemaLoader();
            var schema = await loader.LoadFromFileAsync(file.Path.LocalPath);
            var parser = new SchemaParser();
            var model = parser.Parse(schema);
            HelpInfo = new SchemaFieldInfo { Description = model.Description ?? $"Schema loaded: {file.Name}" };
        }
    }
}
