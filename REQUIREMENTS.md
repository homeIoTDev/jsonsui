# JSON UI Editor – Requirements & Architecture

## 1. Project Goal
The JSON UI Editor is a cross-platform tool (Windows + Linux) that renders JSON configuration files as a **guided UI** based on a JSON Schema.  
Instead of raw text, the tool offers:

- Dropdowns for enums
- Checkboxes for booleans
- NumericUpDown for numbers
- Lists with add/remove
- Panels for nested objects
- Live validation
- Help texts directly on the field
- Diff view between two JSON configs

The tool is intended to run both **locally** as a native app (Avalonia UI) and later **on the web** (Blazor UI) for a config server.

---

## 2. Main Requirements

### 2.1 JSON Schema as UI Blueprint
- The JSON Schema fully defines the UI.
- Types are automatically translated into UI elements.
- `description` is displayed as help text.
- `enum` is rendered as a dropdown.
- `object` is rendered as a panel/expander.
- `array` is rendered as a list with add/remove.
- `oneOf` / `anyOf` is rendered as a selection panel.

### 2.2 Guided UI (PropertyGrid style)
- No text editing in the foreground.
- UI elements prevent errors.
- Required fields are marked.
- Invalid input is ignored or optionally removed.
- Live validation directly on the field.
- On the left is the tree showing the JSON structure; on the right the content is displayed (editor area). In the editor area the current help for the focused UI element is shown. The values `$id` and `$schema` are hidden, i.e. not immediately visible.

#### Tree view (left)
- Shows the JSON structure.
- Behind each element the number of sub-elements is shown, e.g. `{6}` or `[9]`.
- Individual elements are not expanded, since they are visible in the editor area.
- Deferred: array objects are displayed as `[0]`, `[1]`, etc. – but only if the respective array element itself contains further navigable JSON structures (objects or arrays). If an array element contains exclusively scalar properties, it is not displayed as its own tree node, since its contents are fully edited in the editor area of the array.
- Arrows show the expand state:
  - `▶` for collapsed
  - `▼` for expanded
  - `[]` for arrays
- Above the tree view there is a search field.
- In the tree you can only jump to a JSON object or an actually navigable array context. Individual elements must not be expandable with a single click, since they are already visible in the editor area. Single-click expansion is only possible if below a JSON object there are further navigable JSON objects/arrays, or below an array element there are further navigable JSON objects/arrays.
- An array whose elements contain exclusively scalar properties or simple objects without further nested objects/arrays remains visible as an array node in the tree; however, its individual elements are not displayed as [0], [1] etc. in the tree.
- Example: matrixData → [[{x,y,value}, {x,y,value}], ...]: the first array level matrixData is navigable; the array elements [0], [1] of the inner arrays are not required as tree nodes if their contents are fully reachable via the array card/editor representation.

#### Editor area (right)
- Shows the contents of the selected JSON object.
- At the top a breadcrumb is displayed (e.g. `root / plugins / [0]`).
- Clicking the breadcrumb jumps directly to the corresponding location in the tree.

##### Editing in the editor area (right)
- All changes to the respective JSON part must be tracked in memory by the UI control that visualizes this part, in order to point out changed data to the user and possibly undo it.
- If no list is focused in the tree, the vertically split area is omitted and the editor area is displayed across the full width.
- EVERY UI control must be focusable with its text and button as a whole. The focus must be easily indicated to the user so that they recognize the reference to the help section.

#### Arrays in the editor area
- Arrays are displayed in the editor area as array item cards. The display splits the editor area vertically, with the array item cards on the left and the editor area continuing on the right.
  - This representation is also used for deeply nested JSON objects that contain arrays. In this case the tree structure is visualized up to this object, and the editor area shows the content from this level. If an array is present there, the editor area is not split again and the view does not become narrower. If an array contains simple object elements without further navigable substructures, these elements are displayed exclusively as cards and not additionally as [0], [1] etc. in the tree view.
  - With nested arrays the tree selection remains stable; the editor area shows the deeper level.
  - Each card item has an "Edit" button to open the detail editor and a "Delete" button to remove the item.
  - Below the list there is an "Add" button to create new array items.
  - When a list is selected, the help from the JSON schema is displayed compactly above the list.
  - If the right editor area in turn contains a list, a button also appears here, and all three window areas focus accordingly on this hierarchy section. The tree selection remains on the JSON object and is not changed.

### Temporary drill-down navigation within array items
- Array elements such as [0], [1], [2] are generally not displayed as permanent nodes in the tree view, since they are already represented by the array item cards in the editor.
- If an array item contains a nested JSON object or array, it can be opened from the editor area via navigation/drill-down.
- The drill-down is a temporary editor context and does not extend the permanent tree structure.
Example: for root / plugins / [0] / settings the tree selection remains on plugins. The breadcrumb, however, shows the full path including [0] and settings.
- If another array is opened within the drill-down, its card list replaces the previous array representation in the editor area. The editor is not additionally nested as a result.
- The breadcrumb represents the full effective JSON path on every drill-down, including the array indices used.
- When leaving the drill-down, the previous editor/array context is restored.
- SelectedPath still describes exclusively the domain tree selection. The temporary drill-down context must not change SelectedPath.
- This keeps tree selection, array card selection and temporary editor drill-down as three separate states.

#### Help
- Is always displayed at the bottom of the editor area.
- Remains visible when scrolling.
- Contains:
  - `description`
  - `default`
  - `deprecated`
  - `readOnly`
  - `$comment`
- The help always belongs to the focused UI control (which visualizes a JSON part).
- If nothing is focused, the document description is displayed.

#### Dark/Light mode
- Toggleable via icon.
- In dark mode, borders are displayed in neon colors.

#### Text mode
- Activatable via icon and is thus a separate mode to edit JSON as text.
- Shows the JSON text of the selected object in the tree.
- Tree remains visible.
  - The selection in the tree can be changed at any time.
- Help remains visible and shows help depending on where the cursor is in the editor window.
- Validation errors appear below the editor.
- Global errors are marked with "!" in the tree.

#### Icon strip (below the tree structure)
Icons only, no text buttons:

| Function | Icon |
|---------|------|
| Open file | 📂 |
| Save file | 💾 |
| Load schema | 🗂 |
| Dark/Light mode | 🌓 |
| Text mode | 📝 |
| Diff view | 🔀 |

### 2.3 Diff View
- Two JSON configs side by side.
- Changes highlighted in color.
- "Apply" buttons per field.
- Comparison based on JSON tree structure.

### 2.4 Schema Auto-Detection
- `$schema` in the JSON is detected automatically.
- If not present:
  - User selects the schema manually.
  - Tool searches the schema folder.
  - Tool can query a schema server (later).

### 2.5 VS Code Integration
- VS Code extension starts the tool externally.
- Passes the file path to the tool.
- Optional: write the file back after editing.
- Goal: "Open in JSON UI Editor" button in VS Code.

### 2.6 Plugin System (preparation)
Plugins should later:

- add their own UI elements
- register their own validators
- add their own panels
- implement their own diff strategies
- provide schema extensions

Plugins are DLLs and are loaded dynamically.

---

## 3. Architecture Decisions

### 3.1 UI Framework: Avalonia UI (Desktop)
**Rationale:**
- Very fast (native rendering)
- Cross-platform (Windows + Linux + macOS)
- XAML-based (like WPF, but modern)
- Ideal for tools
- No browser dependency
- No WebView2 (Blazor Hybrid would be Windows-only)
- Perfect for dynamic UI generation

### 3.2 Web Framework (later): Blazor WebAssembly
**Rationale:**
- Web version for config server
- Multi-user editing possible
- Same logic as the desktop version
- UI can be rebuilt later
- Runs anywhere in the browser

### 3.3 Core Layer (shared)
The Core Jsonsui.Core contains:

- JSON parser (System.Text.Json)
- JSON Schema parser (System.Text.Json)
- Validation logic
- Diff engine
- Plugin system
- Undo/redo
- File handling
- Schema auto-finder
- JSON models (UI-neutral)
  - JsonEditorNode (without ObservableProperty)
  - FieldRow (without ObservableProperty)
  - CardItem (without ObservableProperty)
  - NestedContext
  - SchemaFieldInfo
  - ValidationError
- Services (already in Core)
  - JsonDocumentService
  - JsonDiffService
  - SchemaLoader
  - SchemaParser
  - SchemaValidator
  - UndoRedoService
- Editor state
```
public class EditorState
{
    public JsonNode Json { get; set; }
    public JsonNode OriginalJson { get; set; }
    public string[] SelectedPath { get; set; }
    public HashSet<string> Expanded { get; set; }
    public int? CardIndex { get; set; }
    public NestedContext? NestedCtx { get; set; }
    public ValidationError[] Errors { get; set; }
}
```
- Editor logic
  - All functions that today live in the Avalonia view model but do not require UI elements:
  - Tree builder
  - Node selection
  - Expand/collapse
  - Array drill-down
  - Object field builder
  - Scalar node builder
  - Error marking
  - Derived values
  - JSON manipulation
  - Undo/redo integration
  - Schema integration

The Core is **UI-independent** and is shared by Avalonia and Blazor.

---

## 4. Technical Guardrails

### 4.1 Handling Invalid JSON
- Parser is tolerant.
- Invalid fields are marked.
- User decides:
  - ignore
  - repair
  - delete
- Tool offers auto-fix suggestions.

### 4.2 Performance
- Avalonia for desktop → fastest approach.
- Blazor WebAssembly only for the web version.
- Core is optimized for large JSON files.
- UI uses virtualization for large arrays.

### 4.3 Startup Behavior
Desktop:
```bash
jsonui config.json
```
