using System;
using System.Collections.ObjectModel;
using System.IO;
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
    private string _currentFilePath = "";
    private int _refreshDepth;

    public bool IsRefreshing => _refreshDepth > 0;

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
    public partial string EffectivePathString { get; set; } = "";

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
    [NotifyPropertyChangedFor(nameof(ShowEmptyDetailState))]
    public partial ObservableCollection<FieldRow> ObjectFields { get; set; } = [];

    public bool HasObjectFields => ObjectFields.Count > 0;

    [ObservableProperty]
    public partial NestedContext? NestedCtx { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CardItem> CardItems { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScalarDetail))]
    public partial JsonEditorNode? ScalarNode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScalarDetail))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyDetailState))]
    public partial string DetailScalarText { get; set; } = "";

    public bool HasScalarDetail => !string.IsNullOrEmpty(DetailScalarText);

    public bool ShowEmptyDetailState => !HasObjectFields && !HasScalarDetail;

    [ObservableProperty]
    public partial ObservableCollection<JsonDiffLine> DiffLines { get; set; } = [];

    [ObservableProperty]
    public partial SchemaFieldInfo EditorHelpInfo { get; set; } = new();

    [ObservableProperty]
    public partial SchemaFieldInfo ArrayHelpInfo { get; set; } = new();

    public bool EditorHelpVisible =>
        EditorHelpInfo.HasValidationErrors ||
        !string.IsNullOrEmpty(EditorHelpInfo.ContextDescription) ||
        !string.IsNullOrEmpty(EditorHelpInfo.FieldDescription);

    public bool EditorHelpHasDescription =>
        !string.IsNullOrEmpty(EditorHelpInfo.ContextDescription) ||
        !string.IsNullOrEmpty(EditorHelpInfo.FieldDescription);

    public bool ArrayHelpVisible =>
        !string.IsNullOrEmpty(ArrayHelpInfo.Description);

    public ObservableCollection<JsonEditorNode> TreeRootNodes { get; } = [];

    public MainWindowViewModel()
    {
        RefreshUI();
    }

    // --- Refresh UI from EditorState ---

    public void RefreshUI()
    {
        _refreshDepth++;
        try
        {
            RefreshUIImpl();
        }
        finally
        {
            _refreshDepth--;
            OnPropertyChanged(nameof(EditorHelpVisible));
            OnPropertyChanged(nameof(EditorHelpHasDescription));
            OnPropertyChanged(nameof(ArrayHelpVisible));
        }
    }

    private void RefreshUIImpl()
    {
        var stack = new System.Diagnostics.StackTrace(1, false);
        var frame = stack.GetFrame(0);
        var caller = frame?.GetMethod()?.Name ?? "unknown";
        System.Diagnostics.Debug.WriteLine(
            $"[RefreshUI] START caller={caller} SelectedPath={string.Join("/", _state.SelectedPath)}");

        // Sync state-derived observable properties
        SelectedPathString = EditorLogic.GetSelectedPathString(_state);
        EffectivePathString = EditorLogic.GetEffectivePathString(_state);
        CardArrayPathString = EditorLogic.GetCardArrayPathString(_state);
        DetailPathString = EditorLogic.GetDetailPathString(_state);
        CurrentEditorMode = EditorLogic.GetEditorMode(_state, TextMode);
        ErrorCount = _state.Errors.Length;
        HasErrors = ErrorCount > 0;
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
        NestedCtx = _state.NestedCtx;
        EditorHelpInfo = EditorLogic.GetHelpInfo(_state);
        ArrayHelpInfo = EditorLogic.GetArrayHelpInfo(_state);
        System.Diagnostics.Debug.WriteLine($"[RefreshUI] mode={CurrentEditorMode}");

        // Rebuild tree
        var root = EditorLogic.BuildTree(_state);
        EditorLogic.MarkErrors(root, _state.Errors);
        TreeRootNodes.Clear();
        TreeRootNodes.Add(root);

        // Reset all mode-specific content
        ObjectFields.Clear();
        CardItems.Clear();
        ScalarNode = new ConfixJson.Core.Models.JsonEditorNode { Label = "", Path = [], NodeType = "scalar" };
        DetailScalarText = "";
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
                else if (detailValue is JsonValue jv)
                {
                    DetailScalarText = JsonDocumentService.GetScalarPreview(jv, 200);
                    ScalarNode = EditorLogic.BuildScalarNode(_state, detailPath, detailValue);
                }
            }
        }
        else if (mode == EditorMode.Object)
        {
            var effectivePath = EditorLogic.GetEffectiveEditorPath(_state);
            System.Diagnostics.Debug.WriteLine("[RefreshUI] BuildObjectFields START");
            ObjectFields = new ObservableCollection<FieldRow>(EditorLogic.BuildObjectFields(_state, effectivePath));
            System.Diagnostics.Debug.WriteLine("[RefreshUI] BuildObjectFields END");
        }
        else if (mode == EditorMode.Scalar)
        {
            var selected = EditorLogic.GetSelectedValue(_state);
            ScalarNode = EditorLogic.BuildScalarNode(_state, _state.SelectedPath, selected);
        }

        System.Diagnostics.Debug.WriteLine("[RefreshUI] END");
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
    private void DrillInto(string[]? path)
    {
        if (path == null) return;
        EditorLogic.DrillInto(_state, path);
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
        var (arrayPath, _) = EditorLogic.GetCardArrayContext(_state);
        if (arrayPath.Length == 0) return;
        EditorLogic.AddArrayItem(_state, arrayPath, _undoRedo);
        RefreshUI();
    }

    [RelayCommand]
    private void DeleteCard(int? index)
    {
        if (index == null) return;
        var (arrayPath, _) = EditorLogic.GetCardArrayContext(_state);
        if (arrayPath.Length == 0) return;
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
            EditorLogic.SaveDocument(_currentFilePath, _state.Json);
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
    private void SaveScalarDetail(string? value)
    {
        var detailPath = EditorLogic.GetDetailItemPath(_state);
        if (detailPath == null || string.IsNullOrEmpty(value)) return;
        var jsonNode = EditorLogic.ConvertToJsonNode(value);
        if (jsonNode == null) return;
        EditorLogic.SetValueAtPath(_state, detailPath, jsonNode, _undoRedo);
        RefreshUI();
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
        System.Diagnostics.Debug.WriteLine($"[FocusTrace] FocusField path={string.Join("/", path ?? [])}");
        EditorLogic.FocusField(_state, path);
        EditorHelpInfo = EditorLogic.GetHelpInfo(_state);
        OnPropertyChanged(nameof(EditorHelpVisible));
        OnPropertyChanged(nameof(EditorHelpHasDescription));
        System.Diagnostics.Debug.WriteLine(
            $"[FocusTrace] FocusField done ContextDescription='{EditorHelpInfo.ContextDescription}' " +
            $"FieldDescription='{EditorHelpInfo.FieldDescription}'");
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
            _currentFilePath = file.Path.LocalPath;
            _undoRedo.Clear();
            TryAutoLoadSchema(node, file.Path.LocalPath);
            RefreshUI();
        }
    }

    public void LoadJsonFromFile(string filePath)
    {
        var node = EditorLogic.LoadDocument(filePath);
        EditorLogic.InitDocument(_state, node, System.IO.Path.GetFileName(filePath));
        DocumentFilename = System.IO.Path.GetFileName(filePath);
        _currentFilePath = filePath;
        _undoRedo.Clear();
        TryAutoLoadSchema(node, filePath);
        RefreshUI();
    }

    private void TryAutoLoadSchema(JsonNode root, string jsonFilePath)
    {
        System.Diagnostics.Debug.WriteLine($"[SchemaTrace] JSON loaded: {jsonFilePath}");

        string? schemaRef = null;
        if (root is JsonObject obj && obj["$schema"] is JsonValue sv)
            schemaRef = sv.GetValue<string>();

        System.Diagnostics.Debug.WriteLine($"[SchemaTrace] top-level $schema: {schemaRef ?? "NULL"}");

        if (string.IsNullOrEmpty(schemaRef))
        {
            _state.ActiveSchema = null;
            System.Diagnostics.Debug.WriteLine("[SchemaTrace] ActiveSchema cleared — no $schema in this document");
            return;
        }

        string resolvedPath;
        if (Path.IsPathRooted(schemaRef))
            resolvedPath = schemaRef;
        else
            resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(jsonFilePath) ?? "", schemaRef));

        System.Diagnostics.Debug.WriteLine($"[SchemaTrace] resolved schema path: {resolvedPath}");

        if (!File.Exists(resolvedPath))
        {
            System.Diagnostics.Debug.WriteLine("[SchemaTrace] schema file exists: False — skipping auto-load");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[SchemaTrace] schema file exists: True");

        try
        {
            var loader = new SchemaLoader();
            var schemaDoc = loader.LoadFromString(File.ReadAllText(resolvedPath));
            var parser = new SchemaParser();
            var model = parser.Parse(schemaDoc);
            _state.ActiveSchema = model;

            System.Diagnostics.Debug.WriteLine("[SchemaTrace] schema parser invoked: true");
            System.Diagnostics.Debug.WriteLine($"[SchemaTrace] parsed schema properties: {model.Properties.Count}");
            System.Diagnostics.Debug.WriteLine("[SchemaTrace] ActiveSchema assigned: true");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SchemaTrace] schema load failed: {ex.Message}");
        }
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
            _state.ActiveSchema = model;
            RefreshUI();
        }
    }
}
