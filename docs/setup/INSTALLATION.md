# Installation

There is no installer. AVEX OS is currently built from source and run unpackaged.

## Requirements (verified on this host)

| Requirement | Version used | Notes |
|---|---|---|
| Windows | 11 build 26300 x64 | Minimum target platform 10.0.17763 |
| .NET SDK | 10.0.403 | User-scoped install supported |
| .NET runtime | `Microsoft.NETCore.App` 10.0.x | App is framework-dependent, rolls forward to 10.0.14 |
| Windows App Runtime | 2.6.5 | Required at run time for unpackaged WinUI 3 |
| Windows SDK | 10.0.26100 | Referenced by the TFM |
| Visual C++ build tools | 17.14.x | Needed by the WinUI/XAML toolchain |

Developer Mode is **not** required (the app is unpackaged). It **is** required if you want to try the
MSIX/packaged configuration.

## 1. Get the SDK

If `dotnet` is not already on `PATH`, install the user-scoped SDK and note its location, e.g.

```
%LOCALAPPDATA%\Microsoft\dotnet
```

## 2. Build

From the repository root:

```bat
set "DOTNET_ROOT=C:\Users\<you>\AppData\Local\Microsoft\dotnet"
"%DOTNET_ROOT%\dotnet" build AVEXOS.slnx -c Debug
```

Expected: `Build succeeded`, **0 errors**. The warning count MSBuild prints depends on how you build:
it reads 10 after an incremental build of an up-to-date tree and 12 with `-t:Rebuild`, because each
warning is reported once at solution scope and once at project scope. The distinct set is **6** — four
`NU1903` (the SQLitePCLRaw advisory, one per project that transitively references SQLite) and two
`CS0168` unused variables. See the README for the counting command.

If you get `error MSB3021/MSB3027 ... being used by another process`, a previously launched
`AVEXOS.App.exe` is still holding its own DLLs. Close the app and rebuild.

## 3. Run

```bat
scripts\launch-app.cmd
```

This sets `DOTNET_ROOT` and starts the exe from
`src\AVEXOS.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\`.

### The `DOTNET_ROOT` requirement is not optional

Launching `AVEXOS.App.exe` without `DOTNET_ROOT` set does **not** produce a console error. It shows a
GUI dialog whose window title is the executable name and whose process stays alive until dismissed. If
you are scripting against the app, check the window **class** before assuming it launched:

| Class | Meaning |
|---|---|
| `WinUIDesktopWin32WindowClass` | the app really launched |
| an ordinary dialog owned by the same PID | the apphost could not find the runtime |

`scripts/capture-window.ps1` prints class, title and geometry for every window the process owns.

## 4. Verify the install

```bat
"%DOTNET_ROOT%\dotnet" test tests\AVEXOS.Security.Tests
"%DOTNET_ROOT%\dotnet" test tests\AVEXOS.ThemeEngine.Tests
```

Expected: 29 passed, 6 passed, 8 passed, and 3 passed.

Then confirm the app window exists:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -STA -File scripts/capture-window.ps1
```

## 5. Data written on first run

```
%LOCALAPPDATA%\AVEXOS\
  avex.db              audit log, operation records, settings
  backups\bk-*.json    one file per pre-change snapshot
  wallpapers\          imported wallpaper files
  themes\  cache\  logs\
%APPDATA%\AVEXOS\      roaming root
```

Nothing is written to `C:\Windows`, `Program Files`, or `HKLM`.

## 6. Uninstall

Delete the app folder and the data roots:

```bat
rd /s /q "%LOCALAPPDATA%\AVEXOS"
rd /s /q "%APPDATA%\AVEXOS"
```

**Before deleting `%LOCALAPPDATA%\AVEXOS\backups`,** restore any changes you applied — the backups are
the only record of your original values. See
[BACKUP_AND_RECOVERY.md](../operations/BACKUP_AND_RECOVERY.md).

No system component is modified during install, so there is nothing else to unwind.
