# Tossakan — User & Build Manual

This manual covers how to run Tossakan for development and how to publish it as a
standalone Windows application.

## Prerequisites

- Windows 10 (19041+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
- No Visual Studio required — everything here uses the `dotnet` CLI

Check your SDK version:

```
dotnet --version
```

Should report an `8.0.x` SDK.

## Running the app (development)

From the repository root:

```
dotnet run --project "src\Tossakan"
```

This restores packages, builds a Debug x64 binary and launches the app.

### Alternative: build then launch the exe directly

```
dotnet build
src\Tossakan\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Tossakan.exe
```

Use this when you want to launch the app repeatedly without rebuilding each time (e.g. from a
shortcut or a script).

### First run

On first launch the app creates its data folder and seeds a "Welcome Board":

- Database: `%LOCALAPPDATA%\Tossakan\workmanagement.db`
- Attachments: `%LOCALAPPDATA%\Tossakan\attachments\`
- Custom background photos: `%LOCALAPPDATA%\Tossakan\backgrounds\`
- Error log: `%LOCALAPPDATA%\Tossakan\app.log`

If the UI appears to do nothing after an action, check `app.log` first — unhandled exceptions
are logged there instead of crashing the app.

### Resetting all data

Close the app, then delete the whole `%LOCALAPPDATA%\Tossakan\` folder. It will be
recreated (with a fresh Welcome Board) the next time the app starts.

## Publishing a standalone build

A standalone build is self-contained: it bundles the .NET runtime and the Windows App SDK, so it
runs on a machine with nothing else installed.

### One-step publish (recommended)

```
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

This will:

1. Run `dotnet publish` in Release mode for `win-x64`, self-contained, into `.\publish\`
2. Create a **Tossakan** shortcut on your Desktop pointing at `publish\Tossakan.exe`

When it finishes:

- Standalone exe: `publish\Tossakan.exe`
- Shortcut: `<Desktop>\Tossakan.lnk`

### Manual publish (equivalent, without the shortcut step)

```
dotnet publish "src\Tossakan\Tossakan.csproj" -c Release -r win-x64 --self-contained true -o publish
```

### Distributing the build

Copy the entire `publish\` folder (not just the .exe — it depends on the sibling .dll/.pri/
runtime files and language resource folders next to it) to the target machine, or zip it up.
There is no installer; it runs directly from that folder.

### Re-publishing after changes

Re-run the same publish command/script. It overwrites the contents of `publish\` in place. The
desktop shortcut does not need to be recreated unless the target path changed.

## Troubleshooting

| Symptom | Where to look |
|---|---|
| App won't start / closes immediately | `%LOCALAPPDATA%\Tossakan\app.log` |
| A click/action seems to do nothing | Same log — most failures are caught and logged there |
| Need a clean slate | Delete `%LOCALAPPDATA%\Tossakan\` (see "Resetting all data" above) |
| Build fails referencing WindowsAppSDK | Confirm `Microsoft.WindowsAppSDK` stays on the 1.8.x line (see CLAUDE.md pitfalls) |
