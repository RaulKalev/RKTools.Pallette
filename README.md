# Palette

A fast, small, keyboard-first command launcher for Revit (RK Tools). Think VS Code's Command Palette or PowerToys Run,
but for Revit commands and your own Revit tools.

```
Shortcut  ->  type 2-4 characters  ->  Enter  ->  keep working
```

Targets Revit 2024 (.NET Framework 4.8) and Revit 2026 (.NET 8) from one code base.

## Install

1. Build: `dotnet build Palette/Palette.csproj -c Release -p:PaletteUseDefaultOutput=true`
   (without the property, binaries go to `%UserProfile%\Desktop\DevDlls\Palette\`, the RK Tools convention).
2. Copy `Deploy/Palette2026.addin` (or `Palette2024.addin`) to `%AppData%\Autodesk\Revit\Addins\<year>\` and point its
   `<Assembly>` at `Palette\bin\Release\net8.0-windows\Palette.dll` (`net48` for 2024). Palette ships as one merged DLL.
3. Start Revit and choose *Load Always* when asked about the unsigned add-in. A **Palette** button appears on the
   **RK Tools** tab.

## Launch it

- Click the **Palette** ribbon button, or
- assign a keyboard shortcut (recommended): in Revit open **View > User Interface > Keyboard Shortcuts**, search for
  "Palette", and assign a key (for example `Ctrl+Space` or `F1`). This is the supported Revit mechanism; Palette installs no
  global keyboard hooks.

Palette opens next to the mouse cursor, on the monitor the cursor is on, always fully inside that monitor's work area
(DPI aware). The search box has focus immediately.

| Key | Action |
| --- | --- |
| type | filter live |
| Up / Down, PageUp / PageDown | change selection |
| Enter or click | run the command, close Palette |
| Ctrl+D | favorite / unfavorite the selected command |
| Esc, or clicking elsewhere | close |

## How search works

Everything is searched in memory (no I/O while typing). Ranking, highest first: exact name or alias, name starts with,
a word of the name starts with, initials (`cf` finds *Camera FOV*), name contains, keyword, category, description, loose
letter-order match (`vsb` finds *Visibility/Graphics*). Every word you type must match. Favorites and recently used
commands win ties inside the same band. Case and accents are ignored.

## Favorites and recently used

With an empty search box Palette shows **Favorites** (your order), then **Recently used** (last 6, no duplicates of
favorites), then the remaining commands. Favorite with the star or Ctrl+D; order them in Palette settings (the cog in
the footer, or the *Palette Settings* command). Launches are counted in `recent.json` (id, last used, use count; capped).

## Settings and where they live

Per user, never inside a Revit model:

| File | Content |
| --- | --- |
| `%AppData%\Palette\settings.json` | favorites (ordered), hidden commands, custom order, recent count |
| `%AppData%\Palette\recent.json` | recently launched commands |
| `%AppData%\Palette\Commands\*.palette.json` | command manifests (see below) |
| `%LocalAppData%\Palette\palette.log` | log (startup, settings, registration, execution failures) |

Files are written atomically. A file that cannot be parsed is copied to `*.bad-<timestamp>`, Palette falls back to
defaults and keeps working. Unknown JSON properties are ignored, so newer/older files load.

Palette settings lets you reorder favorites and the other commands (chevrons), favorite, and hide/show commands.
Hidden commands never appear in Palette.

## Adding your own commands (other plugins)

Discovery and execution are separate. Palette never instantiates another plugin's `IExternalCommand` and never calls its
`Execute`. Two supported routes:

### 1. Manifest file (no reference to Palette; recommended)

Drop `%AppData%\Palette\Commands\<yourtool>.palette.json` (or the same under `%ProgramData%\Palette\Commands`). Palette
re-reads manifests when a file changed, at the next open.

```json
{
  "source": "Camera FOV",
  "commands": [
    {
      "id": "camera.fov",
      "name": "Camera FOV",
      "category": "Security",
      "description": "Coverage of the selected camera",
      "keywords": ["camera", "cctv", "fov", "coverage"],
      "aliases": ["fov"],
      "icon": "Cctv",
      "revitCommandId": "CustomCtrl_%CustomCtrl_%RK Tools%Camera FOV%Camera FOV"
    }
  ]
}
```

Exactly one of `revitCommandId` (a string accepted by `RevitCommandId.LookupCommandId`, typically the id of your
ribbon button) or `postableCommand` (a `PostableCommand` enum name, e.g. `ManageLinks`) is required. `icon` is any
[Material Design icon name](https://pictogrammers.com/library/mdi/). Palette runs the command with `UIApplication.PostCommand`;
a command Revit does not know or cannot post right now is simply not listed.

### 2. In-process API

If your plugin can reference `Palette.dll` and is loaded into the same context, call from `OnStartup` (any time; it also
works before Palette started):

```csharp
Palette.PaletteApi.Register(
    id: "camera.fov", name: "Camera FOV", category: "Security",
    keywords: new[] { "camera", "cctv", "fov" },
    execute: uiApp => { /* runs inside a valid Revit API context */ });

Palette.PaletteApi.RegisterRevitCommand("pulse.analyse", "Pulse - Analyse Panel", "<revit command id string>");
```

The delegate runs later inside a Revit `ExternalEvent`, so it may use the `UIApplication` and open transactions.
Add `isAvailable: ctx => ctx.IsCategorySelected("Electrical Equipment")` to show a command only for a matching selection.
Revit 2025+ may load add-ins into separate contexts; if the registry is not shared, use the manifest.

## Known limitations

- Revit only lets add-ins run commands inside an API context. Palette is a modeless WPF window, so a chosen command is
  queued and run by an `ExternalEvent`; Revit starts it when it is idle (usually instantly).
- Only commands Revit exposes for posting (`PostableCommand`, or an id found by `RevitCommandId.LookupCommandId`)
  can be launched. Commands that Revit refuses to post (no project open, another command active, add-in not loaded)
  are hidden or reported with a short message, never forced.
- The exact id of a third-party ribbon button depends on its tab, panel and button name; look it up in Revit's journal
  (`Execute external command:CustomCtrl_%...`) when writing a manifest.
- Palette hides itself when it loses focus. It is not shown over a modal Revit dialog.
- No drag-and-drop reordering in settings yet (chevron buttons).

## Milestone 1 scope

Done: ribbon button, cursor-relative DPI/multi-monitor placement, instant search, keyboard navigation, central command
registry, built-in Revit commands via `PostCommand`, manifests and in-process API, favorites, recents, JSON settings,
settings window (reorder, favorite, hide), error handling and logging, context availability (`PaletteContext`).

Not yet: floating toolbar / custom ribbon, automatic plugin scanning, cloud sync, profiles, recommendations, deep context-aware
command sets.

## Architecture

```
Palette.Core/            Revit-free, unit tested (netstandard2.0, compiled into Palette.dll)
  Commands/              PaletteCommand, PaletteContext, PaletteCommandRegistry, manifests
  Search/                SearchService (weighted ranking), PaletteCatalog (home list / search results)
  Settings/              SettingsService, RecentCommandsService (atomic, tolerant JSON)
  Layout/                WindowPlacement (cursor/work-area maths in pixels)
Palette/                 Revit add-in (net48 + net8.0-windows)
  App/                   App (ribbon, startup), OpenPaletteCommand (the ribbon/shortcut command)
  Revit/                 PaletteHost (wiring), CommandExecutor (ExternalEvent), PostCommandAction,
                         BuiltInCommands, ContextBuilder, PaletteApi (public)
  Infrastructure/        WindowPositioner (Win32 cursor/monitor/DPI)
  UI/                    PaletteWindow, SettingsWindow, view models, themes (Dark/Light/High contrast)
  Diagnostics/           opt-in in-Revit self-test (Tools/Run-RevitSelfTest.ps1)
Tests/Palette.Core.Tests xUnit tests for Core
Tests/Palette.UiHarness  renders the windows outside Revit and checks placement on the real monitors
```

Flow: ribbon command (API context) builds a `PaletteContext` snapshot and shows the window -> the window searches
`PaletteCatalog` in memory -> Enter hides the window and enqueues the command -> `CommandExecutor` (ExternalEvent) runs it
with the `UIApplication`. The window is created once (pre-warmed on first idle) and re-shown, so reopening is cheap.

## Development

```
dotnet test Tests/Palette.Core.Tests -f net8.0
dotnet run --project Tests/Palette.UiHarness -c Release -p:PaletteUseDefaultOutput=true
./Tools/Run-RevitSelfTest.ps1 -RevitVersion 2026 -RevitExe "E:\Revit 2026\Revit.exe"
./Tools/Make-Icons.ps1
```

Shared code is C# 7.3; keep Revit-version specific code behind `REVIT2024` / `REVIT2026`.
