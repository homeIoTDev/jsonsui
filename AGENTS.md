# ConfixJson Development Guide

## Project Structure

- `src/ConfixJson.Core/` - JSON Schema handling, validation, and shared logic
- `src/ConfixJson.UI/` - Avalonia desktop UI application
- `ConfixJson.slnx` - .NET solution file

The UI project references the Core library.

## Entry Points

**Desktop App:** Run `dotnet run --project src/ConfixJson.UI/ConfixJson.UI.csproj`
**Core Library:** Use `src/ConfixJson.Core` as package reference

## Key Code Files

- `src/ConfixJson.Core/Models/*` - Data models (SchemaModel, SchemaProperty)
- `src/ConfixJson.Core/Services/` - Core services (SchemaParser, SchemaValidator, SchemaLoader)
- `src/ConfixJson.UI/Program.cs` - Avalonia app entry point
- `src/ConfixJson.UI/Views/` - UI views
- `src/ConfixJson.UI/ViewModels/` - View models

## Build & Run

1. Build solution: `dotnet build ConfixJson.slnx`
2. Run desktop app: `dotnet run --project src/ConfixJson.UI/ConfixJson.UI.csproj`
3. Core tests may exist for validation logic in `src/ConfixJson.Core`

## Architecture Notes

- **Core (`.Core`)**: UI-independent JSON schema parsing, validation, and diff logic (used by both desktop and web versions)
- **UI (`.UI`)**: Avalonia-based desktop interface
- Schema-driven: JSON Schema defines UI completely (types → UI elements, descriptions → help text)
- Plugin system supported (DLLs loaded dynamically at runtime)

## UI Type Mapping

`SchemaParser.DetermineUiType()` maps JSON Schema types to UI elements:
- "string" → TextBox (or specialized types like DatePicker, EmailBox)
- "enum" → ComboBox
- "boolean" → CheckBox
- "number"/"integer" → NumberBox
- "object" → ObjectPanel
- "array" → ArrayPanel

## Core Services (just a good deal)

1. **SchemaParser**: Converts JSON Schema to SchemaModel objects
2. **SchemaValidator**: Validates JSON files against schemas
3. **SchemaLoader**: Locates and loads schema files or from schema servers

## Configuration

JSON Schema Auto-Detection:
1. Check `$schema` property in JSON file
2. Browse schema folder if not present
3. Query schema server (future capability)

## Development Workflow

1. Modify JSON Schema → Core updates type mappings → UI reflects automatically
2. Validate JSON files against schemas using Core library
3. Run desktop app to test UI generation
4. UI preview shows schema-driven properties with live validation

## Required Dependencies

Based on project files:
- `JsonSchema.Net` package in Core
- `Avalonia*` packages in UI (Avalonia UI framework)
- .NET 10.0+ runtime target framework

## File Structure of Core

`src/ConfixJson.Core/` contains:
- **Models**: `SchemaModel.cs`, `SchemaProperty.cs`, `UiElementType.cs` (enum for UI element types)
- **Services**: `SchemaLoader.cs`, `SchemaParser.cs`, `SchemaValidator.cs`

## Outlook

Desktop (Avalonia) and future Web (Blazor) versions share identical Core logic.

## Current Status

Basic build (console output shows successful)