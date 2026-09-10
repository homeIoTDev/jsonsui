using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ConfixJson.Core.Models;

namespace ConfixJson.Core.Services;

public static class EditorLogic
{
    public static void InitDocument(EditorState state, JsonNode node, string filename)
    {
        state.Json = node;
        state.OriginalJson = node.DeepClone();
        state.SelectedPath = [];
        state.Expanded = [];
        state.CardIndex = null;
        state.NestedCtx = null;
        state.FocusFieldPath = null;
        state.Errors = [];
    }

    // --- Tree Building ---

    public static JsonEditorNode BuildTree(EditorState state, string? filterText = null)
    {
        var nodeByPathKey = new Dictionary<string, JsonEditorNode>();
        var root = BuildNode(state.Json, "root", [], 0, null, state.Expanded, nodeByPathKey);
        root.IsExpanded = true;
        root.IsSelected = state.SelectedPath.Length == 0;
        if (state.SelectedPath.Length > 0 && nodeByPathKey.TryGetValue(string.Join("/", state.SelectedPath), out var sel))
            sel.IsSelected = true;
        ApplyFilter(root, filterText);
        return root;
    }

    // --- Filtering (affects only the visible tree/navigation structure) ---

    /// <summary>
    /// Markiert Tree-Knoten als sichtbar/unsichtbar anhand des Filtertextes.
    /// Sichtbar bleibt ein Knoten, wenn er selbst oder ein Nachkomme matcht.
    /// Array-Index-Labels ([0], [1], ...) matchen nicht selbst, werden aber
    /// rekursiv durchsucht. state.Expanded wird nicht verändert; Ancestors von
    /// Treffern werden nur auf dem frischen Baum temporär aufgeklappt.
    /// </summary>
    public static void ApplyFilter(JsonEditorNode root, string? filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            SetAllVisible(root, true);
            return;
        }

        FilterNode(root, filterText.Trim());
    }

    private static bool FilterNode(JsonEditorNode node, string filter)
    {
        var selfMatch = !IsArrayIndexNode(node) &&
                        node.Label.Contains(filter, StringComparison.OrdinalIgnoreCase);

        var childMatch = false;
        foreach (var child in node.Children)
            childMatch |= FilterNode(child, filter);

        node.IsVisible = selfMatch || childMatch;
        if (childMatch)
            node.IsExpanded = true;

        return node.IsVisible;
    }

    private static bool IsArrayIndexNode(JsonEditorNode node)
    {
        if (node.Parent?.NodeType != "array")
            return false;
        var label = node.Label;
        return label.Length >= 2 && label[0] == '[' && label[^1] == ']' &&
               int.TryParse(label.AsSpan(1, label.Length - 2), out _);
    }

    private static void SetAllVisible(JsonEditorNode node, bool visible)
    {
        node.IsVisible = visible;
        foreach (var child in node.Children)
            SetAllVisible(child, visible);
    }

    private static JsonEditorNode BuildNode(JsonNode? value, string label, string[] path, int depth,
        JsonEditorNode? parent, HashSet<string> expanded, Dictionary<string, JsonEditorNode> nodeByPathKey)
    {
        var node = new JsonEditorNode
        {
            Label = label,
            Path = path,
            Value = value,
            Depth = depth,
            Parent = parent
        };

        nodeByPathKey[node.PathKey] = node;

        if (value is JsonObject obj)
        {
            node.NodeType = path.Length == 0 ? "root" : "object";
            foreach (var kvp in obj)
            {
                var childPath = path.Concat([kvp.Key]).ToArray();
                var child = BuildNode(kvp.Value, kvp.Key, childPath, depth + 1, node, expanded, nodeByPathKey);
                child.IsExpanded = expanded.Contains(child.PathKey);
                node.Children.Add(child);
            }
        }
        else if (value is JsonArray arr)
        {
            node.NodeType = "array";
            for (int i = 0; i < arr.Count; i++)
            {
                var childPath = path.Concat([i.ToString()]).ToArray();
                var child = BuildNode(arr[i], $"[{i}]", childPath, depth + 1, node, expanded, nodeByPathKey);
                child.IsExpanded = expanded.Contains(child.PathKey);
                node.Children.Add(child);
            }
        }
        else
        {
            node.NodeType = "scalar";
        }

        return node;
    }

    // --- Mark Errors ---

    public static void MarkErrors(JsonEditorNode root, ValidationError[] errors)
    {
        MarkErrorsRecursive(root, errors);
    }

    private static void MarkErrorsRecursive(JsonEditorNode node, ValidationError[] errors)
    {
        var pathKey = string.Join("/", node.Path);
        node.HasErrors = errors.Any(e => string.Join("/", e.Path) == pathKey);
        foreach (var child in node.Children)
            MarkErrorsRecursive(child, errors);
    }

    // --- Selection ---

    public static void SelectNode(EditorState state, JsonEditorNode? node)
    {
        if (node == null) return;

        state.SelectedPath = node.Path;
        state.NestedCtx = null;
        state.CardIndex = null;
        state.FocusFieldPath = null;

        if (node.Value is JsonArray arr && arr.Count > 0)
            state.CardIndex = 0;
    }

    public static void ToggleExpand(EditorState state, JsonEditorNode? node)
    {
        if (node == null || !node.IsExpandable) return;

        node.IsExpanded = !node.IsExpanded;
        if (node.IsExpanded)
            state.Expanded.Add(node.PathKey);
        else
            state.Expanded.Remove(node.PathKey);
    }

    public static void SelectCard(EditorState state, int? index)
    {
        if (index == null) return;
        var ctx = state.NestedCtx;
        while (ctx != null)
        {
            if (ctx.ArrayPath != null)
            {
                ctx.CardIndex = index.Value;
                state.FocusFieldPath = null;
                return;
            }
            ctx = ctx.Previous;
        }
        state.CardIndex = index.Value;
        state.FocusFieldPath = null;
    }

    public static void DrillInto(EditorState state, string[] path)
    {
        var value = JsonDocumentService.GetByPath(state.Json, path);
        state.FocusFieldPath = null;
        var previousSelectedPath = state.SelectedPath.ToArray();
        if (value is JsonArray)
            state.NestedCtx = new NestedContext { ArrayPath = path, CardIndex = 0, PreviousSelectedPath = previousSelectedPath, Previous = state.NestedCtx };
        else
            state.NestedCtx = new NestedContext { ObjectPath = path, PreviousSelectedPath = previousSelectedPath, Previous = state.NestedCtx };
        SyncTreeSelection(state, path);
    }

    public static void DrillIntoArray(EditorState state, string[] path)
    {
        var previousSelectedPath = state.SelectedPath.ToArray();
        state.NestedCtx = new NestedContext { ArrayPath = path, CardIndex = 0, PreviousSelectedPath = previousSelectedPath, Previous = state.NestedCtx };
        state.FocusFieldPath = null;
        SyncTreeSelection(state, path);
    }

    private static void SyncTreeSelection(EditorState state, string[] path)
    {
        var np = GetNavigablePrefix(state, path);
        if (np.Length == 0)
            return;

        if (np.SequenceEqual(state.SelectedPath))
            return;

        state.SelectedPath = np;
        for (int i = 1; i < np.Length; i++)
            state.Expanded.Add(string.Join("/", np[..i]));
    }

    private static string[] GetNavigablePrefix(EditorState state, string[] path)
    {
        var navigable = new List<string>();
        JsonNode? current = state.Json;
        foreach (var segment in path)
        {
            if (current is JsonArray)
                break;
            navigable.Add(segment);
            current = current is JsonObject obj ? obj[segment] : null;
            if (current == null)
                break;
        }
        return navigable.ToArray();
    }

    public static void NavigateToErrorContext(EditorState state, ValidationError error)
    {
        if (error.Path.Length == 0)
            return;

        var container = error.Path[..^1];
        var navigable = GetNavigablePrefix(state, container);

        state.SelectedPath = navigable;
        for (int i = 1; i < navigable.Length; i++)
            state.Expanded.Add(string.Join("/", navigable[..i]));

        int? elementIndex = null;
        if (JsonDocumentService.GetByPath(state.Json, container) is JsonArray containerArr &&
            int.TryParse(error.Path[^1], out var idx) && idx >= 0 && idx < containerArr.Count)
            elementIndex = idx;

        state.NestedCtx = BuildContainerContext(state, container, navigable, elementIndex);
        state.CardIndex = elementIndex;
        state.FocusFieldPath = (string[])error.Path.Clone();
    }

    private static NestedContext? BuildContainerContext(EditorState state, string[] container, string[] navigable, int? elementIndex)
    {
        NestedContext? ctx = null;
        var path = new List<string>(navigable);
        JsonNode? current = JsonDocumentService.GetByPath(state.Json, navigable);
        for (int i = navigable.Length; i < container.Length; i++)
        {
            var segment = container[i];
            if (current is JsonArray arr)
            {
                if (!int.TryParse(segment, out var index) || index < 0 || index >= arr.Count)
                    break;
                ctx = new NestedContext { ArrayPath = path.ToArray(), CardIndex = index, Previous = ctx };
                path.Add(segment);
                current = arr[index];
            }
            else if (current is JsonObject obj)
            {
                path.Add(segment);
                ctx = new NestedContext { ObjectPath = path.ToArray(), Previous = ctx };
                current = obj[segment];
            }
            else
                break;
        }

        if (elementIndex != null &&
            JsonDocumentService.GetByPath(state.Json, container) is JsonArray)
        {
            ctx = new NestedContext { ArrayPath = (string[])container.Clone(), CardIndex = elementIndex, Previous = ctx };
        }
        return ctx;
    }

    public static void ExitNestedArray(EditorState state)
    {
        var previousSelectedPath = state.NestedCtx?.PreviousSelectedPath;
        state.NestedCtx = state.NestedCtx?.Previous;
        if (previousSelectedPath != null)
            state.SelectedPath = previousSelectedPath;
        state.FocusFieldPath = null;
    }

    public static void FocusField(EditorState state, string[]? path)
    {
        state.FocusFieldPath = path;
    }

    // --- Document Mutations ---

    public static void SetValueAtPath(EditorState state, string[] path, JsonNode? value, UndoRedoService undoRedo)
    {
        var oldValue = JsonDocumentService.GetByPath(state.Json, path);
        state.Json = JsonDocumentService.SetByPath(state.Json, path, value ?? JsonValue.Create<string?>(null)!);
        undoRedo.PushUndo(new UndoCommand
        {
            Path = path,
            OldValue = oldValue?.DeepClone(),
            NewValue = value?.DeepClone()
        });
        Validate(state);
    }

    public static AddPropertyResult AddProperty(EditorState state, string[] objectPath, string name,
        UndoRedoService undoRedo, JsonNode? initialValue = null)
    {
        var objectSchema = ResolveObjectSchema(state.ActiveSchema, objectPath);
        SchemaProperty? schemaProp = null;
        if (objectSchema != null)
            schemaProp = objectSchema.Properties.FirstOrDefault(p => p.Name == name);

        var value = DetermineInitialValue(schemaProp, initialValue);

        var result = JsonDocumentService.AddProperty(state.Json, objectPath, name, value);
        if (!result.IsSuccess)
            return result;

        state.Json = result.Root;
        undoRedo.PushUndo(new UndoCommand
        {
            Path = objectPath,
            PropertyName = name,
            NewValue = value?.DeepClone(),
            Action = "add_property"
        });
        Validate(state);
        return result;
    }

    public static RemovePropertyResult RemoveProperty(EditorState state, string[] objectPath, string name,
        UndoRedoService undoRedo)
    {
        var oldValue = JsonDocumentService.GetByPath(state.Json, objectPath.Concat([name]).ToArray());

        var result = JsonDocumentService.RemoveProperty(state.Json, objectPath, name);
        if (!result.IsSuccess)
            return result;

        state.Json = result.Root;
        undoRedo.PushUndo(new UndoCommand
        {
            Path = objectPath,
            PropertyName = name,
            OldValue = oldValue?.DeepClone(),
            Action = "remove_property"
        });
        Validate(state);
        return result;
    }

    public static RenamePropertyResult RenameProperty(EditorState state, string[] objectPath, string oldName, string newName,
        UndoRedoService undoRedo)
    {
        var result = JsonDocumentService.RenameProperty(state.Json, objectPath, oldName, newName);
        if (!result.IsSuccess)
            return result;

        state.Json = result.Root;
        undoRedo.PushUndo(new UndoCommand
        {
            Path = objectPath,
            OldPropertyName = oldName,
            PropertyName = newName,
            Action = "rename_property"
        });
        Validate(state);
        return result;
    }

    public static void AddArrayItem(EditorState state, string[] arrayPath, UndoRedoService undoRedo)
    {
        var arr = JsonDocumentService.GetByPath(state.Json, arrayPath) as JsonArray;
        JsonNode? template;
        if (arr != null && arr.Count > 0)
            template = JsonDocumentService.CreateTemplate(arr[0]);
        else
            template = CreateEmptyArrayItemTemplate(state.ActiveSchema, arrayPath);

        state.Json = JsonDocumentService.AddArrayItem(state.Json, arrayPath, template!);
        var newIndex = ((JsonArray?)JsonDocumentService.GetByPath(state.Json, arrayPath))?.Count - 1 ?? 0;
        undoRedo.PushUndo(new UndoCommand
        {
            Path = arrayPath,
            NewValue = template,
            Action = "add_array_item",
            ArrayIndex = newIndex
        });

        if (state.NestedCtx != null)
            state.NestedCtx.CardIndex = newIndex;
        else
            state.CardIndex = newIndex;

        Validate(state);
    }

    /// <summary>
    /// Erzeugt den Initialwert für ein neues Element eines leeren Arrays.
    /// Verwendet das Items-Schema (ArrayItemSchema), sofern vorhanden, damit bei
    /// primitiven Item-Typen (z. B. "string" bei ["string","null"]) kein JsonObject {}
    /// entsteht. Ohne Items-Schema bleibt das bisherige Fallback (JsonObject).
    /// Nullable Items erzeugen nicht automatisch null, sondern den Nicht-null-Initialwert.
    /// </summary>
    private static JsonNode CreateEmptyArrayItemTemplate(SchemaModel? schema, string[] arrayPath)
    {
        var itemSchema = schema != null
            ? ResolveSchemaProperty(schema, arrayPath)?.ArrayItemSchema
            : null;

        if (itemSchema == null)
            return new JsonObject();

        return CreatePrimitiveForType(itemSchema.JsonType ?? "string");
    }

    public static void RemoveArrayItem(EditorState state, string[] arrayPath, int index, UndoRedoService undoRedo)
    {
        var oldValue = (JsonDocumentService.GetByPath(state.Json, arrayPath) as JsonArray)?[index];
        state.Json = JsonDocumentService.RemoveArrayItem(state.Json, arrayPath, index);
        undoRedo.PushUndo(new UndoCommand
        {
            Path = arrayPath,
            OldValue = oldValue?.DeepClone(),
            Action = "remove_array_item",
            ArrayIndex = index
        });

        int activeCard = state.NestedCtx?.CardIndex ?? state.CardIndex ?? -1;
        if (activeCard >= index)
        {
            var arr = JsonDocumentService.GetByPath(state.Json, arrayPath) as JsonArray;
            int newCount = arr?.Count ?? 0;
            if (newCount == 0)
            {
                if (state.NestedCtx != null)
                    state.NestedCtx.CardIndex = null;
                else
                    state.CardIndex = null;
            }
            else if (activeCard >= newCount)
            {
                if (state.NestedCtx != null)
                    state.NestedCtx.CardIndex = newCount - 1;
                else
                    state.CardIndex = newCount - 1;
            }
        }

        Validate(state);
    }

    public static void Undo(EditorState state, UndoRedoService undoRedo)
    {
        state.Json = undoRedo.ApplyUndo(state.Json);
        Validate(state);
    }

    public static void Redo(EditorState state, UndoRedoService undoRedo)
    {
        state.Json = undoRedo.ApplyRedo(state.Json);
        Validate(state);
    }

    public static void ParseText(EditorState state, string text)
    {
        var node = JsonNode.Parse(text);
        if (node != null && state.SelectedPath.Length > 0)
        {
            state.Json = JsonDocumentService.SetByPath(state.Json, state.SelectedPath, node);
            Validate(state);
        }
    }

    // --- Validation ---

    public static void Validate(EditorState state)
    {
        var errors = new List<ValidationError>();
        ValidateNode(state.Json, [], errors, state.ActiveSchema);
        state.Errors = errors.ToArray();
    }

    private static void ValidateNode(JsonNode? node, string[] path, List<ValidationError> errors, SchemaModel? schema)
    {
        if (node is JsonObject obj)
        {
            var objSchema = ResolveObjectSchema(schema, path);
            if (objSchema != null)
            {
                foreach (var required in objSchema.Required)
                {
                    if (!obj.ContainsKey(required))
                        errors.Add(new ValidationError { Path = path.Concat([required]).ToArray(), Message = $"{required} is required" });
                }
            }

            foreach (var kvp in obj)
            {
                var childPath = path.Concat([kvp.Key]).ToArray();
                if (kvp.Value is JsonValue val && val.GetValueKind() == JsonValueKind.Null)
                    errors.Add(new ValidationError { Path = childPath, Message = $"{kvp.Key} is null" });
                else if (kvp.Value is JsonValue sv && sv.TryGetValue<string>(out var str) && string.IsNullOrEmpty(str))
                    errors.Add(new ValidationError { Path = childPath, Message = $"{kvp.Key} is empty" });
                else if (kvp.Value is JsonObject or JsonArray)
                    ValidateNode(kvp.Value, childPath, errors, schema);
                else
                    ValidateLeaf(kvp.Value, childPath, errors, schema);
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
                ValidateNode(arr[i], path.Concat([i.ToString()]).ToArray(), errors, schema);
        }
    }

    private static void ValidateLeaf(JsonNode? node, string[] path, List<ValidationError> errors, SchemaModel? schema)
    {
        if (schema == null) return;
        var prop = ResolveSchemaProperty(schema, path);
        if (prop == null) return;

        var name = path.Length > 0 ? path[^1] : "value";

        if (prop.Const != null)
        {
            var preview = JsonDocumentService.GetScalarPreview(node, int.MaxValue);
            if (preview != prop.Const)
                errors.Add(new ValidationError { Path = path, Message = $"{name} must equal {prop.Const}" });
        }

        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s))
            {
                if (prop.Pattern != null)
                {
                    var matches = false;
                    try { matches = Regex.IsMatch(s, prop.Pattern); }
                    catch (ArgumentException) { matches = true; }
                    if (!matches)
                        errors.Add(new ValidationError { Path = path, Message = $"{name} must match pattern {prop.Pattern}" });
                }
                if (prop.MinLength.HasValue && s.Length < prop.MinLength.Value)
                    errors.Add(new ValidationError { Path = path, Message = $"{name} must be at least {prop.MinLength.Value} characters" });
                if (prop.MaxLength.HasValue && s.Length > prop.MaxLength.Value)
                    errors.Add(new ValidationError { Path = path, Message = $"{name} must be at most {prop.MaxLength.Value} characters" });
            }
            else if (TryGetNumber(jv, out var num))
            {
                if (prop.Minimum.HasValue && num < (decimal)prop.Minimum.Value)
                    errors.Add(new ValidationError { Path = path, Message = $"{name} must be >= {prop.Minimum.Value}" });
                if (prop.Maximum.HasValue && num > (decimal)prop.Maximum.Value)
                    errors.Add(new ValidationError { Path = path, Message = $"{name} must be <= {prop.Maximum.Value}" });
            }
        }
    }

    private static SchemaModel? ResolveObjectSchema(SchemaModel? schema, string[] path)
    {
        if (schema == null) return null;
        if (path.Length == 0) return schema;
        var prop = ResolveSchemaProperty(schema, path);
        return prop?.ObjectSchema ?? prop?.ArrayItemSchema?.ObjectSchema;
    }

    // --- Property Catalog ---

    public static PropertyCatalog BuildPropertyCatalog(EditorState state, string[] objectPath)
    {
        var catalog = new PropertyCatalog();
        var obj = JsonDocumentService.GetByPath(state.Json, objectPath) as JsonObject;

        var objectSchema = ResolveObjectSchema(state.ActiveSchema, objectPath);
        catalog.SchemaAvailable = objectSchema != null;
        catalog.AdditionalPropertiesAllowed = objectSchema?.AdditionalPropertiesAllowed;
        catalog.AdditionalPropertiesSchema = objectSchema?.AdditionalPropertiesSchema;
        catalog.CanAddCustomProperty = ComputeCanAddCustomProperty(catalog);

        if (objectSchema == null)
            return catalog;

        foreach (var schemaProp in objectSchema.Properties)
        {
            if (obj?.ContainsKey(schemaProp.Name) == true)
                continue;

            catalog.Items.Add(new PropertyCatalogItem
            {
                Name = schemaProp.Name,
                Type = schemaProp.JsonType,
                IsRequired = schemaProp.IsRequired,
                IsAlreadyPresent = false,
                IsCustom = false,
                DefaultValue = schemaProp.DefaultValue,
                ConstValue = schemaProp.Const,
                IsReadOnly = schemaProp.IsReadOnly,
                IsDeprecated = schemaProp.IsDeprecated,
                Description = schemaProp.Description,
                Comment = schemaProp.Comment,
                EnumValues = schemaProp.EnumValues,
                CanAdd = true,
                SchemaProperty = schemaProp
            });
        }

        return catalog;
    }

    private static bool ComputeCanAddCustomProperty(PropertyCatalog catalog)
    {
        if (!catalog.SchemaAvailable)
            return true;
        if (catalog.AdditionalPropertiesSchema != null)
            return true;
        return catalog.AdditionalPropertiesAllowed != false;
    }

    // --- Property Initial Values ---

    private static JsonNode? DetermineInitialValue(SchemaProperty? schemaProp, JsonNode? explicitValue)
    {
        if (explicitValue != null)
            return explicitValue.DeepClone();

        if (schemaProp == null)
            return JsonValue.Create<string>("")!;

        // 0 Nicht-null-Typen (z. B. ["null"]): Property als JSON-null anlegen.
        if (schemaProp.JsonTypes is { Count: 0 })
            return null;

        if (schemaProp.Const != null)
        {
            var node = CreateNodeFromSchemaString(schemaProp.JsonType!, schemaProp.Const);
            if (node != null) return node;
        }

        if (schemaProp.DefaultValue != null)
        {
            var node = CreateNodeFromSchemaString(schemaProp.JsonType!, schemaProp.DefaultValue);
            if (node != null) return node;
        }

        if (schemaProp.EnumValues is { Count: > 0 })
        {
            var node = CreateNodeFromSchemaString(schemaProp.JsonType!, schemaProp.EnumValues[0]);
            if (node != null) return node;
        }

        return CreatePrimitiveForType(schemaProp.JsonType!);
    }

    private static JsonNode? CreateNodeFromSchemaString(string jsonType, string text)
    {
        return jsonType switch
        {
            "string" => JsonValue.Create(text),
            "integer" => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)
                ? JsonValue.Create(l)
                : JsonValue.Create(0L),
            "number" => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? JsonValue.Create(d)
                : JsonValue.Create(0.0),
            "boolean" => bool.TryParse(text, out var b) ? JsonValue.Create(b) : JsonValue.Create(false),
            "object" or "array" => TryParseJsonNode(text),
            _ => TryParseJsonNode(text)
        };
    }

    private static JsonNode? TryParseJsonNode(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static JsonNode CreatePrimitiveForType(string jsonType) => jsonType switch
    {
        "string" => JsonValue.Create<string>("")!,
        "integer" => JsonValue.Create(0)!,
        "number" => JsonValue.Create(0.0)!,
        "boolean" => JsonValue.Create(false)!,
        "object" => new JsonObject(),
        "array" => new JsonArray(),
        _ => JsonValue.Create<string>("")!
    };

    private static bool TryGetNumber(JsonValue jv, out decimal value)
    {
        if (jv.TryGetValue<int>(out var iv)) { value = iv; return true; }
        if (jv.TryGetValue<long>(out var lv)) { value = lv; return true; }
        if (jv.TryGetValue<double>(out var dv)) { value = (decimal)dv; return true; }
        if (jv.TryGetValue<decimal>(out var mv)) { value = mv; return true; }
        value = 0;
        return false;
    }

    // --- Derived Values ---

    public static JsonNode? GetSelectedValue(EditorState state)
        => JsonDocumentService.GetByPath(state.Json, state.SelectedPath);

    public static string GetSelectedPathString(EditorState state)
        => string.Join(" / ", state.SelectedPath);

    public static string[] GetEffectiveEditorPath(EditorState state)
    {
        if (state.NestedCtx?.ObjectPath != null)
            return state.NestedCtx.ObjectPath;
        var detailPath = GetDetailItemPath(state);
        if (detailPath != null)
            return detailPath;
        return state.SelectedPath;
    }

    public static string GetEffectivePathString(EditorState state)
        => string.Join(" / ", GetEffectiveEditorPath(state));

    private static (string[]?, int?) FindArrayInStack(EditorState state)
    {
        var ctx = state.NestedCtx;
        while (ctx != null)
        {
            if (ctx.ArrayPath != null)
                return (ctx.ArrayPath, ctx.CardIndex);
            ctx = ctx.Previous;
        }
        return (null, null);
    }

    public static (string[] CardArrayPath, int? ActiveCardIndex) GetCardArrayContext(EditorState state)
    {
        var (arrayPath, cardIndex) = FindArrayInStack(state);
        if (arrayPath != null)
            return (arrayPath, cardIndex);
        var selected = GetSelectedValue(state);
        if (selected is JsonArray)
            return (state.SelectedPath, state.CardIndex);
        return ([], null);
    }

    public static string? GetActiveArrayPath(EditorState state)
    {
        var (arrayPath, _) = FindArrayInStack(state);
        return arrayPath != null ? string.Join("/", arrayPath) : null;
    }

    public static string GetCardArrayPathString(EditorState state)
        => string.Join(" / ", GetCardArrayContext(state).CardArrayPath);

    public static string[]? GetDetailItemPath(EditorState state)
    {
        var (cardArrayPath, activeCardIndex) = GetCardArrayContext(state);
        if (activeCardIndex == null) return null;
        return cardArrayPath.Concat([activeCardIndex.Value.ToString()]).ToArray();
    }

    public static JsonNode? GetDetailItemValue(EditorState state)
    {
        var itemPath = GetDetailItemPath(state);
        return itemPath != null ? JsonDocumentService.GetByPath(state.Json, itemPath) : null;
    }

    public static string GetDetailPathString(EditorState state)
    {
        var itemPath = GetDetailItemPath(state);
        return itemPath != null ? string.Join(" / ", itemPath) : "";
    }

    public static EditorMode GetEditorMode(EditorState state, bool textMode)
    {
        if (textMode)
            return EditorMode.Text;

        if (state.NestedCtx?.ObjectPath != null)
            return EditorMode.Object;

        var selected = GetSelectedValue(state);
        if ((selected is JsonArray arr && arr.Count > 0) || state.NestedCtx != null)
            return EditorMode.ArraySplit;
        if (selected is JsonObject)
            return EditorMode.Object;
        if (selected is JsonValue)
            return EditorMode.Scalar;
        return EditorMode.Empty;
    }

    // --- Field / Card Building ---

    public static List<FieldRow> BuildObjectFields(EditorState state, string[] objectPath)
    {
        var rows = new List<FieldRow>();
        var obj = JsonDocumentService.GetByPath(state.Json, objectPath) as JsonObject;
        if (obj == null) return rows;

        foreach (var kvp in obj)
        {
            var fieldPath = objectPath.Concat([kvp.Key]).ToArray();

            SchemaProperty? schemaProp = null;
            if (state.ActiveSchema != null)
                schemaProp = ResolveSchemaProperty(state.ActiveSchema, fieldPath);

            var fieldType = DetermineFieldType(kvp.Value, schemaProp);

            var row = new FieldRow
            {
                Key = kvp.Key,
                Path = fieldPath,
                FieldType = fieldType,
                IsRequired = schemaProp?.IsRequired ?? false,
                IsReadOnly = schemaProp?.IsReadOnly ?? false,
                IsDeprecated = schemaProp?.IsDeprecated ?? false,
                DefaultValue = schemaProp?.DefaultValue,
                Comment = schemaProp?.Comment,
                HasErrors = state.Errors.Any(e => e.PathString == string.Join(".", fieldPath)),
                EnumValues = schemaProp?.EnumValues,
                MinValue = schemaProp?.Minimum != null ? (decimal)schemaProp.Minimum.Value : decimal.MinValue,
                MaxValue = schemaProp?.Maximum != null ? (decimal)schemaProp.Maximum.Value : decimal.MaxValue
            };

            if (kvp.Value is JsonObject nestedObj)
                row.NestedObjectSummary = $"{{{nestedObj.Count} fields}}";
            else if (kvp.Value is JsonArray nestedArr)
                row.ArrayItemCount = $"{nestedArr.Count} items";
            else if (kvp.Value is JsonValue jv)
            {
                if (fieldType == "enum" && jv.TryGetValue<string>(out var sv))
                    row.ScalarValue = sv;
                else if (fieldType == "boolean" && jv.TryGetValue<bool>(out var bv))
                    row.BoolValue = bv;
                else if (fieldType is "integer" or "number")
                {
                    var extracted = ExtractNumericValue(jv);
                    row.NumericValue = extracted;
                    row.OriginalNumericValue = extracted;
                    System.Diagnostics.Debug.WriteLine($"[BuildNumeric] Field={kvp.Key} extracted={extracted} jv.ToJsonString()={jv.ToJsonString()}");
                }
                else if (fieldType is "date" or "time" or "date-time")
                {
                    var str = jv.TryGetValue<string>(out var s) ? s : null;
                    TemporalValue.Apply(row, fieldType, str);
                }
                else if (fieldType != "null")
                    row.ScalarValue = JsonDocumentService.GetScalarPreview(jv, 200);
            }

            rows.Add(row);
        }
        return rows;
    }

    public static List<CardItem> BuildCardItems(EditorState state, string[] arrayPath)
    {
        var cards = new List<CardItem>();
        var arr = JsonDocumentService.GetByPath(state.Json, arrayPath) as JsonArray;
        if (arr == null) return cards;

        var (_, stackCardIndex) = FindArrayInStack(state);
        int activeIndex = (stackCardIndex ?? state.CardIndex) ?? -1;

        for (int i = 0; i < arr.Count; i++)
        {
            var itemPath = arrayPath.Concat([i.ToString()]).ToArray();
            cards.Add(new CardItem
            {
                Index = i,
                ItemPath = itemPath,
                Preview = BuildCardPreview(arr[i]),
                HasErrors = state.Errors.Any(e => e.PathString == string.Join(".", itemPath)),
                IsSelected = i == activeIndex
            });
        }
        return cards;
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

    public static JsonEditorNode? BuildScalarNode(EditorState state, string[] path, JsonNode? value)
    {
        if (value is not JsonValue) return null;
        return new JsonEditorNode
        {
            Label = path.Length > 0 ? path[^1] : "value",
            Path = path,
            Value = value,
            NodeType = "scalar",
            HasErrors = state.Errors.Any(e => e.PathString == string.Join(".", path))
        };
    }

    // --- Error List & Navigation ---

    public static List<ErrorListItem> BuildErrorItems(EditorState state)
    {
        var items = new List<ErrorListItem>();
        foreach (var error in state.Errors)
        {
            items.Add(new ErrorListItem
            {
                Path = (string[])error.Path.Clone(),
                DisplayPath = FormatErrorPath(state.Json, error.Path),
                Message = error.Message,
                IsMissing = IsErrorTargetMissing(state, error)
            });
        }
        return items;
    }

    public static bool IsErrorTargetMissing(EditorState state, ValidationError error)
    {
        if (error.Path.Length == 0) return false;
        var container = JsonDocumentService.GetByPath(state.Json, error.Path[..^1]);
        return container is JsonObject obj && !obj.ContainsKey(error.Path[^1]);
    }

    public static string FormatErrorPath(JsonNode root, string[] path)
    {
        var sb = new System.Text.StringBuilder();
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is JsonArray)
            {
                sb.Append('[').Append(segment).Append(']');
            }
            else
            {
                if (sb.Length > 0)
                    sb.Append('.');
                sb.Append(segment);
            }
            current = current switch
            {
                JsonObject obj => obj[segment],
                JsonArray arr when int.TryParse(segment, out var idx) && idx >= 0 && idx < arr.Count => arr[idx],
                _ => null
            };
            if (current == null)
                break;
        }
        return sb.ToString();
    }

    // --- Schema Resolution ---

    public static SchemaProperty? ResolveSchemaProperty(SchemaModel schema, string[] jsonPath)
    {
        var currentModel = schema;
        SchemaProperty? result = null;

        for (int i = 0; i < jsonPath.Length; i++)
        {
            var segment = jsonPath[i];

            if (int.TryParse(segment, out _))
            {
                if (result?.ArrayItemSchema?.ObjectSchema != null)
                    currentModel = result.ArrayItemSchema.ObjectSchema;
                continue;
            }

            var prop = currentModel.Properties.FirstOrDefault(p => p.Name == segment);
            if (prop == null) return null;
            result = prop;

            if (prop.ObjectSchema != null)
                currentModel = prop.ObjectSchema;
            else if (prop.ArrayItemSchema?.ObjectSchema != null)
                currentModel = prop.ArrayItemSchema.ObjectSchema;
        }

        if (result != null && jsonPath.Length > 0 && int.TryParse(jsonPath[^1], out _))
            result = result.ArrayItemSchema ?? result;

        return result;
    }

    // --- Help Info ---

    public static SchemaFieldInfo GetHelpInfo(EditorState state)
    {
        var focusPath = state.FocusFieldPath ?? GetEffectiveEditorPath(state);
        var pathStr = string.Join(" / ", focusPath);
        var errors = state.Errors.Where(e => string.Join(".", e.Path) == string.Join(".", focusPath)).ToArray();

        var info = new SchemaFieldInfo
        {
            PathString = pathStr,
            HasValidationErrors = errors.Length > 0,
            ErrorMessages = errors.Length > 0 ? string.Join("; ", errors.Select(e => e.Message)) : null
        };

        if (state.ActiveSchema != null)
        {
            bool hasFieldFocus = state.FocusFieldPath != null;

            if (hasFieldFocus && focusPath.Length > 0)
            {
                var contextPath = GetContextPath(focusPath);
                var contextProp = ResolveSchemaProperty(state.ActiveSchema, contextPath);
                if (contextProp != null)
                {
                    info.ContextDescription = contextProp.Description;
                    info.ContextMetaItems.AddRange(BuildMetaItems(contextProp));
                }

                var fieldProp = ResolveSchemaProperty(state.ActiveSchema, focusPath);
                if (fieldProp != null && fieldProp.JsonType != "array")
                {
                    info.FieldDescription = fieldProp.Description;
                    info.FieldMetaItems.AddRange(BuildMetaItems(fieldProp));
                    info.IsRequired = fieldProp.IsRequired;
                    info.IsDeprecated = fieldProp.IsDeprecated;
                    info.IsReadOnly = fieldProp.IsReadOnly;
                    info.Comment = fieldProp.Comment;
                    info.DefaultValue = fieldProp.DefaultValue;
                }
            }
            else
            {
                var contextProp = ResolveSchemaProperty(state.ActiveSchema, focusPath);
                if (contextProp != null && contextProp.JsonType != "array")
                {
                    info.ContextDescription = contextProp.Description;
                    info.ContextMetaItems.AddRange(BuildMetaItems(contextProp));
                    info.IsRequired = contextProp.IsRequired;
                    info.IsDeprecated = contextProp.IsDeprecated;
                    info.IsReadOnly = contextProp.IsReadOnly;
                    info.Comment = contextProp.Comment;
                    info.DefaultValue = contextProp.DefaultValue;
                }
                else if (focusPath.Length == 0)
                {
                    info.ContextDescription = state.ActiveSchema.Description;
                }
            }
        }

        return info;
    }

    private static string[] GetContextPath(string[] focusPath)
    {
        var context = focusPath[..^1];
        while (context.Length > 0 && int.TryParse(context[^1], out _))
            context = context[..^1];
        return context;
    }

    private static List<MetaItem> BuildMetaItems(SchemaProperty prop)
    {
        var items = new List<MetaItem>();

        if (!string.IsNullOrEmpty(prop.DefaultValue))
            items.Add(new MetaItem { Label = "default", Value = prop.DefaultValue });

        if (prop.Minimum.HasValue)
            items.Add(new MetaItem { Label = "minimum", Value = prop.Minimum.Value.ToString(CultureInfo.InvariantCulture) });

        if (prop.Maximum.HasValue)
            items.Add(new MetaItem { Label = "maximum", Value = prop.Maximum.Value.ToString(CultureInfo.InvariantCulture) });

        if (prop.MinLength.HasValue)
            items.Add(new MetaItem { Label = "minLength", Value = prop.MinLength.Value.ToString() });

        if (prop.MaxLength.HasValue)
            items.Add(new MetaItem { Label = "maxLength", Value = prop.MaxLength.Value.ToString() });

        if (!string.IsNullOrEmpty(prop.Pattern))
            items.Add(new MetaItem { Label = "pattern", Value = prop.Pattern });

        if (prop.Const != null)
            items.Add(new MetaItem { Label = "const", Value = prop.Const });

        if (prop.EnumValues is { Count: > 0 })
            items.Add(new MetaItem { Label = "enum", Value = string.Join(", ", prop.EnumValues) });

        return items;
    }

    private static decimal ExtractNumericValue(JsonValue jv)
    {
        if (jv.TryGetValue<int>(out var iv)) return iv;
        if (jv.TryGetValue<long>(out var lv)) return lv;
        if (jv.TryGetValue<double>(out var dv)) return (decimal)dv;
        if (jv.TryGetValue<decimal>(out var mv)) return mv;
        return 0;
    }

    private static string DetermineFieldType(JsonNode? value, SchemaProperty? schemaProp)
    {
        if (value is JsonObject) return "object";
        if (value is JsonArray) return "array";

        // Null-Wert bei vorhandenen JSON-Daten: als nullable Typ anzeigen, damit der
        // tatsächliche null-Wert sichtbar bleibt (unabhängig von der Typ-Anzahl im Schema).
        // (JSON-null wird von JsonNode als null-Referenz geliefert.)
        if (value == null &&
            schemaProp != null &&
            schemaProp.IsNullable)
        {
            return "nullable";
        }

        // Schema-basierte Sonderfälle, die unabhängig vom konkreten Wert gelten.
        if (schemaProp?.EnumValues is { Count: > 0 }) return "enum";

        // Bei einem vorhandenen, nicht-null JSON-Wert bestimmt der tatsächliche
        // JsonValueKind die UI-Control-Wahl. Bei Union-Typen wie ["string","boolean","null"]
        // ist schemaProp.JsonType nur der erste Nicht-null-Typ ("string"); ein tatsächlicher
        // Boolean-Wert false muss deshalb als "boolean" (CheckBox) erkannt werden.
        if (value is JsonValue jv)
        {
            return jv.GetValueKind() switch
            {
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Number => IsIntegralNumber(jv) ? "integer" : "number",
                JsonValueKind.String => schemaProp?.Format switch
                {
                    "date" => "date",
                    "time" => "time",
                    "date-time" => "date-time",
                    _ => "scalar"
                },
                _ => "scalar"
            };
        }

        // Schema-basierte Entscheidung, wenn kein konkreter JSON-Wert vorliegt.
        if (schemaProp != null)
        {
            if (schemaProp.JsonType == "string")
            {
                if (schemaProp.Format == "date") return "date";
                if (schemaProp.Format == "time") return "time";
                if (schemaProp.Format == "date-time") return "date-time";
            }
            return schemaProp.JsonType switch
            {
                "boolean" => "boolean",
                "integer" or "number" => schemaProp.JsonType!,
                "object" => "object",
                "array" => "array",
                _ => "scalar"
            };
        }

        return "scalar";
    }

    private static bool IsIntegralNumber(JsonValue jv)
        => jv.TryGetValue<int>(out _) || jv.TryGetValue<long>(out _);

    public static SchemaFieldInfo GetArrayHelpInfo(EditorState state)
    {
        var (cardArrayPath, _) = GetCardArrayContext(state);
        var focusPath = cardArrayPath.Length > 0 ? cardArrayPath : GetEffectiveEditorPath(state);

        System.Diagnostics.Debug.WriteLine($"[ArrayHelpInfo] ActiveSchema={(state.ActiveSchema != null ? "present" : "NULL")} path={string.Join("/", focusPath)}");

        return BuildHelpInfo(state, focusPath);
    }

    private static SchemaFieldInfo BuildHelpInfo(EditorState state, string[] focusPath)
    {
        var pathStr = string.Join(" / ", focusPath);
        var errors = state.Errors.Where(e => string.Join(".", e.Path) == string.Join(".", focusPath)).ToArray();

        var info = new SchemaFieldInfo
        {
            PathString = pathStr,
            HasValidationErrors = errors.Length > 0,
            ErrorMessages = errors.Length > 0 ? string.Join("; ", errors.Select(e => e.Message)) : null
        };

        if (state.ActiveSchema != null)
        {
            var schemaProp = ResolveSchemaProperty(state.ActiveSchema, focusPath);
            if (schemaProp != null)
            {
                info.Description = schemaProp.Description;
                info.DefaultValue = schemaProp.DefaultValue;
                info.Minimum = schemaProp.Minimum;
                info.Maximum = schemaProp.Maximum;
                info.MinLength = schemaProp.MinLength;
                info.MaxLength = schemaProp.MaxLength;
                info.Pattern = schemaProp.Pattern;
                info.Const = schemaProp.Const;
                info.IsRequired = schemaProp.IsRequired;
                info.IsDeprecated = schemaProp.IsDeprecated;
                info.IsReadOnly = schemaProp.IsReadOnly;
                info.Comment = schemaProp.Comment;
                info.FieldMetaItems.AddRange(BuildMetaItems(schemaProp));

                System.Diagnostics.Debug.WriteLine(
                    $"[BuildHelpInfo] schemaProp.Name={schemaProp.Name} JsonType={schemaProp.JsonType} " +
                    $"Description='{schemaProp.Description}' => info.Description='{info.Description}'");
            }
            else if (focusPath.Length == 0)
            {
                info.Description = state.ActiveSchema.Description;
                System.Diagnostics.Debug.WriteLine(
                    $"[BuildHelpInfo] root path => info.Description='{info.Description}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BuildHelpInfo] no schemaProp found for path={string.Join("/", focusPath)}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[BuildHelpInfo] ActiveSchema is NULL");
        }

        return info;
    }

    // --- Save / Load ---

    public static void SaveDocument(string filePath, JsonNode document)
    {
        JsonFileService.SaveToFile(filePath, document);
    }

    public static JsonNode LoadDocument(string filePath)
    {
        return JsonFileService.LoadFromFile(filePath);
    }

    public static JsonNode LoadDocumentFromStream(Stream stream)
    {
        return JsonFileService.LoadFromStream(stream);
    }

    // --- Diff ---

    public static List<JsonDiffLine> GetDiffLines(EditorState state)
    {
        var original = JsonDocumentService.ToFormattedJson(state.OriginalJson);
        var current = JsonDocumentService.ToFormattedJson(state.Json);
        return JsonDiffService.ComputeDiff(original, current);
    }

    // --- Change Field ---

    public static JsonNode? ConvertToJsonNode(object? value)
    {
        return value switch
        {
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            double d => JsonValue.Create(d),
            long l => JsonValue.Create(l),
            _ => null
        };
    }
}
