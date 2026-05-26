# Public Release Checklist

## Safety

- [x] Private 30 MB app store removed from public candidate.
- [x] Sanitized sample `data/app-store.json` added.
- [x] User metadata moved to `%LocalAppData%\CodexLocalRetrieval`.
- [x] Build output and dependency caches excluded by `.gitignore`.
- [x] Dedicated secret scanners checked; `gitleaks` and `trufflehog` were unavailable in this environment.
- [x] Public hero screenshot captured from sanitized sample data.

## Validation

- [x] `dotnet test .\native\CodexLocalRetrieval.Native.Tests\CodexLocalRetrieval.Native.Tests.csproj -p:Platform=x64`
- [x] `dotnet build .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -p:Platform=x64`
- [x] `dotnet publish .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64`
- [x] Capture a sanitized hero screenshot from the sample data.

## Release

- [x] First GitHub release ships as a portable Windows x64 ZIP.
- [ ] If shipping MSIX, create a real signing certificate flow and document install steps.
- [ ] Create the GitHub repository `codex-local-retrieval`.
- [ ] Push only after the final safety scan passes.
