# v0.1.2

Patch release for the app icon.

## Fixes

- Regenerated the app icon, titlebar icon, tile logos, and launcher icon with transparent backgrounds.

# v0.1.1

Patch release for local session indexing.

## Fixes

- Local session indexing streams Codex JSONL files, so large or actively written chat logs can load without leaving the app stuck on sample sessions.
- Real Codex session titles are refreshed after indexing so freshly indexed local chats sort and label correctly.

# v0.1.0

First public release of Codex Local Retrieval.

## Highlights

- Native WinUI 3 desktop app for browsing sanitized local session archives.
- Deep fuzzy search across titles, paths, messages, and code blocks.
- Restore packet builder for continuing archived work in a new chat.
- Workspace and collection group views.
- Right-click chat actions for pin, rename, archive, copy path, and collections.
- Theme, accent, shape, and density settings.
- Optional OpenAI-compatible AI provider support with model detection and Windows credential storage for API keys.
- Startup auto-detection for local Codex sessions, plus a Settings chat source path for manual indexing.

## Release Artifact

- `codex-local-retrieval-win-x64.zip`
- Portable Windows x64 build.
- Contains a top-level `Codex Local Retrieval.exe` launcher and an `app` folder for runtime files.
- Unsigned; Windows SmartScreen may warn on first launch.

## Validation

- `dotnet build .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -p:Platform=x64`
- `dotnet test .\native\CodexLocalRetrieval.Native.Tests\CodexLocalRetrieval.Native.Tests.csproj -p:Platform=x64`
- `.\tools\release\package-win-x64.ps1`
- Extracted ZIP launcher smoke test.

## Known Issues

- MSIX packaging and code signing are not implemented.
- Auto-detection targets standard Codex folders. Users with custom archive locations should set the chat source path in Settings.
- A sanitized README screenshot is included. Additional workflow video capture is not included.
