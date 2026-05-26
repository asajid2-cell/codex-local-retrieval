# Getting Started

This tutorial builds and opens the native app with sanitized sample data.

## Prerequisites

- Windows 10 or Windows 11
- .NET 8 SDK

## Build

```powershell
dotnet build .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -p:Platform=x64
```

Expected result: `Build succeeded`.

## Run

```powershell
.\native\CodexLocalRetrieval.Native\bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\CodexLocalRetrieval.Native.exe
```

Expected result: a WinUI window opens with two sanitized sample chats.

## Test

```powershell
dotnet test .\native\CodexLocalRetrieval.Native.Tests\CodexLocalRetrieval.Native.Tests.csproj -p:Platform=x64
```

Expected result: all service tests pass.

## Package

```powershell
dotnet publish .\native\CodexLocalRetrieval.Native\CodexLocalRetrieval.Native.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -o .\artifacts\codex-local-retrieval-win-x64
Compress-Archive -Path .\artifacts\codex-local-retrieval-win-x64\* -DestinationPath .\artifacts\codex-local-retrieval-win-x64.zip -Force
```

Expected result: `artifacts/codex-local-retrieval-win-x64.zip` is created. The first release ZIP is unsigned.
