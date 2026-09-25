# 🚀 jsonsui

> **No more raw text. Navigate and edit JSON configurations through a schema-driven UI.**

> A fast, cross-platform JSON configuration editor powered by Avalonia UI.

[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/UI-Avalonia-8B5CF6.svg)](https://avaloniaui.net/)

Built with Avalonia UI for Windows and Linux, with a shared UI-independent core designed to support additional frontends in the future.

**[Why jsonsui?](#-why-jsonsui) • [Visual Editing](#-visual-json-editing) • [Downloads](#-downloads) • [Getting Started](#-getting-started) • [CLI](#-cli) • [Architecture](#-architecture)**

---

## ✨ Why jsonsui?

JSON is everywhere — especially in configuration files.

But as configurations grow, editing raw JSON becomes increasingly difficult:

* Where is the setting I need?
* What value is actually allowed here?
* Which fields are required?
* What does this property mean?
* How do I safely edit deeply nested objects and arrays?

**jsonsui** takes a different approach.

Instead of navigating large configuration files as raw text, you can explore their structure visually and edit the relevant parts through an intuitive UI.

### 🧭 Navigate

Use a structured tree to move through objects and arrays without turning every individual JSON property into a tree node.

### ✏️ Edit visually

Properties are presented as appropriate UI controls instead of requiring manual JSON syntax.

### 🧩 Let the schema do the work

When a JSON Schema is available, jsonsui uses it to understand the configuration and provide appropriate controls, descriptions, defaults, constraints, enums and validation.

### 🔎 Handle complex configurations

Nested objects can be opened where they are needed, while arrays are presented as editable cards rather than a long list of indexed tree nodes.

---

## 🎯 Visual JSON Editing

The main experience in jsonsui is **visual editing of JSON configurations**.

Depending on the schema and value type, jsonsui can present properties as:

* Text fields
* Checkboxes
* Numeric controls
* Enum drop-downs
* Nullable value controls
* Array editors
* Nested object editors

Schema information can also provide:

* Descriptions and help text
* Default values
* Required fields
* Minimum / maximum constraints
* String length constraints
* Patterns
* Read-only properties
* Deprecated properties

The goal is simple:

> **Work with the configuration — not with its syntax.**

---

## 🧩 JSON Schema

JSON Schema is a central part of jsonsui.

If a JSON document contains a `$schema` reference, jsonsui can automatically detect and load the associated schema.

Schemas can also be loaded manually when needed.

For example, a schema can turn a configuration such as:

```json
{
  "port": 8080,
  "tls": true,
  "mode": "production"
}
```

into an editor with appropriate controls and constraints instead of requiring the user to edit the raw JSON directly.

This allows the same JSON structure to become a guided configuration experience.

---

## 📚 Arrays & Nested Objects

Complex configurations should not require endlessly scrolling through raw JSON.

jsonsui keeps the main navigation focused on meaningful structural elements and lets you drill into nested objects when needed.

Arrays are handled through an editor view with individual cards for their elements, making larger collections easier to understand and edit.

---

## 🔍 JSON Diff

Before saving changes, jsonsui can compare the current configuration with the previous state and show structural changes.

The diff currently identifies:

* Added values
* Removed values
* Modified values
* Unchanged values
* Type changes
* Object and array changes

The result is presented in a dedicated diff view so changes can be reviewed before committing them to disk.

---

## 📝 Raw JSON Editing

Visual editing is the primary experience, but jsonsui also provides a text-based JSON editor for situations where direct JSON editing is more appropriate.

This gives you a choice between:

**Visual editing when structure and schema guidance matter.**

**Raw JSON when direct text editing is faster.**

---

## 🛠️ More than just editing

jsonsui also includes:

* ↩️ Undo / Redo
* 💾 Load and Save
* ✅ Live validation of supported schema constraints
* ➕ Add properties
* 🗑️ Remove properties
* 🌍 Localization
* 🔎 Tree filtering
* 🌓 Avalonia-based desktop UI
* 🖥️ Windows and Linux support
* ⌨️ Command-line entry points

The feature set is focused on making configuration editing safer and easier without hiding the underlying JSON.

---

## 🤖 AI-Assisted Development

`jsonsui` is developed with **AI-assisted development tools**.

Rather than generating the project in one large step, development is done iteratively in small, verifiable stages.

The workflow typically looks like this:

```text
Idea / Requirement
       ↓
Architecture & UX decision
       ↓
Small implementation step
       ↓
Build & verification
       ↓
Manual UI testing
       ↓
Regression check
       ↓
Next iteration
```

AI is used as a development tool for tasks such as implementation, refactoring, investigation and debugging.

**Architecture, requirements, design decisions, verification and final acceptance remain human-driven.**

This approach allows jsonsui to be developed quickly while keeping the codebase understandable and intentionally designed.

---

## 🏗️ Architecture

jsonsui is built around a UI-independent core:

```text
┌──────────────────────┐
│      Jsonsui.UI      │
│      Avalonia UI     │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│     Jsonsui.Core     │
│                      │
│  JSON editing logic  │
│  Schema handling     │
│  Validation          │
│  Navigation          │
│  Undo / Redo         │
│  Diff                │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│     Jsonsui.Tui      │
│   CLI / console      │
└──────────────────────┘
```

The separation keeps the core JSON and schema logic independent from Avalonia and leaves room for additional frontends in the future.

---

## 📥 Downloads

Pre-built releases are available on the [Releases page](https://github.com/homeIoTDev/jsonsui/releases).

Two variants are provided for every platform:

| Variant | Requires | Size | Contents |
|---|---|---|---|
| `*-full.*` | nothing | large | single executable |
| `*-slim.*` | .NET 10 Runtime | small | single executable |

| Platform | Full (self-contained) | Slim (framework-dependent) |
|---|---|---|
| Windows x64 | `jsonsui-win-x64-full.zip` | `jsonsui-win-x64-slim.zip` |
| Linux x64 | `jsonsui-linux-x64-full.tar.gz` | `jsonsui-linux-x64-slim.tar.gz` |
| macOS x64 | `jsonsui-macos-x64-full.zip` | `jsonsui-macos-x64-slim.zip` |
| macOS arm64 | `jsonsui-macos-arm64-full.zip` | `jsonsui-macos-arm64-slim.zip` |

Each archive contains a `jsonsui/` folder with the executable (`jsonsui.exe` on Windows, `jsonsui` on Linux and macOS).

> macOS binaries are ad-hoc signed. On first launch you may need to allow the app in **System Settings → Privacy & Security**.

---

## 🚀 Getting Started

### Requirements

* .NET 10 SDK

### Build

```bash
dotnet build Jsonsui.slnx
```

### Run the application

```bash
dotnet run --project src/Jsonsui.UI
```

You can also provide a JSON configuration and, if required, a schema through the command line.

---

## ⌨️ CLI

The project also includes a lightweight command-line entry point.

Example:

```bash
dotnet run --project src/Jsonsui.Tui -- ./config.json
```

Available functionality includes:

* JSON input
* Schema input
* JSON diff
* Language selection
* Help

The console project currently provides command-line functionality rather than a full interactive terminal editor.

---

## 🗺️ What's next?

jsonsui is already usable as a JSON configuration editor, but there is plenty of room to expand it.

Possible future directions include:

* More advanced JSON Schema support
* Improved schema composition (`oneOf`, `anyOf`, `allOf`)
* Further diff capabilities
* Additional frontends
* Web-based configuration editing
* A fully interactive terminal UI
* Additional integrations

The roadmap is intentionally open — the project will evolve based on practical use and feedback.

---

## ❤️ Support the Project

jsonsui is open source and developed independently.

If you find it useful, there are several ways to support the project:

* ⭐ Star the repository
* 🐛 Report bugs
* 💡 Suggest improvements
* 🔧 Contribute code
* 📣 Share the project
* ❤️ Support future development

Financial support can help make it possible to spend more time on jsonsui and future features.

---

## 📄 License

jsonsui is released under the **MIT License**.

See [LICENSE](LICENSE) for details.
