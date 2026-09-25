# Contributing to jsonsui

Thanks for your interest in improving jsonsui.

Please read [`ARCHITECTURE_OVERVIEW.md`](ARCHITECTURE_OVERVIEW.md) first — it defines the project architecture and the strict separation between the Core and UI layers.

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet build Jsonsui.slnx
```

## Run

Desktop UI:

```bash
dotnet run --project src/Jsonsui.UI
```

Command line:

```bash
dotnet run --project src/Jsonsui.Tui -- ./config.json
```

## Project Structure

| Project | Purpose |
|---|---|
| `src/Jsonsui.Core` | UI-independent logic: models, services, document CRUD, validation, diff, undo/redo, file I/O |
| `src/Jsonsui.UI` | Avalonia desktop UI (MVVM with CommunityToolkit.Mvvm) |
| `src/Jsonsui.Tui` | Command-line entry point |

The UI project references the Core library. **Core must never reference UI.** File I/O belongs in Core; the UI only calls Core services.

## Coding Guidelines

* Follow the existing code style and conventions.
* Keep changes focused and small.
* Do not add comments unless they explain non-obvious intent.

## Tests

There is currently no automated test suite. Verify changes by building the solution and manually testing the UI and CLI:

```bash
dotnet build Jsonsui.slnx
```

## Release

Releases are built automatically by GitHub Actions when a tag matching `vX.Y.Z` is pushed:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The version is taken from the tag and applied to the assembly and file version. The resulting archives are attached to the GitHub Release.

Two variants are built for every platform:

| Variant | Requires | Contents |
|---|---|---|
| `*-full.*` | nothing | self-contained, single executable |
| `*-slim.*` | .NET 10 Runtime | single executable (framework-dependent) |

Supported platforms: Windows x64, Linux x64, macOS x64 and macOS arm64.

## Pull Requests

* Ensure `dotnet build Jsonsui.slnx` succeeds.
* Describe what changed and why.
* Keep pull requests focused on a single concern.
