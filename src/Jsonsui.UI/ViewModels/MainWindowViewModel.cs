using System;
using System.Collections.Generic;
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
using Jsonsui.Core.Models;
using Jsonsui.Core.Services;
using Jsonsui.UI.Localization;

namespace Jsonsui.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly UndoRedoService _undoRedo = new();
    private readonly EditorState _state = new();
    private readonly Avalonia.Threading.DispatcherTimer _textParseTimer;
    private string _currentFilePath = "";
    private int _refreshDepth;
    private bool _suppressTextSync;

    public bool IsRefreshing => _refreshDepth > 0;

    // --- UI-only Observable Properties ---

    [ObservableProperty]
    public partial bool Dark { get; set; } = true;

    [ObservableProperty]
    public partial bool TextMode { get; set; }

    [ObservableProperty]
    public partial bool ShowSaved { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument))]
    public partial string DocumentFilename { get; set; } = "";

    public bool HasDocument => !string.IsNullOrEmpty(DocumentFilename);

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

    public ObservableCollection<ErrorListItem> ErrorItems { get; set; } = [];

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

    [ObservableProperty]
    public partial FieldRow? ScalarRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetailArray))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyDetailState))]
    public partial FieldRow? DetailArrayRow { get; set; }

    public bool HasScalarDetail => ScalarNode is { Path.Length: > 0 };

    public bool HasDetailArray => DetailArrayRow != null;

    public bool ShowEmptyDetailState => !HasObjectFields && !HasScalarDetail && !HasDetailArray;

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
                    Strings.Format("Schema_Tooltip_Auto", fileName),
                SchemaLoadStatus.Loaded =>
                    Strings.Format("Schema_Tooltip_Manual", fileName),
                SchemaLoadStatus.Failed =>
                    Strings.Get("Schema_Tooltip_Failed"),
                _ => Strings.Get("Schema_Tooltip_None")
            };
        }
    }

    public ObservableCollection<JsonEditorNode> TreeRootNodes { get; } = [];

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnFilterTextChanged(string value) => RefreshTree();

    partial void OnTextContentChanged(string value)
    {
        if (_suppressTextSync || !TextMode) return;

        _textParseTimer.Stop();
        _textParseTimer.Start();
    }

    private void ApplyTextContent()
    {
        if (!TextMode) return;

        var value = TextContent;
        if (string.IsNullOrWhiteSpace(value))
        {
            HasTextParseError = false;
            TextParseError = "";
            return;
        }

        try
        {
            EditorLogic.ParseText(_state, value);
            HasTextParseError = false;
            TextParseError = "";
            RefreshUI(syncText: false);
        }
        catch (JsonException ex)
        {
            HasTextParseError = true;
            TextParseError = ex.Message;
        }
    }

    public MainWindowViewModel()
    {
        _textParseTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _textParseTimer.Tick += (_, _) =>
        {
            _textParseTimer.Stop();
            ApplyTextContent();
        };

        RefreshUI();
    }

    // --- Refresh UI from EditorState ---

    private enum FocusOwner
    {
        None,
        Field,
        Tree,
        Card
    }

    public void RefreshUI(bool syncText = true)
    {
        var (focusOwner, focusedPath) = _refreshDepth == 0
            ? CaptureFocus()
            : (FocusOwner.None, null);

        _refreshDepth++;
        try
        {
            RefreshUIImpl(syncText);
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

        if (_refreshDepth != 0) return;

        switch (focusOwner)
        {
            case FocusOwner.Field when focusedPath != null:
                RestoreFocus(focusedPath);
                break;
            case FocusOwner.Tree:
                FocusTreeSelection();
                break;
            case FocusOwner.Card:
                FocusSelectedCard();
                break;
        }
    }

    private Avalonia.Controls.Window? GetWindow()
    {
        return (Avalonia.Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private (FocusOwner Owner, string[]? Path) CaptureFocus()
    {
        var window = GetWindow();
        var focused = window?.FocusManager?.GetFocusedElement();
        switch (focused)
        {
            case Avalonia.Controls.Control c1 when c1.DataContext is FieldRow row:
                return (FocusOwner.Field, (string[])row.Path.Clone());
            case Avalonia.Controls.Control c2 when c2.DataContext is JsonEditorNode:
                return (FocusOwner.Tree, null);
            case Avalonia.Controls.Control c3 when c3.DataContext is CardItem:
                return (FocusOwner.Card, null);
            default:
                return (FocusOwner.None, null);
        }
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
                control.Focus(Avalonia.Input.NavigationMethod.Directional);
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

    /// <summary>Sets the keyboard focus to the currently selected tree row.</summary>
    public void FocusTreeSelection()
    {
        var path = (string[])_state.SelectedPath.Clone();
        RestoreFocusToTree(path, Avalonia.Threading.DispatcherPriority.Loaded);
        RestoreFocusToTree(path, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void RestoreFocusToTree(string[] path, Avalonia.Threading.DispatcherPriority priority)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var window = GetWindow();
            if (window == null) return;
            FindFocusableTreeRow(window, path)?.Focus(Avalonia.Input.NavigationMethod.Directional);
        }, priority);
    }

    private static Avalonia.Controls.Control? FindFocusableTreeRow(Avalonia.Visual node, string[] path)
    {
        Avalonia.Controls.Control? best = null;
        int bestDepth = -1;
        FindFocusableTreeRowCore(node, path, 0, ref bestDepth, ref best);
        return best;
    }

    private static void FindFocusableTreeRowCore(Avalonia.Visual node, string[] path, int depth,
        ref int bestDepth, ref Avalonia.Controls.Control? best)
    {
        if (node is Avalonia.Controls.Control c &&
            c.DataContext is JsonEditorNode n &&
            n.Path.SequenceEqual(path) &&
            c.Focusable &&
            c.IsEffectivelyVisible &&
            depth > bestDepth)
        {
            bestDepth = depth;
            best = c;
        }

        foreach (var child in node.GetVisualChildren())
            FindFocusableTreeRowCore(child, path, depth + 1, ref bestDepth, ref best);
    }

    private void RefreshUIImpl(bool syncText)
    {
        var previousSuppress = _suppressTextSync;
        _suppressTextSync = true;
        try
        {
            RefreshUIImplCore(syncText);
        }
        finally
        {
            _suppressTextSync = previousSuppress;
        }
    }

    private void RefreshUIImplCore(bool syncText)
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
        ErrorItems = new ObservableCollection<ErrorListItem>(EditorLogic.BuildErrorItems(_state));
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
        NestedCtx = _state.NestedCtx;
        EditorHelpInfo = EditorLogic.GetHelpInfo(_state);
        ArrayHelpInfo = EditorLogic.GetArrayHelpInfo(_state);
        System.Diagnostics.Debug.WriteLine($"[RefreshUI] mode={CurrentEditorMode}");

        // Rebuild tree
        RefreshTree();

        // Reset all mode-specific content
        ObjectFields.Clear();
        CardItems.Clear();
        ScalarNode = new Jsonsui.Core.Models.JsonEditorNode { Label = "", Path = [], NodeType = "scalar" };
        ScalarRow = null;
        DetailArrayRow = null;
        DetailScalarText = "";
        TextEditorPathString = "";
        if (syncText)
        {
            _textParseTimer.Stop();
            HasTextParseError = false;
            TextParseError = "";
            TextContent = "";
        }

        // Build mode-specific content
        var mode = CurrentEditorMode;
        if (mode == EditorMode.Text)
        {
            TextEditorPathString = SelectedPathString;
            if (syncText)
            {
                var selected = EditorLogic.GetSelectedValue(_state);
                TextContent = JsonDocumentService.ToFormattedJson(selected ?? _state.Json);
            }
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
                else if (detailValue is JsonArray)
                {
                    DetailArrayRow = EditorLogic.BuildFieldRow(_state, detailPath);
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
            ScalarRow = EditorLogic.BuildScalarFieldRow(_state);
        }

        System.Diagnostics.Debug.WriteLine("[RefreshUI] END");
    }

    private void RefreshTree()
    {
        var root = EditorLogic.BuildTree(_state, FilterText);
        EditorLogic.MarkErrors(root, _state.Errors);
        TreeRootNodes.Clear();
        if (root.IsVisible)
            TreeRootNodes.Add(root);
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

    // --- Tree keyboard navigation ---

    [RelayCommand]
    private void MoveTreeSelection(int delta)
    {
        var root = TreeRootNodes.FirstOrDefault();
        if (root == null) return;

        var next = EditorLogic.GetNextVisible(root, _state.SelectedPath, delta);
        if (next == null) return;

        EditorLogic.SelectNode(_state, next);
        RefreshUI();
        FocusTreeSelection();
    }

    [RelayCommand]
    private void TreeHome()
    {
        var root = TreeRootNodes.FirstOrDefault();
        if (root == null) return;

        var first = EditorLogic.FlattenVisibleNavigable(root).FirstOrDefault();
        if (first == null) return;

        EditorLogic.SelectNode(_state, first);
        RefreshUI();
        FocusTreeSelection();
    }

    [RelayCommand]
    private void TreeEnd()
    {
        var root = TreeRootNodes.FirstOrDefault();
        if (root == null) return;

        var last = EditorLogic.GetLastVisibleNavigable(root);
        if (last == null) return;

        EditorLogic.SelectNode(_state, last);
        RefreshUI();
        FocusTreeSelection();
    }

    [RelayCommand]
    private void ExpandTree()
    {
        var root = TreeRootNodes.FirstOrDefault();
        if (root == null) return;

        var node = EditorLogic.FindNodeByPath(root, _state.SelectedPath);
        if (node == null) return;

        if (node.IsExpandable && !node.IsExpanded)
        {
            EditorLogic.ToggleExpand(_state, node);
            RefreshUI();
        }
        else if (node.IsExpanded)
        {
            var child = EditorLogic.GetFirstVisibleChild(node);
            if (child != null)
            {
                EditorLogic.SelectNode(_state, child);
                RefreshUI();
            }
        }

        FocusTreeSelection();
    }

    [RelayCommand]
    private void CollapseTree()
    {
        var root = TreeRootNodes.FirstOrDefault();
        if (root == null) return;

        var node = EditorLogic.FindNodeByPath(root, _state.SelectedPath);
        if (node == null) return;

        if (node.IsExpanded)
        {
            EditorLogic.ToggleExpand(_state, node);
            RefreshUI();
        }
        else
        {
            var parent = EditorLogic.GetVisibleParent(node);
            if (parent != null)
            {
                EditorLogic.SelectNode(_state, parent);
                RefreshUI();
            }
        }

        FocusTreeSelection();
    }

    [RelayCommand]
    private void FocusTreeFilter()
    {
        var box = GetWindow()?.FindControl<TextBox>("FilterBox");
        box?.Focus(Avalonia.Input.NavigationMethod.Directional);
        box?.SelectAll();
    }

    [RelayCommand]
    private void ActivateTreeNode(JsonEditorNode? node)
    {
        if (node == null) return;
        EditorLogic.SelectNode(_state, node);
        RefreshUI();
        FocusFirstEditorTarget();
    }

    /// <summary>Jumps from the tree to the first editable target of the editor area.</summary>
    public void FocusFirstEditorTarget()
    {
        switch (CurrentEditorMode)
        {
            case EditorMode.Object:
                if (ObjectFields.Count > 0)
                {
                    FocusEditorField(ObjectFields[0].Path);
                }
                else
                {
                    FocusNamedButton("AddPropertyButton");
                }
                break;

            case EditorMode.ArraySplit:
                if (CardItems.Count > 0)
                    FocusSelectedCard();
                else
                    FocusNamedButton("AddCardButton");
                break;

            case EditorMode.Scalar:
                if (ScalarRow != null)
                    FocusEditorField(ScalarRow.Path);
                break;

            case EditorMode.Text:
                FocusNamedControl<TextBox>("TextEditorBox");
                break;
        }
    }

    private void FocusEditorField(string[] path)
    {
        var copy = (string[])path.Clone();
        RestoreFocus(copy);
        RestoreFocus(copy, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void FocusNamedButton(string name)
        => FocusNamedControl<Button>(name);

    private void FocusNamedControl<T>(string name) where T : Avalonia.Controls.Control
    {
        void FocusOnce() => GetWindow()?.FindControl<T>(name)?.Focus(Avalonia.Input.NavigationMethod.Directional);
        Avalonia.Threading.Dispatcher.UIThread.Post(FocusOnce, Avalonia.Threading.DispatcherPriority.Loaded);
        Avalonia.Threading.Dispatcher.UIThread.Post(FocusOnce, Avalonia.Threading.DispatcherPriority.Background);
    }

    [RelayCommand]
    private void DrillInto(string[]? path)
    {
        if (path == null) return;
        EditorLogic.DrillInto(_state, path);
        RefreshUI();
        FocusFirstEditorTarget();
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

    public void NavigateToError(ErrorListItem item)
    {
        if (item == null) return;
        var error = new ValidationError { Path = item.Path };
        EditorLogic.NavigateToErrorContext(_state, error);
        RefreshUI();
        if (!item.IsMissing)
        {
            RestoreFocus(item.Path);
            RestoreFocus(item.Path, Avalonia.Threading.DispatcherPriority.Background);
        }
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

    public void AddSchemaProperty(PropertyCatalogItem item, string? requestedType = null)
    {
        if (item == null || _addPropertyTargetPath == null) return;

        JsonNode? initialValue = null;
        if (requestedType != null)
            initialValue = EditorLogic.CreatePrimitiveForType(requestedType);

        var result = EditorLogic.AddProperty(_state, _addPropertyTargetPath, item.Name, _undoRedo, initialValue);
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
        ErrorItems = new ObservableCollection<ErrorListItem>(EditorLogic.BuildErrorItems(_state));
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
        EditorHelpInfo = EditorLogic.GetHelpInfo(_state);
        ArrayHelpInfo = EditorLogic.GetArrayHelpInfo(_state);
        OnPropertyChanged(nameof(EditorHelpVisible));
        OnPropertyChanged(nameof(EditorHelpHasDescription));
        OnPropertyChanged(nameof(ArrayHelpVisible));
    }

    // --- Rename Property (inline, UI state only; mutation via EditorLogic.RenameProperty) ---

    public void BeginRename(FieldRow row)
    {
        if (row == null || row.IsReadOnly || row.Path.Length == 0) return;

        row.EditName = row.Key;
        row.RenameError = null;
        row.IsEditingName = true;
        row.NotifyPropertyChanged(nameof(FieldRow.EditName));
        row.NotifyPropertyChanged(nameof(FieldRow.RenameError));
        row.NotifyPropertyChanged(nameof(FieldRow.HasRenameError));
        row.NotifyPropertyChanged(nameof(FieldRow.IsEditingName));
    }

    public void CommitRename(FieldRow row)
    {
        if (row == null || !row.IsEditingName || row.IsReadOnly || row.Path.Length == 0) return;

        var newName = row.EditName?.Trim() ?? "";
        var objectPath = row.Path[..^1];
        var oldName = row.Path[^1];

        var result = EditorLogic.RenameProperty(_state, objectPath, oldName, newName, _undoRedo);
        if (!result.IsSuccess)
        {
            row.RenameError = result.Failure switch
            {
                RenamePropertyFailure.InvalidPropertyName => string.IsNullOrEmpty(newName)
                    ? Strings.Get("Rename_EmptyName")
                    : Strings.Get("Rename_InvalidName"),
                RenamePropertyFailure.PropertyAlreadyExists => Strings.Format("Rename_AlreadyExists", newName),
                RenamePropertyFailure.PropertyNotFound => Strings.Get("Rename_NotFound"),
                _ => Strings.Get("Rename_Failed")
            };
            row.NotifyPropertyChanged(nameof(FieldRow.RenameError));
            row.NotifyPropertyChanged(nameof(FieldRow.HasRenameError));
            return;
        }

        row.IsEditingName = false;
        row.RenameError = null;
        row.NotifyPropertyChanged(nameof(FieldRow.IsEditingName));
        row.NotifyPropertyChanged(nameof(FieldRow.RenameError));
        row.NotifyPropertyChanged(nameof(FieldRow.HasRenameError));

        var newPath = objectPath.Concat([newName]).ToArray();
        _state.FocusFieldPath = newPath;
        RefreshUI();
        RestoreFocus(newPath);
        RestoreFocus(newPath, Avalonia.Threading.DispatcherPriority.Background);
    }

    public void CancelRename(FieldRow row)
    {
        if (row == null || !row.IsEditingName) return;

        row.IsEditingName = false;
        row.EditName = null;
        row.RenameError = null;
        row.NotifyPropertyChanged(nameof(FieldRow.IsEditingName));
        row.NotifyPropertyChanged(nameof(FieldRow.EditName));
        row.NotifyPropertyChanged(nameof(FieldRow.RenameError));
        row.NotifyPropertyChanged(nameof(FieldRow.HasRenameError));
    }

    [RelayCommand]
    private void AddCard()
    {
        var (arrayPath, _) = EditorLogic.GetCardArrayContext(_state);
        if (JsonDocumentService.GetByPath(_state.Json, arrayPath) is not JsonArray) return;
        EditorLogic.AddArrayItem(_state, arrayPath, _undoRedo);
        RefreshUI();
        if (EditorLogic.GetDetailItemValue(_state) is JsonValue)
            FocusScalarDetailEditor();
    }

    // --- Nullable / Union type helpers ---

    /// <summary>
    /// Returns the non-null types of a field according to the schema (from JsonTypes or JsonType).
    /// Empty list = 0 non-null types. null = field has no schema.
    /// </summary>
    public List<string>? GetFieldJsonTypes(string[] path)
    {
        if (_state.ActiveSchema == null) return null;
        var prop = EditorLogic.ResolveSchemaProperty(_state.ActiveSchema, path);
        if (prop == null) return null;

        if (prop.JsonTypes != null)
            return prop.JsonTypes.Where(t => t != "null").Distinct().ToList();
        if (prop.JsonType != null)
            return [prop.JsonType];
        return [];
    }

    /// <summary>
    /// Returns the non-null types of a not-yet-created catalog property
    /// (from its SchemaProperty.JsonTypes or JsonType). Empty list = 0 non-null types.
    /// null = no schema type known.
    /// </summary>
    public List<string>? GetItemJsonTypes(PropertyCatalogItem item)
    {
        if (item.SchemaProperty == null) return null;
        var prop = item.SchemaProperty;

        if (prop.JsonTypes != null)
            return prop.JsonTypes.Where(t => t != "null").Distinct().ToList();
        if (prop.JsonType != null)
            return [prop.JsonType];
        return [];
    }

    /// <summary>Indicates whether the schema of this field allows null (IsNullable).</summary>
    public bool FieldAllowsNull(string[] path)
    {
        if (_state.ActiveSchema == null) return false;
        var prop = EditorLogic.ResolveSchemaProperty(_state.ActiveSchema, path);
        return prop?.IsNullable == true;
    }

    public bool FieldIsNull(string[] path)
    {
        var value = JsonDocumentService.GetByPath(_state.Json, path);
        // JSON null is represented in System.Text.Json.Nodes as a C# null reference
        // (GetByPath then returns null), not as a JsonValue with ValueKind.Null.
        return value == null || (value is JsonValue jv && jv.GetValueKind() == JsonValueKind.Null);
    }

    /// <summary>
    /// Sets a field's value to the requested (schema) type through the existing
    /// Core mutation/undo path (EditorLogic.SetValueAtPath). The UI is refreshed afterwards.
    /// </summary>
    public void SetFieldToType(string[] path, string type)
    {
        if (path.Length == 0) return;

        JsonNode? value = type switch
        {
            "object" => new JsonObject(),
            "array" => new JsonArray(),
            "boolean" => JsonValue.Create(false),
            "integer" => JsonValue.Create(0),
            "number" => JsonValue.Create(0.0),
            "string" => JsonValue.Create(""),
            // JSON null is a C# null reference in System.Text.Json.Nodes
            // (JsonNode.Parse("null") also returns null). SetValueAtPath/SetByPath
            // therefore set the property correctly to JSON null (the property is kept).
            "null" => null,
            _ => null
        };
        if (value == null && type != "null") return;

        EditorLogic.SetValueAtPath(_state, path, value, _undoRedo);

        if (type == "object")
        {
            _state.FocusFieldPath = path;
            DrillInto(path);
        }
        else if (type == "array")
        {
            _state.FocusFieldPath = path;
            DrillIntoArray(path);
        }
        else
        {
            _state.FocusFieldPath = path;
            RefreshUI();
            RestoreFocus(path);
            RestoreFocus(path, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void FocusScalarDetailEditor()
        => FocusScalarDetailEditor(Avalonia.Threading.DispatcherPriority.Loaded);

    private void FocusScalarDetailEditor(Avalonia.Threading.DispatcherPriority priority)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            GetWindow()?.FindControl<TextBox>("ScalarDetailTextBox")?.Focus(Avalonia.Input.NavigationMethod.Directional);
        }, priority);
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
    private void MoveCardSelection(int delta)
    {
        if (CardItems.Count == 0) return;

        var current = EditorLogic.GetCardArrayContext(_state).ActiveCardIndex ?? 0;
        var next = Math.Clamp(current + delta, 0, CardItems.Count - 1);
        if (next == current)
        {
            FocusSelectedCard();
            return;
        }

        EditorLogic.SelectCard(_state, next);
        RefreshUI();
        FocusSelectedCard();
    }

    /// <summary>Escape from a focused array card: step one level up when in a nested
    /// context (focus stays in the editor), otherwise move focus to the tree.</summary>
    [RelayCommand]
    private void EscapeFromCard()
    {
        if (_state.NestedCtx != null)
        {
            EditorLogic.ExitNestedArray(_state);
            RefreshUI();
            FocusFirstEditorTarget();
            return;
        }

        FocusTreeSelection();
    }

    /// <summary>Moves focus from the card Enter into the detail editor of the active element.</summary>
    public void FocusCardDetail()
    {
        var (arrayPath, index) = EditorLogic.GetCardArrayContext(_state);
        if (index == null) return;

        var detailPath = arrayPath.Concat([index.Value.ToString()]).ToArray();
        var value = JsonDocumentService.GetByPath(_state.Json, detailPath);

        if (value is JsonValue)
        {
            FocusScalarDetailEditor(Avalonia.Threading.DispatcherPriority.Loaded);
            FocusScalarDetailEditor(Avalonia.Threading.DispatcherPriority.Background);
            return;
        }

        if (value is JsonArray)
        {
            if (DetailArrayRow != null)
                FocusEditorField(DetailArrayRow.Path);
            return;
        }

        if (ObjectFields.Count > 0)
        {
            var path = (string[])ObjectFields[0].Path.Clone();
            RestoreFocus(path);
            RestoreFocus(path, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>Sets the keyboard focus to the currently selected array card.
    /// Without cards (empty array) the add button is focused.</summary>
    public void FocusSelectedCard()
    {
        var index = EditorLogic.GetCardArrayContext(_state).ActiveCardIndex;
        if (index == null)
        {
            FocusNamedButton("AddCardButton");
            return;
        }
        var target = index.Value;
        RestoreFocusToCard(target, Avalonia.Threading.DispatcherPriority.Loaded);
        RestoreFocusToCard(target, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void RestoreFocusToCard(int index, Avalonia.Threading.DispatcherPriority priority)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var window = GetWindow();
            if (window == null) return;
            FindFocusableCard(window, index)?.Focus(Avalonia.Input.NavigationMethod.Directional);
        }, priority);
    }

    private static Avalonia.Controls.Control? FindFocusableCard(Avalonia.Visual node, int index)
    {
        Avalonia.Controls.Control? best = null;
        int bestDepth = -1;
        FindFocusableCardCore(node, index, 0, ref bestDepth, ref best);
        return best;
    }

    private static void FindFocusableCardCore(Avalonia.Visual node, int index, int depth,
        ref int bestDepth, ref Avalonia.Controls.Control? best)
    {
        if (node is Avalonia.Controls.Control c &&
            c.Classes.Contains("CardItem") &&
            c.DataContext is CardItem card &&
            card.Index == index &&
            c.Focusable &&
            c.IsEffectivelyVisible &&
            depth > bestDepth)
        {
            bestDepth = depth;
            best = c;
        }

        foreach (var child in node.GetVisualChildren())
            FindFocusableCardCore(child, index, depth + 1, ref bestDepth, ref best);
    }

    [RelayCommand]
    private void DrillIntoArray(string[]? path)
    {
        if (path == null) return;
        EditorLogic.DrillIntoArray(_state, path);
        RefreshUI();
        FocusFirstEditorTarget();
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
            TextParseError = Strings.Format("Save_Error", ex.Message);
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
        if (TextMode && _textParseTimer.IsEnabled)
        {
            _textParseTimer.Stop();
            ApplyTextContent();
        }

        TextMode = !TextMode;
        RefreshUI();
    }

    [RelayCommand]
    private void OpenDiff()
    {
        Diff.Open(_state.OriginalJson, _state.Json);
    }

    [RelayCommand]
    private void CloseDiff()
    {
        Diff.Close();
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
        if (string.IsNullOrWhiteSpace(text)) return;

        var previousSuppress = _suppressTextSync;
        _suppressTextSync = true;
        try
        {
            TextContent = text;
        }
        finally
        {
            _suppressTextSync = previousSuppress;
        }

        ApplyTextContent();
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
            Title = Strings.Get("Dialog_OpenJson"),
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
                FilterText = "";
                DocumentFilename = file.Name;
                _currentFilePath = file.Path.LocalPath;
                _undoRedo.Clear();
                TryAutoLoadSchema(node, file.Path.LocalPath);
                EditorLogic.Validate(_state);
                RefreshUI();
                FocusTreeSelection();
            }
            catch (JsonException ex)
            {
                LoadError = Strings.Format("Load_InvalidJson", ex.Message);
            }
            catch (Exception ex)
            {
                LoadError = Strings.Format("Load_FileFailed", ex.Message);
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
            FilterText = "";
            DocumentFilename = System.IO.Path.GetFileName(filePath);
            _currentFilePath = filePath;
            _undoRedo.Clear();

            if (schemaPath != null)
                LoadSchemaFromPath(schemaPath);
            else
                TryAutoLoadSchema(node, filePath);

            EditorLogic.Validate(_state);
            RefreshUI();
            FocusTreeSelection();
        }
        catch (JsonException ex)
        {
            LoadError = Strings.Format("Load_InvalidJson", ex.Message);
        }
        catch (Exception ex)
        {
            LoadError = Strings.Format("Load_FileFailed", ex.Message);
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
            Title = Strings.Get("Dialog_OpenSchema"),
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
