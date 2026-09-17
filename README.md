# 🚀 jsonsui

> **No more raw text. Navigate and edit JSON configurations through a schema-driven UI**  
> A blazing-fast, cross-platform config editor powered by Avalonia UI.

[![GitHub license](https://shields.io)](LICENSE)
[![Build Status](https://shields.io)](actions)
[![AvaloniaUI](https://shields.io)](https://avaloniaui.net)

**jsonsui** turns standard JSON Schema into a focused, human-friendly configuration experience for complex JSON files.

Instead of exposing the entire JSON document as a form or forcing users to edit raw JSON, jsonsui separates navigation from editing:

* The tree is for navigation
* The editor is for the selected JSON context
* Arrays become card-based editors
* Nested structures can be drilled into without cluttering the main tree
* JSON Schema provides validation, field descriptions, defaults, constraints and UI metadata

The result is a configuration editor designed for large and deeply nested configuration files — without making users understand the underlying JSON structure.

Built with Avalonia UI for Windows and Linux, with a shared core architecture prepared for a web-based configuration experience.
[Key Features](#-key-features) • [Installation](#-installation) • [CLI Usage](#-cli-usage) • [Architecture](#-architecture)

---

## 🎯 Key Features

* **Schema-Driven UI:** Automatically turns types, enums, arrays, and descriptions into intuitive UI forms.
* **Pixel-Perfect & Native:** Built with **Avalonia UI** for lightning-fast performance on Windows and Linux.
* **Diff & Merge:** Compare two JSON configs side-by-side and merge changes field by field.
* **Dev-First Integration:** Includes a dedicated **VS Code Extension** to open your files with a single click.
* **Web-Ready Architecture:** Shared core logic prepared for **Blazor WebAssembly** configuration servers.
* **Extensible Core:** Dynamic plugin system to inject custom UI components, validators, and diff strategies.

## 💻 CLI Usage

Open any JSON configuration file instantly from your terminal:

```bash
# Open a config file (auto-detects schema via \$schema)
jsonsui appsettings.json

# Force a specific JSON schema
jsonsui config.json --schema ./schemas/app-schema.json

# Compare and sync two production environments
jsonsui --diff prod.json dev.json

# Set UI language
jsonsui  config.json --lang=en
```