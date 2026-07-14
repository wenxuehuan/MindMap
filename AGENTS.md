# Repository Guidelines

## Project Structure & Module Organization

The maintained solution lives in `SWDT/`. `SWDT/SWDT.slnx` is the solution entry point; `SWDT/SWDT/` contains the .NET 10 WPF application, and `SWDT/SWDT.Tests/` contains xUnit tests. UI markup is in `App.xaml` and `MainWindow.xaml`; their `.xaml.cs` files hold startup and interaction logic. Domain and persistence types use focused files such as `MindMapNode.cs`, `MindMapDocument.cs`, and `MindMapFile.cs`. Keep reusable calculations in standalone classes such as `NodeLayoutGeometry.cs` so they can be tested without the UI. Packaging scripts are under `SWDT/installer/`. Root-level Markdown files are project and product notes; `SWDT.zip` is a release artifact, not source.

## Build, Test, and Development Commands

Run these commands from `SWDT/`:

```powershell
dotnet restore .\SWDT.slnx
dotnet build .\SWDT.slnx
dotnet test .\SWDT.slnx
dotnet run --project .\SWDT\SWDT.csproj
```

`restore` downloads dependencies, `build` compiles both projects, `test` runs the xUnit suite, and `run` launches the Windows desktop app. Use `dotnet format .\SWDT.slnx` to check and apply standard .NET formatting before a broad refactor. Packaging changes should be validated against `installer/package-iexpress.ps1` and `installer/install.ps1` on Windows.

## Coding Style & Naming Conventions

Use four-space indentation and conventional C# naming: PascalCase for types, methods, properties, and XAML controls; camelCase for locals and parameters; `_camelCase` for private fields. Nullable reference types and implicit usings are enabled. Keep visual structure in XAML, extract testable non-UI logic from `MainWindow.xaml.cs`, and preserve existing UTF-8 Chinese interface text. Do not commit generated `bin/`, `obj/`, `TestResults/`, `publish/`, or archive outputs.

## Testing Guidelines

Tests use xUnit. Place them in `SWDT.Tests/`, name files after the subject (`NodeLayoutGeometryTests.cs`), and use behavior-oriented method names such as `GetAnchoredOffset_PreservesConnectionSide`. Add regression tests for geometry, serialization compatibility, normalization, and undo/redo logic. No coverage threshold is configured; prioritize meaningful assertions and run the full solution tests before submitting.

## Commit & Pull Request Guidelines

Recent history uses short task summaries, often written in Chinese. Keep subjects concise, specific, and action-oriented (for example, `Fix dark-theme selection contrast`); avoid placeholders such as `1`. Pull requests should explain the user-visible effect, list build/test commands run, link relevant issues, and include screenshots or recordings for WPF UI changes. Call out JSON format or installer compatibility impacts explicitly, and keep each PR focused on one coherent change.
