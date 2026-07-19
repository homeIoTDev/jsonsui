# 🚀 Confix.json

> **No more raw text. Just clean config UIs based on JSON Schema**  
> A blazing-fast, cross-platform config editor powered by Avalonia UI.

[![GitHub license](https://shields.io)](LICENSE)
[![Build Status](https://shields.io)](actions)
[![AvaloniaUI](https://shields.io)](https://avaloniaui.net)

**Confix.json** bridges the gap between raw text editing and complex enterprise configurations. It reads any standard JSON Schema and instantly generates a type-safe, human-friendly user interface on Windows, Linux, and the Web. No more syntax errors, no more broken deployments.

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
confix appsettings.json

# Force a specific JSON schema
confix config.json --schema ./schemas/app-schema.json

# Compare and sync two production environments
confix --diff prod.json dev.json
```
