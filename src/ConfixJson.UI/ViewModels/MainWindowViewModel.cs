using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConfixJson.Core.Services;
using ConfixJson.UI.Models;

namespace ConfixJson.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly UndoRedoService _undoRedo = new();

    private JsonNode _json = new JsonObject();
    private JsonNode _originalJson = new JsonObject();
    private string[] _selectedPath = [];
    private HashSet<string> _expanded = [];
    private int? _cardIndex;
    private string[]? _focusFieldPath;

    // --- Observable Properties ---

    [ObservableProperty]
    public partial bool Dark { get; set; } = true;

    [ObservableProperty]
    public partial bool TextMode { get; set; }

    [ObservableProperty]
    public partial bool ShowDiff { get; set; }

    [ObservableProperty]
    public partial bool ShowSaved { get; set; }

    [ObservableProperty]
    public partial string DocumentFilename { get; set; } = "config.json";

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

    // Tree data
    public ObservableCollection<JsonEditorNode> TreeRootNodes { get; } = [];
    private JsonEditorNode? _rootNode;

    // Validation
    private ValidationError[] _allErrors = [];

    // Flat node lookup by path key
    private readonly Dictionary<string, JsonEditorNode> _nodeByPathKey = new();

    public MainWindowViewModel()
    {
    }

    // --- Tree Building ---

    private void RebuildTree()
    {
        TreeRootNodes.Clear();
        _nodeByPathKey.Clear();
        _rootNode = BuildNode(_json, "root", [], 0, null);
        _rootNode.IsExpanded = true;
        _rootNode.IsSelected = _selectedPath.Length == 0;
        TreeRootNodes.Add(_rootNode);
    }

    private JsonEditorNode BuildNode(JsonNode? value, string label, string[] path, int depth, JsonEditorNode? parent)
    {
        var node = new JsonEditorNode
        {
            Label = label,
            Path = path,
            Value = value,
            Depth = depth,
            Parent = parent
        };

        var pathKey = node.PathKey;
        _nodeByPathKey[pathKey] = node;

        if (value is JsonObject obj)
        {
            node.NodeType = path.Length == 0 || path[^1] == "root" ? "root" : "object";
            foreach (var kvp in obj)
            {
                var childPath = path.Concat([kvp.Key]).ToArray();
                var child = BuildNode(kvp.Value, kvp.Key, childPath, depth + 1, node);
                child.IsExpanded = _expanded.Contains(child.PathKey);
                node.Children.Add(child);
            }
        }
        else if (value is JsonArray arr)
        {
            node.NodeType = "array";
            for (int i = 0; i < arr.Count; i++)
            {
                var childPath = path.Concat([i.ToString()]).ToArray();
                var child = BuildNode(arr[i], $"[{i}]", childPath, depth + 1, node);
                if (arr[i] is JsonObject)
                    child.IsExpanded = _expanded.Contains(child.PathKey);
                node.Children.Add(child);
            }
        }
        else
        {
            node.NodeType = "scalar";
        }

        return node;
    }

    private void UpdateNodeSelection()
    {
        var selectedKey = string.Join("/", _selectedPath);
        foreach (var (key, node) in _nodeByPathKey)
        {
            node.IsSelected = key == selectedKey || _selectedPath.Length > 0 && key.StartsWith(selectedKey + "/");
        }
    }

    private void MarkErrors()
    {
        foreach (var node in _nodeByPathKey.Values)
            node.HasErrors = false;

        foreach (var err in _allErrors)
        {
            var key = string.Join("/", err.Path);
            if (_nodeByPathKey.TryGetValue(key, out var node))
                node.HasErrors = true;
        }
    }

    // --- Document Operations ---

    public void SetValueAtPath(string[] path, JsonNode? value)
    {
        var oldValue = JsonDocumentService.GetByPath(_json, path);
        _json = JsonDocumentService.SetByPath(_json, path, value ?? JsonValue.Create<string?>(null)!);
        _undoRedo.PushUndo(new UndoCommand
        {
            Path = path,
            OldValue = oldValue?.DeepClone(),
            NewValue = value?.DeepClone()
        });
        OnDocumentChanged();
    }

    public void AddArrayItem(string[] arrayPath)
    {
        var arr = JsonDocumentService.GetByPath(_json, arrayPath) as JsonArray;
        JsonNode? template;
        if (arr != null && arr.Count > 0)
            template = JsonDocumentService.CreateTemplate(arr[0]);
        else
            template = new JsonObject();

        _json = JsonDocumentService.AddArrayItem(_json, arrayPath, template!);
        var newIndex = ((JsonArray?)JsonDocumentService.GetByPath(_json, arrayPath))?.Count - 1 ?? 0;
        _undoRedo.PushUndo(new UndoCommand
        {
            Path = arrayPath,
            NewValue = template,
            Action = "add_array_item",
            ArrayIndex = newIndex
        });

        OnDocumentChanged();

        // Auto-select new item
        if (NestedCtx != null)
            NestedCtx.CardIndex = newIndex;
        else
            _cardIndex = newIndex;

        RefreshDerived();
    }

    public void RemoveArrayItem(string[] arrayPath, int index)
    {
        var oldValue = (JsonDocumentService.GetByPath(_json, arrayPath) as JsonArray)?[index];
        _json = JsonDocumentService.RemoveArrayItem(_json, arrayPath, index);
        _undoRedo.PushUndo(new UndoCommand
        {
            Path = arrayPath,
            OldValue = oldValue?.DeepClone(),
            Action = "remove_array_item",
            ArrayIndex = index
        });

        // Clear selection if needed
        int activeCard = NestedCtx?.CardIndex ?? _cardIndex ?? -1;
        if (activeCard >= index)
        {
            var arr = JsonDocumentService.GetByPath(_json, arrayPath) as JsonArray;
            int newCount = arr?.Count ?? 0;
            if (newCount == 0)
            {
                if (NestedCtx != null)
                    NestedCtx.CardIndex = null;
                else
                    _cardIndex = null;
            }
            else if (activeCard >= newCount)
            {
                if (NestedCtx != null)
                    NestedCtx.CardIndex = newCount - 1;
                else
                    _cardIndex = newCount - 1;
            }
        }

        OnDocumentChanged();
        RefreshDerived();
    }

    private void OnDocumentChanged()
    {
        ValidateDocument();
        RebuildTree();
        UpdateNodeSelection();
        MarkErrors();
        UpdateCanUndoRedo();
        RefreshDerived();
    }

    // --- Validation ---

    private void ValidateDocument()
    {
        var errors = new List<ValidationError>();
        ValidateNode(_json, [], errors);
        _allErrors = errors.ToArray();
        ErrorCount = _allErrors.Length;
        HasErrors = ErrorCount > 0;
    }

    private void ValidateNode(JsonNode? node, string[] path, List<ValidationError> errors)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                var childPath = path.Concat([kvp.Key]).ToArray();
                if (kvp.Value is JsonValue val && val.GetValueKind() == JsonValueKind.Null)
                {
                    errors.Add(new ValidationError { Path = childPath, Message = $"{kvp.Key} is null" });
                }
                else if (kvp.Value is JsonValue sv && sv.TryGetValue<string>(out var str) && string.IsNullOrEmpty(str))
                {
                    errors.Add(new ValidationError { Path = childPath, Message = $"{kvp.Key} is empty" });
                }
                else
                {
                    ValidateNode(kvp.Value, childPath, errors);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
                ValidateNode(arr[i], path.Concat([i.ToString()]).ToArray(), errors);
        }
    }

    // --- Derived Properties / State Refresh ---

    private void RefreshDerived()
    {
        UpdateDerivedProperties();
    }

    private void UpdateDerivedProperties()
    {
        var selected = JsonDocumentService.GetByPath(_json, _selectedPath);
        SelectedPathString = string.Join(" / ", _selectedPath);

        // Determine card array path and active card index
        string[] cardArrayPath;
        int? activeCardIndex;

        if (NestedCtx != null)
        {
            cardArrayPath = NestedCtx.ArrayPath;
            activeCardIndex = NestedCtx.CardIndex;
        }
        else if (selected is JsonArray)
        {
            cardArrayPath = _selectedPath;
            activeCardIndex = _cardIndex;
        }
        else
        {
            cardArrayPath = [];
            activeCardIndex = null;
        }

        CardArrayPathString = string.Join(" / ", cardArrayPath);

        if (TextMode)
        {
            CurrentEditorMode = EditorMode.Text;
            TextEditorPathString = string.Join(" / ", _selectedPath);
            TextContent = JsonDocumentService.ToFormattedJson(selected ?? _json);
            HasTextParseError = false;
            TextParseError = "";
        }
        else if ((selected is JsonArray && ((JsonArray)selected).Count > 0) || NestedCtx != null)
        {
            CurrentEditorMode = EditorMode.ArraySplit;
            BuildCardItems(cardArrayPath);
            BuildDetailPanel(cardArrayPath, activeCardIndex);
        }
        else if (selected is JsonObject)
        {
            CurrentEditorMode = EditorMode.Object;
            BuildObjectFields(_selectedPath);
        }
        else if (selected is JsonValue)
        {
            CurrentEditorMode = EditorMode.Scalar;
            ScalarNode = BuildScalarNode(_selectedPath, selected);
        }
        else
        {
            CurrentEditorMode = EditorMode.Empty;
        }

        UpdateHelpInfo();
    }

    private void BuildCardItems(string[] arrayPath)
    {
        CardItems.Clear();
        var arr = JsonDocumentService.GetByPath(_json, arrayPath) as JsonArray;
        if (arr == null) return;

        int activeIndex = NestedCtx?.CardIndex ?? _cardIndex ?? -1;

        for (int i = 0; i < arr.Count; i++)
        {
            var itemPath = arrayPath.Concat([i.ToString()]).ToArray();
            CardItems.Add(new CardItem
            {
                Index = i,
                ItemPath = itemPath,
                Preview = BuildCardPreview(arr[i]),
                HasErrors = _allErrors.Any(e => e.PathString == string.Join(".", itemPath)),
                IsSelected = i == activeIndex
            });
        }
    }

    private static string BuildCardPreview(JsonNode? item)
    {
        if (item is JsonObject obj)
        {
            var pairs = obj.Take(3).Select(kvp => $"{kvp.Key}: {JsonDocumentService.GetScalarPreview(kvp.Value, 20)}");
            return string.Join(", ", pairs) + (obj.Count > 3 ? ", ..." : "");
        }
        return JsonDocumentService.GetScalarPreview(item, 60);
    }

    private void BuildDetailPanel(string[] arrayPath, int? cardIndex)
    {
        if (cardIndex == null)
        {
            DetailPathString = "";
            ObjectFields.Clear();
            return;
        }

        var itemPath = arrayPath.Concat([cardIndex.Value.ToString()]).ToArray();
        var itemValue = JsonDocumentService.GetByPath(_json, itemPath);

        DetailPathString = string.Join(" / ", itemPath);

        if (itemValue is JsonObject)
            BuildObjectFields(itemPath);
        else if (itemValue is JsonValue)
            ScalarNode = BuildScalarNode(itemPath, itemValue);
    }

    private void BuildObjectFields(string[] objectPath)
    {
        ObjectFields.Clear();
        var obj = JsonDocumentService.GetByPath(_json, objectPath) as JsonObject;
        if (obj == null) return;

        foreach (var kvp in obj)
        {
            var fieldPath = objectPath.Concat([kvp.Key]).ToArray();
            var fieldType = kvp.Value switch
            {
                JsonObject => "object",
                JsonArray => "array",
                _ => "scalar"
            };

            var row = new FieldRow
            {
                Key = kvp.Key,
                Path = fieldPath,
                FieldType = fieldType,
                IsRequired = false, // Would come from schema
                HasErrors = _allErrors.Any(e => e.PathString == string.Join(".", fieldPath))
            };

            if (kvp.Value is JsonObject nestedObj)
                row.NestedObjectSummary = $"{{{nestedObj.Count} fields}}";
            else if (kvp.Value is JsonArray nestedArr)
                row.ArrayItemCount = $"[{nestedArr.Count}]";
            else if (kvp.Value is JsonValue)
                row.ScalarValue = JsonDocumentService.GetScalarPreview(kvp.Value, 200);

            ObjectFields.Add(row);
        }
    }

    private JsonEditorNode? BuildScalarNode(string[] path, JsonNode? value)
    {
        if (value is not JsonValue) return null;
        var node = new JsonEditorNode
        {
            Label = path.Length > 0 ? path[^1] : "value",
            Path = path,
            Value = value,
            NodeType = "scalar"
        };
        node.HasErrors = _allErrors.Any(e => e.PathString == string.Join(".", path));
        return node;
    }

    private void UpdateHelpInfo()
    {
        var focusPath = _focusFieldPath ?? _selectedPath;
        var pathStr = string.Join(" / ", focusPath);

        var errors = _allErrors.Where(e => string.Join(".", e.Path) == string.Join(".", focusPath)).ToArray();

        HelpInfo = new SchemaFieldInfo
        {
            PathString = pathStr,
            HasValidationErrors = errors.Length > 0,
            ErrorMessages = errors.Length > 0
                ? string.Join("; ", errors.Select(e => e.Message))
                : null
        };
    }

    private void UpdateCanUndoRedo()
    {
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
    }

    // --- Commands ---

    [RelayCommand]
    private void SelectNode(JsonEditorNode? node)
    {
        if (node == null) return;

        _selectedPath = node.Path;
        NestedCtx = null;
        _cardIndex = null;

        if (node.NodeType == "object" || node.NodeType == "root")
        {
            if (!node.IsExpanded)
            {
                node.IsExpanded = true;
                _expanded.Add(node.PathKey);
            }
        }

        if (node.Value is JsonArray arr && arr.Count > 0)
            _cardIndex = 0;

        UpdateNodeSelection();
        RefreshDerived();
        OnPropertyChanged(nameof(TreeRootNodes));
    }

    [RelayCommand]
    private void ToggleExpand(JsonEditorNode? node)
    {
        if (node == null || !node.IsExpandable) return;

        node.IsExpanded = !node.IsExpanded;
        if (node.IsExpanded)
            _expanded.Add(node.PathKey);
        else
            _expanded.Remove(node.PathKey);

        _selectedPath = node.Path;
        UpdateNodeSelection();
        RefreshDerived();
        OnPropertyChanged(nameof(TreeRootNodes));
    }

    [RelayCommand]
    private void ChangeField(object? parameter)
    {
        if (parameter is not object[] args || args.Length < 2) return;
        if (args[0] is not string[] path) return;

        var value = args[1];
        var jsonNode = value switch
        {
            string s => (JsonNode?)JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            double d => JsonValue.Create(d),
            long l => JsonValue.Create(l),
            _ => null
        };

        if (jsonNode != null)
            SetValueAtPath(path, jsonNode);
    }

    [RelayCommand]
    private void AddCard()
    {
        var arrayPath = NestedCtx?.ArrayPath ?? _selectedPath;
        AddArrayItem(arrayPath);
    }

    [RelayCommand]
    private void DeleteCard(int? index)
    {
        if (index == null) return;
        var arrayPath = NestedCtx?.ArrayPath ?? _selectedPath;
        RemoveArrayItem(arrayPath, index.Value);
    }

    [RelayCommand]
    private void SelectCard(int? index)
    {
        if (index == null) return;
        if (NestedCtx != null)
            NestedCtx.CardIndex = index.Value;
        else
            _cardIndex = index.Value;
        RefreshDerived();
    }

    [RelayCommand]
    private void DrillIntoArray(string[]? path)
    {
        if (path == null) return;
        NestedCtx = new NestedContext { ArrayPath = path, CardIndex = 0 };
        _focusFieldPath = null;
        RefreshDerived();
    }

    [RelayCommand]
    private void ExitNestedArray()
    {
        NestedCtx = null;
        _focusFieldPath = null;
        RefreshDerived();
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            JsonFileService.SaveToFile(DocumentFilename, _json);
            _originalJson = _json.DeepClone();
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
    }

    [RelayCommand]
    private void ToggleTextMode()
    {
        TextMode = !TextMode;
        if (TextMode)
            TextContent = JsonDocumentService.ToFormattedJson(JsonDocumentService.GetByPath(_json, _selectedPath) ?? _json);
        RefreshDerived();
    }

    [RelayCommand]
    private void OpenDiff()
    {
        ShowDiff = true;
        var original = JsonDocumentService.ToFormattedJson(_originalJson);
        var current = JsonDocumentService.ToFormattedJson(_json);
        var diff = JsonDiffService.ComputeDiff(original, current);
        DiffLines = new ObservableCollection<JsonDiffLine>(diff);
    }

    [RelayCommand]
    private void CloseDiff()
    {
        ShowDiff = false;
    }

    [RelayCommand]
    private void FocusField(string[]? path)
    {
        _focusFieldPath = path;
        UpdateHelpInfo();
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
            var node = JsonNode.Parse(text);
            if (node != null && _selectedPath.Length > 0)
            {
                _json = JsonDocumentService.SetByPath(_json, _selectedPath, node);
                OnDocumentChanged();
                HasTextParseError = false;
            }
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
        _json = _undoRedo.ApplyUndo(_json);
        OnDocumentChanged();
    }

    [RelayCommand]
    private void Redo()
    {
        _json = _undoRedo.ApplyRedo(_json);
        OnDocumentChanged();
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
            InitDocument(node, file.Name);
        }
    }

    public void LoadJsonFromFile(string filePath)
    {
        var node = JsonFileService.LoadFromFile(filePath);
        InitDocument(node, System.IO.Path.GetFileName(filePath));
    }

    private void InitDocument(JsonNode node, string filename)
    {
        _json = node;
        _originalJson = node.DeepClone();
        DocumentFilename = filename;
        _selectedPath = [];
        _expanded = [];
        _cardIndex = null;
        NestedCtx = null;
        _undoRedo.Clear();
        RebuildTree();
        SelectNodeByPath([]);
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
            HelpInfo.Description = model.Description ?? $"Schema loaded: {file.Name}";
        }
    }

    // --- Helper ---

    private void SelectNodeByPath(string[] path)
    {
        _selectedPath = path;
        UpdateNodeSelection();
        UpdateDerivedProperties();
    }
}
