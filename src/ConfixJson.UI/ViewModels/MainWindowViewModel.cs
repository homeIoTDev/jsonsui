using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
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
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    public partial string LoadError { get; set; } = "";

    public bool HasLoadError => !string.IsNullOrEmpty(LoadError);

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
    [NotifyPropertyChangedFor(nameof(ShowEmptyDetailState))]
    public partial JsonEditorNode? ScalarNode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScalarDetail))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyDetailState))]
    public partial string DetailScalarText { get; set; } = "";

    public bool HasScalarDetail => ScalarNode is { Path.Length: > 0 };

    public bool ShowEmptyDetailState => !HasObjectFields && !HasScalarDetail;

    [ObservableProperty]
    public partial DiffViewModel Diff { get; set; } = new();

    [ObservableProperty]
    public partial SchemaFieldInfo EditorHelpInfo { get; set; } = new();

    [ObservableProperty]
    public partial SchemaFieldInfo ArrayHelpInfo { get; set; } = new();

    [ObservableProperty]
    public partial bool CanAddProperty { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<PropertyCatalogItem> AddPropertyItems { get; set; } = [];

    [ObservableProperty]
    public partial bool HasAddPropertySchemaItems { get; set; }

    [ObservableProperty]
    public partial bool CanAddCustomProperty { get; set; }

    public bool EditorHelpVisible =>
        EditorHelpInfo.HasValidationErrors ||
        !string.IsNullOrEmpty(EditorHelpInfo.ContextDescription) ||
        !string.IsNullOrEmpty(EditorHelpInfo.FieldDescription);

    public bool EditorHelpHasDescription =>
        !string.IsNullOrEmpty(EditorHelpInfo.ContextDescription) ||
        !string.IsNullOrEmpty(EditorHelpInfo.FieldDescription);

    public bool ArrayHelpVisible =>
        !string.IsNullOrEmpty(ArrayHelpInfo.Description);

    public bool IsSchemaStatusVisible => !string.IsNullOrEmpty(DocumentFilename);

    public bool IsSchemaLoaded => _state.SchemaStatus == SchemaLoadStatus.Loaded;

    public bool IsSchemaNone => _state.SchemaStatus == SchemaLoadStatus.None;

    public bool IsSchemaFailed => _state.SchemaStatus == SchemaLoadStatus.Failed;

    public string SchemaTooltip
    {
        get
        {
            var fileName = string.IsNullOrEmpty(_state.SchemaFilePath)
                ? ""
                : System.IO.Path.GetFileName(_state.SchemaFilePath);

            return _state.SchemaStatus switch
            {
                SchemaLoadStatus.Loaded when _state.SchemaAutoDetected =>
                    $"Schema: {fileName}\nQuelle: automatisch über $schema erkannt",
                SchemaLoadStatus.Loaded =>
                    $"Schema: {fileName}\nQuelle: manuell ausgewählt",
                SchemaLoadStatus.Failed =>
                    "Das angegebene JSON-Schema konnte nicht geladen werden.\nKlicken, um ein anderes Schema auszuwählen.",
                _ => "Kein JSON-Schema geladen.\nKlicken, um ein Schema auszuwählen."
            };
        }
    }

    public ObservableCollection<JsonEditorNode> TreeRootNodes { get; } = [];

    public MainWindowViewModel()
    {
        RefreshUI();
    }

    // --- Refresh UI from EditorState ---

    public void RefreshUI()
    {
        string[]? focusedPath = null;
        if (_refreshDepth == 0)
            focusedPath = CaptureFocusedFieldPath();

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
            OnPropertyChanged(nameof(IsSchemaStatusVisible));
            OnPropertyChanged(nameof(IsSchemaLoaded));
            OnPropertyChanged(nameof(IsSchemaNone));
            OnPropertyChanged(nameof(IsSchemaFailed));
            OnPropertyChanged(nameof(SchemaTooltip));
        }

        if (_refreshDepth == 0 && focusedPath != null)
            RestoreFocus(focusedPath);
    }

    private Avalonia.Controls.Window? GetWindow()
    {
        return (Avalonia.Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private string[]? CaptureFocusedFieldPath()
    {
        var window = GetWindow();
        if (window == null) return null;
        var focused = window.FocusManager?.GetFocusedElement();
        if (focused is Avalonia.Controls.Control c && c.DataContext is FieldRow row)
            return (string[])row.Path.Clone();
        return null;
    }

    private void RestoreFocus(string[] path)
        => RestoreFocus(path, Avalonia.Threading.DispatcherPriority.Loaded);

    private void RestoreFocus(string[] path, Avalonia.Threading.DispatcherPriority priority)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var window = GetWindow();
            if (window == null) return;
            var control = FindDeepestFocusable(window, path);
            if (control != null)
                control.Focus();
        }, priority);
    }

    private static Avalonia.Controls.Control? FindDeepestFocusable(Avalonia.Visual node, string[] path)
    {
        Avalonia.Controls.Control? best = null;
        int bestDepth = -1;
        FindDeepestFocusableCore(node, path, 0, ref bestDepth, ref best);
        return best;
    }

    private static void FindDeepestFocusableCore(Avalonia.Visual node, string[] path, int depth,
        ref int bestDepth, ref Avalonia.Controls.Control? best)
    {
        if (node is Avalonia.Controls.Control c &&
            c.DataContext is FieldRow row &&
            row.Path.SequenceEqual(path) &&
            c.Focusable &&
            c.IsEffectivelyVisible &&
            depth > bestDepth)
        {
            bestDepth = depth;
            best = c;
        }

        foreach (var child in node.GetVisualChildren())
            FindDeepestFocusableCore(child, path, depth + 1, ref bestDepth, ref best);
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
        CanAddProperty = GetObjectContextPath(_state) != null;
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

    public void ApplyTemporalChange(string[] path, string? value)
    {
        var node = JsonValue.Create(value ?? "");
        EditorLogic.SetValueAtPath(_state, path, node, _undoRedo);
        RefreshErrorState();
    }

    public void ApplyTextChange(FieldRow row, string? value)
    {
        var path = (string[])row.Path.Clone();
        var node = JsonValue.Create(value ?? "");
        EditorLogic.SetValueAtPath(_state, path, node, _undoRedo);

        row.ScalarValue = value ?? "";
        row.HasErrors = _state.Errors.Any(e => e.PathString == string.Join(".", path));
        row.NotifyPropertyChanged(nameof(FieldRow.HasErrors));
        RefreshErrorState();
    }

    public void ApplyNumericChange(FieldRow row, decimal value, bool isInteger)
    {
        var path = (string[])row.Path.Clone();
        var jsonNode = isInteger
            ? JsonValue.Create((int)value)
            : JsonValue.Create((double)value);
        EditorLogic.SetValueAtPath(_state, path, jsonNode, _undoRedo);

        row.NumericValue = value;
        row.OriginalNumericValue = value;
        row.HasErrors = _state.Errors.Any(e => e.PathString == string.Join(".", path));
        row.NotifyPropertyChanged(nameof(FieldRow.HasErrors));
        RefreshErrorState();
    }

    public string? GetFieldRawValue(string[] path)
    {
        var node = JsonDocumentService.GetByPath(_state.Json, path);
        if (node is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return null;
    }

    // --- Add Property (Flyout support) ---

    private string[]? _addPropertyTargetPath;

    private string[]? GetObjectContextPath(EditorState state)
    {
        var effective = EditorLogic.GetEffectiveEditorPath(state);
        if (JsonDocumentService.GetByPath(state.Json, effective) is JsonObject)
            return effective;
        return null;
    }

    public bool PrepareAddPropertyFlyout()
    {
        _addPropertyTargetPath = GetObjectContextPath(_state);
        if (_addPropertyTargetPath == null)
        {
            AddPropertyItems = [];
            HasAddPropertySchemaItems = false;
            CanAddCustomProperty = false;
            return false;
        }

        var catalog = EditorLogic.BuildPropertyCatalog(_state, _addPropertyTargetPath);
        AddPropertyItems = new ObservableCollection<PropertyCatalogItem>(catalog.Items);
        HasAddPropertySchemaItems = AddPropertyItems.Count > 0;
        CanAddCustomProperty = catalog.CanAddCustomProperty;
        return true;
    }

    public void AddSchemaProperty(PropertyCatalogItem item)
    {
        if (item == null || _addPropertyTargetPath == null) return;
        var result = EditorLogic.AddProperty(_state, _addPropertyTargetPath, item.Name, _undoRedo);
        if (result.IsSuccess)
        {
            var newPath = _addPropertyTargetPath.Concat([item.Name]).ToArray();
            _state.FocusFieldPath = newPath;
            RefreshUI();
            RestoreFocus(newPath);
            RestoreFocus(newPath, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    public void AddCustomProperty(string name, string? valueText)
    {
        if (_addPropertyTargetPath == null || string.IsNullOrWhiteSpace(name)) return;
        var value = JsonDocumentService.ParseFlexibleJsonValue(valueText ?? "");
        var result = EditorLogic.AddProperty(_state, _addPropertyTargetPath, name, _undoRedo, value);
        if (result.IsSuccess)
        {
            var newPath = _addPropertyTargetPath.Concat([name]).ToArray();
            _state.FocusFieldPath = newPath;
            RefreshUI();
            RestoreFocus(newPath);
            RestoreFocus(newPath, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    [RelayCommand]
    private void DeleteProperty(FieldRow? row)
    {
        if (row == null || row.IsReadOnly || row.Path.Length == 0) return;

        var objectPath = row.Path[..^1];
        var key = row.Path[^1];

        string? focusKey = null;
        if (JsonDocumentService.GetByPath(_state.Json, objectPath) is JsonObject obj)
        {
            var keys = obj.Select(kv => kv.Key).ToList();
            int idx = keys.IndexOf(key);
            if (idx >= 0)
            {
                if (idx + 1 < keys.Count)
                    focusKey = keys[idx + 1];
                else if (idx > 0)
                    focusKey = keys[idx - 1];
            }
        }

        var result = EditorLogic.RemoveProperty(_state, objectPath, key, _undoRedo);
        if (!result.IsSuccess)
            return;

        if (focusKey != null)
        {
            var focusPath = objectPath.Concat([focusKey]).ToArray();
            _state.FocusFieldPath = focusPath;
            RefreshUI();
            RestoreFocus(focusPath);
            RestoreFocus(focusPath, Avalonia.Threading.DispatcherPriority.Background);
        }
        else
        {
            _state.FocusFieldPath = null;
            RefreshUI();
        }
    }

    private void RefreshErrorState()
    {
        ErrorCount = _state.Errors.Length;
        HasErrors = ErrorCount > 0;
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
        EditorHelpInfo = EditorLogic.GetHelpInfo(_state);
        ArrayHelpInfo = EditorLogic.GetArrayHelpInfo(_state);
        OnPropertyChanged(nameof(EditorHelpVisible));
        OnPropertyChanged(nameof(EditorHelpHasDescription));
        OnPropertyChanged(nameof(ArrayHelpVisible));
    }

    [RelayCommand]
    private void AddCard()
    {
        var (arrayPath, _) = EditorLogic.GetCardArrayContext(_state);
        if (arrayPath.Length == 0) return;
        EditorLogic.AddArrayItem(_state, arrayPath, _undoRedo);
        RefreshUI();
        if (EditorLogic.GetDetailItemValue(_state) is JsonValue)
            FocusScalarDetailEditor();
    }

    private void FocusScalarDetailEditor()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            GetWindow()?.FindControl<TextBox>("ScalarDetailTextBox")?.Focus();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
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
            EditorLogic.Validate(_state);
            RefreshErrorState();
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

    private string[]? _scalarEditingPath;

    public void CaptureScalarEditingPath()
    {
        _scalarEditingPath = EditorLogic.GetDetailItemPath(_state);
    }

    [RelayCommand]
    private void SaveScalarDetail(string? value)
    {
        var detailPath = _scalarEditingPath ?? EditorLogic.GetDetailItemPath(_state);
        if (detailPath == null || string.IsNullOrEmpty(value)) return;
        var jsonNode = EditorLogic.ConvertToJsonNode(value);
        if (jsonNode == null) return;
        EditorLogic.SetValueAtPath(_state, detailPath, jsonNode, _undoRedo);
        _scalarEditingPath = null;
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
        RefreshUI();
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
        Diff.Load(_state.OriginalJson, _state.Json);
        ShowDiff = true;
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
            LoadError = "";
            var file = files[0];
            try
            {
                await using var stream = await file.OpenReadAsync();
                var node = await JsonFileService.LoadFromStreamAsync(stream);
                EditorLogic.InitDocument(_state, node, file.Name);
                DocumentFilename = file.Name;
                _currentFilePath = file.Path.LocalPath;
                _undoRedo.Clear();
                TryAutoLoadSchema(node, file.Path.LocalPath);
                EditorLogic.Validate(_state);
                RefreshUI();
            }
            catch (JsonException ex)
            {
                LoadError = $"Ungültiges JSON – Datei konnte nicht geladen werden:\n{ex.Message}";
            }
            catch (Exception ex)
            {
                LoadError = $"Datei konnte nicht geladen werden: {ex.Message}";
            }
        }
    }

    public void LoadJsonFromFile(string filePath, string? schemaPath = null)
    {
        LoadError = "";
        try
        {
            var node = EditorLogic.LoadDocument(filePath);
            EditorLogic.InitDocument(_state, node, System.IO.Path.GetFileName(filePath));
            DocumentFilename = System.IO.Path.GetFileName(filePath);
            _currentFilePath = filePath;
            _undoRedo.Clear();

            if (schemaPath != null)
                LoadSchemaFromPath(schemaPath);
            else
                TryAutoLoadSchema(node, filePath);

            EditorLogic.Validate(_state);
            RefreshUI();
        }
        catch (JsonException ex)
        {
            LoadError = $"Ungültiges JSON – Datei konnte nicht geladen werden:\n{ex.Message}";
        }
        catch (Exception ex)
        {
            LoadError = $"Datei konnte nicht geladen werden: {ex.Message}";
        }
    }

    private void LoadSchemaFromPath(string schemaPath)
    {
        try
        {
            var loader = new SchemaLoader();
            var schemaDoc = loader.LoadFromString(File.ReadAllText(schemaPath));
            var parser = new SchemaParser();
            var model = parser.Parse(schemaDoc);
            _state.ActiveSchema = model;
            _state.SchemaStatus = SchemaLoadStatus.Loaded;
            _state.SchemaFilePath = schemaPath;
            _state.SchemaAutoDetected = false;
        }
        catch (Exception ex)
        {
            _state.ActiveSchema = null;
            _state.SchemaStatus = SchemaLoadStatus.Failed;
            _state.SchemaFilePath = schemaPath;
            _state.SchemaAutoDetected = false;
            System.Diagnostics.Debug.WriteLine($"[SchemaTrace] manual schema load failed: {ex.Message}");
        }
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
            _state.SchemaStatus = SchemaLoadStatus.None;
            _state.SchemaFilePath = null;
            _state.SchemaAutoDetected = false;
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
            _state.ActiveSchema = null;
            _state.SchemaStatus = SchemaLoadStatus.Failed;
            _state.SchemaFilePath = resolvedPath;
            _state.SchemaAutoDetected = true;
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
            _state.SchemaStatus = SchemaLoadStatus.Loaded;
            _state.SchemaFilePath = resolvedPath;
            _state.SchemaAutoDetected = true;

            System.Diagnostics.Debug.WriteLine("[SchemaTrace] schema parser invoked: true");
            System.Diagnostics.Debug.WriteLine($"[SchemaTrace] parsed schema properties: {model.Properties.Count}");
            System.Diagnostics.Debug.WriteLine("[SchemaTrace] ActiveSchema assigned: true");
        }
        catch (Exception ex)
        {
            _state.ActiveSchema = null;
            _state.SchemaStatus = SchemaLoadStatus.Failed;
            _state.SchemaFilePath = resolvedPath;
            _state.SchemaAutoDetected = true;
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
            try
            {
                var loader = new SchemaLoader();
                var schema = await loader.LoadFromFileAsync(file.Path.LocalPath);
                var parser = new SchemaParser();
                var model = parser.Parse(schema);
                _state.ActiveSchema = model;
                _state.SchemaStatus = SchemaLoadStatus.Loaded;
                _state.SchemaFilePath = file.Path.LocalPath;
                _state.SchemaAutoDetected = false;
            }
            catch (Exception ex)
            {
                _state.ActiveSchema = null;
                _state.SchemaStatus = SchemaLoadStatus.Failed;
                _state.SchemaFilePath = file.Path.LocalPath;
                _state.SchemaAutoDetected = false;
                System.Diagnostics.Debug.WriteLine($"[SchemaTrace] manual schema load failed: {ex.Message}");
            }
            RefreshUI();
        }
    }
}
