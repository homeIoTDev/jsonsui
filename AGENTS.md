# Jsonsui Development Guide

> **Always read `ARCHITECTURE_OVERVIEW.md` first** — it defines the project architecture, design decisions, and the strict separation between Core (UI-independent) and UI layers.

## Project Structure

- `src/Jsonsui.Core/` - UI-independent logic: models, services, document CRUD, validation, diff, undo/redo, file I/O
- `src/Jsonsui.UI/` - Avalonia desktop UI application (MVVM with CommunityToolkit.Mvvm)
- `Jsonsui.slnx` - .NET solution file

The UI project references the Core library. **Core must never reference UI.**

## Key Documentation

- **`ARCHITECTURE_OVERVIEW.md`** — Architecture decisions, requirements, Core/UI boundary definition
- **`ui.intent.json`** — Technology-neutral UI specification (layout, panels, widgets, design tokens)

## Entry Points

**Desktop App:** `dotnet run --project src/Jsonsui.UI/Jsonsui.UI.csproj`
**Core Library:** Use `src/Jsonsui.Core` as package reference

## Build & Run

1. Build solution: `dotnet build Jsonsui.slnx`
2. Run desktop app: `dotnet run --project src/Jsonsui.UI/Jsonsui.UI.csproj`

## Architecture: Core vs UI boundary

**Core (`.Core`)** — Zero UI dependencies. Contains:
- JSON document CRUD (`JsonDocumentService`)
- File I/O (`JsonFileService`)
- Diff computation (`JsonDiffService`)
- Undo/Redo (`UndoRedoService`)
- Schema parsing & validation (`SchemaParser`, `SchemaValidator`, `SchemaLoader`)
- Validation models (`ValidationError`)
- Future: `EditorState`, `EditorLogic`, UI-neutral models (`JsonEditorNode`, `FieldRow`, `CardItem`, `NestedContext`, `SchemaFieldInfo`)

**UI (`.UI`)** — Avalonia MVVM. Contains:
- ViewModels (`MainWindowViewModel` with `[ObservableProperty]`, `[RelayCommand]`)
- Views (`MainWindow.axaml`, `TreeDataTemplate`)
- Controls, styles, themes, converters
- File picker dialogs (Avalonia-specific), theme toggling

**Rule:** File I/O (`File.ReadAllText`, `StreamReader`, `JsonNode.Parse` for loaded files) belongs in Core. UI only calls Core services.

## Key Code Files

| Layer | Path | Purpose |
|---|---|---|
| Core | `Services/JsonDocumentService.cs` | GetByPath, SetByPath, Clone, Array CRUD, CreateTemplate |
| Core | `Services/JsonFileService.cs` | LoadFromFile, LoadFromStream(Async), SaveToFile |
| Core | `Services/JsonDiffService.cs` | ComputeDiff (line-by-line) |
| Core | `Services/UndoRedoService.cs` | Command-based undo/redo stack |
| Core | `Services/SchemaParser.cs` | JSON Schema → SchemaModel |
| Core | `Services/SchemaValidator.cs` | Schema validation |
| Core | `Services/SchemaLoader.cs` | Load schema files |
| Core | `Models/SchemaModel.cs` | Schema data model |
| Core | `Models/SchemaProperty.cs` | Schema property model |
| UI | `ViewModels/MainWindowViewModel.cs` | Application state, commands, tree/editor logic |
| UI | `Views/MainWindow.axaml` | Complete window layout |
| UI | `Views/TreeDataTemplate.cs` | Recursive tree node rendering |
| UI | `Models/JsonEditorNode.cs` | Observable tree node (UI layer) |
| UI | `Models/FieldRow.cs` | Property grid row (UI layer) |
| UI | `Models/CardItem.cs` | Array card item (UI layer) |
| UI | `Converters/EditorConverters.cs` | XAML value converters |
| UI | `Styles/EditorStyles.axaml` | Global component styles |
| UI | `Styles/ThemeDark.axaml` | Dark theme resources |
| UI | `Styles/ThemeLight.axaml` | Light theme resources |

## UI Type Mapping

`SchemaParser.DetermineUiType()` maps JSON Schema types to UI elements:
- "string" → TextBox (or specialized types like DatePicker, EmailBox)
- "enum" → ComboBox
- "boolean" → CheckBox
- "number"/"integer" → NumberBox
- "object" → ObjectPanel
- "array" → ArrayPanel

## Configuration

JSON Schema Auto-Detection:
1. Check `$schema` property in JSON file
2. Browse schema folder if not present
3. Query schema server (future capability)

## Required Dependencies

- `JsonSchema.Net` package in Core
- `Avalonia*` packages in UI (Avalonia UI framework)
- `CommunityToolkit.Mvvm` in UI
- .NET 10.0+ runtime target framework

## Outlook

Desktop (Avalonia) and future Web (Blazor) versions share identical Core logic.
