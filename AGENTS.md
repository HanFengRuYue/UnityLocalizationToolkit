# Repository Guidelines

## Project Structure & Module Organization
- `UnityLocalizationToolkit.slnx` is the solution entry point.
- `UnityLocalizationToolkit/` contains the WinUI 3 app.
- `UnityLocalizationToolkit/Pages/` holds page XAML and code-behind (HomePage, TextTranslationPage, FontReplacementPage, SettingsPage).
- `UnityLocalizationToolkit/Services/` contains scanning, Excel, asset modification, theme, and project services.
- `UnityLocalizationToolkit/Models/` contains data models like `GameProject`, `TextEntry`, and `FontAsset`.
- `UnityLocalizationToolkit/Assets/` contains legacy MSIX assets (not used in the unpackaged build).
- `UnityLocalizationToolkit/classdata.tpk` is the Unity type database copied to output.

## Build, Test, and Development Commands
- `dotnet build UnityLocalizationToolkit.slnx -c Debug` builds the solution.
- `dotnet run --project UnityLocalizationToolkit/UnityLocalizationToolkit.csproj` launches the app.
- `dotnet test` runs automated tests (none are configured yet).

## Coding Style & Naming Conventions
- C# uses file-scoped namespaces, nullable reference types, and async/await for IO-heavy work.
- Indent with 4 spaces in C# and XAML; keep XAML attribute alignment consistent.
- Use PascalCase for public types and members, camelCase for locals and private fields.
- Keep user-facing UI strings and XML docs in Simplified Chinese; keep internal comments minimal and clear.
- Use `System.Diagnostics.Trace.WriteLine()` for logging (Console output is not visible in WinUI 3).

## Testing Guidelines
- No automated test project is currently configured.
- If you add tests, create a `*.Tests` project and run `dotnet test`.
- For manual verification, run the app and validate scan/export/import and font replacement flows.

## Commit & Pull Request Guidelines
- Commit subjects are short and imperative; current history is mostly Simplified Chinese, with English also acceptable.
- PRs should describe the change, list verification steps, and link issues when applicable.
- Include screenshots or short recordings for UI changes and call out any data or asset impacts.

## Configuration & Data Safety
- The tool modifies game assets; always work on copies and rely on the built-in backup behavior.
- Avoid committing large game files or generated outputs to the repo.
