# cKit Screen Capture

A small Win64 Windows desktop app with a left-nav tool layout.

Tools:

- `Capture` captures one monitor or the merged virtual desktop, previews it, copies it to the clipboard, and saves PNG files.
- `System Info` shows OS, runtime, process, and display details.
- `Meters` displays live CPU, GPU, and network usage.
- `Net` displays current active adapter IP addresses.
- `Clipboard` automatically stores clipboard history in a root-level SQLite database.
- `Analysis` stores keyboard and mouse input events in a root-level SQLite database.

Capture targets:

- `Merged screens` captures the full virtual desktop across all connected monitors.
- `Screen 1`, `Screen 2`, and later screens capture only that monitor.

## Requirements

- Windows
- .NET 10 SDK

## Run

```powershell
dotnet run -c Release
```

## Publish a Win64 EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

The published app is written to:

```text
bin\Release\net10.0-windows\win-x64\publish\
```

Saved screenshots go to:

```text
%USERPROFILE%\Pictures\cKitCaptures
```
