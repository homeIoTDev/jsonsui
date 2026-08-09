using System.Text.Json;
using System.Text.Json.Nodes;
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

    public static JsonEditorNode BuildTree(EditorState state)
    {
        var nodeByPathKey = new Dictionary<string, JsonEditorNode>();
        var root = BuildNode(state.Json, "root", [], 0, null, state.Expanded, nodeByPathKey);
        root.IsExpanded = true;
        root.IsSelected = state.SelectedPath.Length == 0;
        if (state.SelectedPath.Length > 0 && nodeByPathKey.TryGetValue(string.Join("/", state.SelectedPath), out var sel))
            sel.IsSelected = true;
        return root;
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
        if (value is JsonArray)
            state.NestedCtx = new NestedContext { ArrayPath = path, CardIndex = 0, Previous = state.NestedCtx };
        else
            state.NestedCtx = new NestedContext { ObjectPath = path, Previous = state.NestedCtx };
    }

    public static void DrillIntoArray(EditorState state, string[] path)
    {
        state.NestedCtx = new NestedContext { ArrayPath = path, CardIndex = 0, Previous = state.NestedCtx };
        state.FocusFieldPath = null;
    }

    public static void ExitNestedArray(EditorState state)
    {
        state.NestedCtx = state.NestedCtx?.Previous;
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

    public static void AddArrayItem(EditorState state, string[] arrayPath, UndoRedoService undoRedo)
    {
        var arr = JsonDocumentService.GetByPath(state.Json, arrayPath) as JsonArray;
        JsonNode? template;
        if (arr != null && arr.Count > 0)
            template = JsonDocumentService.CreateTemplate(arr[0]);
        else
            template = new JsonObject();

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
        ValidateNode(state.Json, [], errors);
        state.Errors = errors.ToArray();
    }

    private static void ValidateNode(JsonNode? node, string[] path, List<ValidationError> errors)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                var childPath = path.Concat([kvp.Key]).ToArray();
                if (kvp.Value is JsonValue val && val.GetValueKind() == JsonValueKind.Null)
                    errors.Add(new ValidationError { Path = childPath, Message = $"{kvp.Key} is null" });
                else if (kvp.Value is JsonValue sv && sv.TryGetValue<string>(out var str) && string.IsNullOrEmpty(str))
                    errors.Add(new ValidationError { Path = childPath, Message = $"{kvp.Key} is empty" });
                else
                    ValidateNode(kvp.Value, childPath, errors);
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
                ValidateNode(arr[i], path.Concat([i.ToString()]).ToArray(), errors);
        }
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

            var fieldType = kvp.Value switch
            {
                JsonObject => "object",
                JsonArray => "array",
                _ => schemaProp?.EnumValues is { Count: > 0 } ? "enum" : "scalar"
            };

            var row = new FieldRow
            {
                Key = kvp.Key,
                Path = fieldPath,
                FieldType = fieldType,
                IsRequired = schemaProp?.IsRequired ?? false,
                HasErrors = state.Errors.Any(e => e.PathString == string.Join(".", fieldPath)),
                EnumValues = schemaProp?.EnumValues
            };

            if (kvp.Value is JsonObject nestedObj)
                row.NestedObjectSummary = $"{{{nestedObj.Count} fields}}";
            else if (kvp.Value is JsonArray nestedArr)
                row.ArrayItemCount = $"{nestedArr.Count} items";
            else if (kvp.Value is JsonValue jv)
            {
                if (fieldType == "enum" && jv.TryGetValue<string>(out var sv))
                    row.ScalarValue = sv;
                else
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
            if (prop == null) break;
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
                var contextPath = focusPath[..^1];
                var contextProp = ResolveSchemaProperty(state.ActiveSchema, contextPath);
                if (contextProp != null && contextProp.JsonType != "array")
                {
                    info.ContextDescription = contextProp.Description;
                    info.ContextMeta = BuildMetaText(contextProp);
                }

                var fieldProp = ResolveSchemaProperty(state.ActiveSchema, focusPath);
                if (fieldProp != null && fieldProp.JsonType != "array")
                {
                    info.FieldDescription = fieldProp.Description;
                    info.FieldMeta = BuildMetaText(fieldProp);
                }
            }
            else
            {
                var contextProp = ResolveSchemaProperty(state.ActiveSchema, focusPath);
                if (contextProp != null && contextProp.JsonType != "array")
                {
                    info.ContextDescription = contextProp.Description;
                    info.ContextMeta = BuildMetaText(contextProp);
                }
                else if (focusPath.Length == 0)
                {
                    info.ContextDescription = state.ActiveSchema.Description;
                }
            }
        }

        return info;
    }

    private static string? BuildMetaText(SchemaProperty prop)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(prop.DefaultValue))
            parts.Add($"Default: {prop.DefaultValue}");

        if (prop.Minimum.HasValue && prop.Maximum.HasValue)
            parts.Add($"Bereich: {prop.Minimum} – {prop.Maximum}");
        else if (prop.Minimum.HasValue)
            parts.Add($"Min: {prop.Minimum}");
        else if (prop.Maximum.HasValue)
            parts.Add($"Max: {prop.Maximum}");

        if (prop.EnumValues is { Count: > 0 })
            parts.Add($"Werte: {string.Join(", ", prop.EnumValues)}");

        return parts.Count > 0 ? string.Join("  |  ", parts) : null;
    }

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
