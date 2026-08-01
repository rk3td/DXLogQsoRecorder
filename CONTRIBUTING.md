# Contributing to DXLog QSO Recorder

Thank you for your interest in the project.

## Before opening a pull request

1. Open an issue for significant changes so the approach can be discussed first.
2. Keep the application portable. Do not add mandatory installers, Registry storage, or machine-wide configuration.
3. Preserve compatibility with Windows 10 and Windows 11.
4. Keep user-facing text, source comments, logs, and documentation in English.
5. Do not add automatic deletion of recordings.
6. Do not introduce integration with ContestJudge into this repository.

## Development setup

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022, Rider, or the `dotnet` CLI

Build the project:

```powershell
dotnet restore .\DXLogQsoRecorder\DXLogQsoRecorder.csproj
dotnet build .\DXLogQsoRecorder\DXLogQsoRecorder.csproj -c Release
```

Build the portable Windows x64 package:

```text
build-portable-win-x64.cmd
```

## Pull requests

- Make one logical change per pull request.
- Describe the behavior before and after the change.
- Include reproduction steps for bug fixes.
- Test UDP reception, audio capture, MP3 output, and WAV fallback when relevant.
- Do not commit generated `bin`, `obj`, `publish`, `Data`, `Logs`, `Packets`, `Temp`, or `Recordings` directories.

## Coding style

The repository includes `.editorconfig`. Prefer clear, explicit code and avoid unnecessary background processing in the audio path.
