# Repository Guidelines

## Project Overview

SWDT is a Windows WPF mind-map editor. The app currently supports multiple open documents, detachable/reorderable tabs, canvas pan/zoom, auto layout, node and connection styling, summary nodes, undo/redo, recent files, light/dark/system themes, and JSON-based save/load.

The solution is intentionally small, but `MainWindow.xaml.cs` is large and owns most runtime behavior. Make scoped changes, and prefer extracting new testable logic into plain C# classes when a feature grows beyond simple UI glue.

## Project Structure & Module Organization

- `SWDT.slnx` is the solution entry point.
- `SWDT/SWDT.csproj` defines the WPF app project, targeting `net10.0-windows` with nullable reference types and implicit usings enabled.
- `SWDT/App.xaml` and `SWDT/App.xaml.cs` contain application-level resources and startup behavior.
- `SWDT/MainWindow.xaml` defines the main window, menu, toolbar, tab strip, canvas, side inspector, tree view, and color picker UI.
- `SWDT/MainWindow.xaml.cs` contains the main code-behind: document lifecycle, rendering, input handling, layout, serialization, undo/redo, themes, recent files, and inspector updates.
- `SWDT/MindMapNode.cs`, `MindMapConnection.cs`, `CanvasSettings.cs`, `MindMapFile.cs`, `MindMapDocument.cs`, `DocumentHistoryEntry.cs`, and `AppSettings.cs` are the current domain and state model classes.
- `SWDT/bin/`, `SWDT/obj/`, `.vs/`, and `*.csproj.user` are generated or local IDE outputs; do not edit or commit generated files from these locations.

If tests are added, place them in a sibling project such as `SWDT.Tests/` and add it to `SWDT.slnx`.

## Build, Test, and Development Commands

Run commands from the repository root:

- `dotnet restore .\SWDT.slnx` restores NuGet packages.
- `dotnet build .\SWDT.slnx` builds the application.
- `dotnet run --project .\SWDT\SWDT.csproj` launches the WPF app locally on Windows.
- `dotnet test .\SWDT.slnx` runs tests once a test project exists.
- `dotnet format .\SWDT.slnx` may be used before larger changes if formatting drift appears.

The project uses the installed .NET 10 SDK and Windows Desktop runtime.

## Architecture & Data Flow Notes

- `MindMapDocument` is runtime document state. It wraps the root node, canvas settings, custom connections, dirty state, selection, undo/redo stacks, and viewport transform.
- `MindMapFile` is the persisted JSON shape. Keep save/load compatibility in mind when adding fields. `DeserializeMindMapFile` also supports older files that deserialize directly as `MindMapNode`.
- `MindMapNode.Parent` is `[JsonIgnore]`; call or preserve `LinkParents` after deserialization or tree mutations.
- Normalize loaded or edited data through the existing normalization helpers (`NormalizeNodeStyle`, `NormalizeCanvasSettings`, `NormalizeConnections`, `NormalizeConnectionStyle`) instead of assuming older files have every field populated.
- App settings are stored under `%APPDATA%\SWDT\settings.json` and currently include recent files and theme mode.
- Most user-visible edits should push an undo snapshot before mutation, mark the document dirty, and refresh the canvas/inspector/tree as appropriate. Follow existing patterns such as `PushUndoSnapshot`, `MarkCurrentDocumentDirty`, `RenderCanvas`, `UpdateInspector`, `UpdateTree`, and `UpdateCommandState`.
- Canvas rendering is rebuilt in code-behind using WPF elements on `MindMapCanvas`. Node positions live in the model; visual controls live in `_nodeControls` and are disposable render artifacts.

## Coding Style & Naming Conventions

- Four spaces for indentation in C# and XAML.
- PascalCase for classes, methods, properties, records, and XAML control names.
- camelCase for local variables and private fields, matching the existing `_fieldName` style for private fields.
- Keep UI layout in XAML and interaction logic in code-behind or dedicated classes. As the app grows, move pure logic such as layout, serialization migration, or geometry calculations out of `MainWindow`.
- Nullable reference types are enabled; address nullable warnings with real checks or better types instead of broad suppressions.
- Prefer `System.Text.Json` and the existing model classes for document persistence.
- Preserve UTF-8 encoding and existing Chinese UI text. When editing strings, check for accidental mojibake or replacement characters.

## WPF UI Guidelines

- Keep named XAML controls and code-behind event handlers synchronized. When adding a control event in XAML, implement or update the matching handler in `MainWindow.xaml.cs`.
- Reuse existing resources and styles in `MainWindow.xaml` before adding new inline styling.
- Maintain keyboard workflows already present in `Window_PreviewKeyDown`, including save, undo/redo, node creation, deletion, and editing shortcuts.
- For visible UI changes, verify light, dark, and system theme behavior. Theme brushes are applied dynamically by `ApplyCurrentTheme`.
- For canvas changes, verify pan, zoom, selection, multi-selection rectangle, drag, snap-to-grid, auto layout, fit-all, and center-selected behavior when relevant.

## Testing Guidelines

There is no test project yet. For new nontrivial logic, add tests in `SWDT.Tests/` using xUnit, NUnit, or MSTest. Name test files after the class or feature under test, for example `MindMapLayoutTests.cs` or `MindMapFileMigrationTests.cs`.

Prioritize tests for:

- JSON save/load compatibility and legacy file migration.
- Layout and geometry calculations.
- Undo/redo snapshots.
- Normalization of older or partially populated files.

Run `dotnet test .\SWDT.slnx` before opening a pull request once tests are present. For UI-heavy changes, also run the app and manually exercise the changed workflow.

## Commit & Pull Request Guidelines

Project-specific commit history was not available in this environment. Use concise, imperative commit messages such as `Add connection styling controls` or `Fix recent file cleanup`.

Pull requests should include:

- A short description of the change and why it is needed.
- Build and test results, including the exact commands run.
- Screenshots or screen recordings for visible WPF UI changes.
- Notes about document format compatibility if persistence models changed.
- Linked issues or task IDs when applicable.

Keep PRs focused on one functional change when possible.
