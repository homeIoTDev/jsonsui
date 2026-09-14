using System.Text.Json.Nodes;

namespace Jsonsui.Core.Services;

public class UndoCommand
{
    public string[] Path { get; init; } = [];
    public JsonNode? OldValue { get; init; }
    public JsonNode? NewValue { get; init; }
    public string Action { get; init; } = "set"; // set, remove_array_item, add_array_item, add_property, remove_property, rename_property
    public int ArrayIndex { get; init; } = -1;
    public string PropertyName { get; init; } = "";

    /// <summary>Alter Property-Name, nur bei Action "rename_property" verwendet.</summary>
    public string OldPropertyName { get; init; } = "";
}

public class UndoRedoService
{
    private readonly Stack<UndoCommand> _undoStack = new();
    private readonly Stack<UndoCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void PushUndo(UndoCommand command)
    {
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public UndoCommand? PopUndo()
    {
        if (_undoStack.Count == 0) return null;
        var cmd = _undoStack.Pop();
        _redoStack.Push(cmd);
        return cmd;
    }

    public UndoCommand? PopRedo()
    {
        if (_redoStack.Count == 0) return null;
        var cmd = _redoStack.Pop();
        _undoStack.Push(cmd);
        return cmd;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    public JsonNode ApplyUndo(JsonNode document)
    {
        var cmd = PopUndo();
        if (cmd == null) return document;

        return cmd.Action switch
        {
            "set" => JsonDocumentService.SetByPath(document, cmd.Path, cmd.OldValue!),
            "remove_array_item" => JsonDocumentService.AddArrayItem(document, cmd.Path, cmd.OldValue!),
            "add_array_item" => JsonDocumentService.RemoveArrayItem(document, cmd.Path, cmd.ArrayIndex),
            "add_property" => JsonDocumentService.RemoveProperty(document, cmd.Path, cmd.PropertyName).Root,
            "remove_property" => JsonDocumentService.AddProperty(document, cmd.Path, cmd.PropertyName, cmd.OldValue!).Root,
            "rename_property" => JsonDocumentService.RenameProperty(document, cmd.Path, cmd.PropertyName, cmd.OldPropertyName).Root,
            _ => document
        };
    }

    public JsonNode ApplyRedo(JsonNode document)
    {
        var cmd = PopRedo();
        if (cmd == null) return document;

        return cmd.Action switch
        {
            "set" => JsonDocumentService.SetByPath(document, cmd.Path, cmd.NewValue!),
            "remove_array_item" => JsonDocumentService.RemoveArrayItem(document, cmd.Path, cmd.ArrayIndex),
            "add_array_item" => JsonDocumentService.AddArrayItem(document, cmd.Path, cmd.NewValue!),
            "add_property" => JsonDocumentService.AddProperty(document, cmd.Path, cmd.PropertyName, cmd.NewValue!).Root,
            "remove_property" => JsonDocumentService.RemoveProperty(document, cmd.Path, cmd.PropertyName).Root,
            "rename_property" => JsonDocumentService.RenameProperty(document, cmd.Path, cmd.OldPropertyName, cmd.PropertyName).Root,
            _ => document
        };
    }
}
