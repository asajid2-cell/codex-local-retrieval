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

## Release Artifact

- `codex-local-retrieval-win-x64.zip`
- Portable Windows x64 build.
- Unsigned; Windows SmartScreen may warn on first launch.

## Validation

- `dotnet build .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -p:Platform=x64`
- `dotnet test .\native\CodexLocalRetrieval.Native.Tests\CodexLocalRetrieval.Native.Tests.csproj -p:Platform=x64`
- `dotnet publish .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64`

## Known Issues

- MSIX packaging and code signing are not implemented.
- Import UI is not public-ready, although the core service can parse JSONL roots.
- A sanitized README screenshot is included. Additional workflow video capture is not included.
