# StarStrings Auto-Updater

A Windows tray app that keeps [MrKraken's StarStrings](https://github.com/MrKraken/StarStrings)
`global.ini` localization file up to date for **Star Citizen LIVE**.

It periodically checks the StarStrings GitHub releases page, and when a new version is
published, downloads the `StarStrings-LIVE.zip`, extracts `global.ini` (and `user.cfg` if you
don't already have one), and installs them into your Star Citizen LIVE folder — after asking
you first.

## What it does

- Runs quietly in the system tray, starting automatically when you log into Windows.
- Checks the StarStrings "latest" release every 30 minutes (configurable, see below).
- When it finds a genuinely new version, shows a dialog with the release name, publish date,
  and release notes, and asks **Install Now** or **Skip**.
- Backs up your existing `global.ini` to `global.ini.bak` before overwriting it, and lets you
  restore that backup from the tray menu at any time.
- Never touches an existing `user.cfg` — it only installs the bundled one if you don't have one
  yet, matching the StarStrings README's own install instructions.
- Keeps two logs under `%AppData%\StarStringsAutoUpdater\logs\`:
  - `update.log` — general activity/diagnostics (checks, downloads, errors).
  - `version-history.log` — one line per version actually installed, with timestamp, release
    name, and the zip's SHA-256 hash.
- Also checks for new releases of itself (see "Updating the app itself" below) and offers to
  download them, on the same schedule as the StarStrings content check.
- Shows the running app version at the top of the tray menu and in the tray icon's tooltip.
- Tray icon right-click menu: **Start/Stop Auto-Check**, **Check for Updates Now**, **Change
  Star Citizen LIVE Folder**, **Open Log Folder**, **Restore Backup (global.ini.bak)**, **Start
  with Windows** (toggle), **Exit**.

## Why this isn't a literal Windows Service

A true Windows Service (the kind managed by `services.msc`/SCM) runs in an isolated session
with no desktop access, so it **cannot show a tray icon or any UI** — that's a Windows security
boundary (Session 0 isolation), not a limitation of this app. Since the requirement was a
right-clickable taskbar icon that can start/stop the checking loop, this app instead:

- Registers itself to launch automatically when you log in, via the per-user
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry key (no admin rights needed).
- Runs the periodic check on a background timer inside the tray app itself.
- "Start/Stop" in the tray menu pauses/resumes that timer — the app keeps running either way,
  so you can resume from the tray without relaunching anything.

## How "new version" is detected

The upstream [StarStrings release workflow](https://github.com/MrKraken/StarStrings/blob/master/.github/workflows/release.yml)
deletes and recreates its `latest` release/tag on every push to `master` — so the tag name
never changes, only the release's publish timestamp, name, and the zip's contents. This app
therefore compares:

1. The release's `published_at` timestamp against the last one it installed. If unchanged, it
   skips the check entirely (no download).
2. If the timestamp changed, it downloads the zip and compares its SHA-256 hash against the
   last one it installed. If the hash is identical (the release was just re-published with the
   same content), it silently updates its tracked timestamp and does **not** prompt you.
3. Only a genuinely different hash triggers the install prompt.

If you decline a version, the app won't nag you about that exact version again on automatic
checks — but "Check for Updates Now" always re-prompts regardless.

## Restoring a backup

Every time the app installs a new `global.ini`, it first copies whatever was there to
`global.ini.bak` (one rolling backup, not a history). The tray menu's **Restore Backup**
item copies that file back over the current `global.ini` — it's greyed out if there's no
Star Citizen LIVE folder set or no backup file present.

Restoring also clears the app's "what's currently installed" bookkeeping, since the file on
disk no longer matches what it last applied. That means the next check (automatic or manual)
will treat the latest StarStrings release as new again and offer to reinstall it, rather than
silently thinking nothing changed.

## Updating the app itself

Separately from StarStrings content updates, the app checks GitHub for a newer release of
**itself** (`10-Gauge/StarStrings-Auto-Updater`) on the same schedule as the content check.
Unlike the StarStrings repo, this one uses ordinary semver tags, so it just reads GitHub's
normal "latest release" endpoint and compares it (Major.Minor.Build only) against the running
build's own version.

If a newer version is found, it shows a prompt with the release notes and a **Download**
button. It deliberately does **not** silently replace the running exe in place — Windows
won't let a running executable overwrite itself anyway — so instead:

1. It downloads the new version's exe to `%AppData%\StarStringsAutoUpdater\updates\`.
2. It asks whether to restart into it now. If you say yes, it launches the new exe as an
   independent process and exits — the new instance then re-registers the "start with
   Windows" registry entry to point at its own (new) path, so future logins launch the
   updated version automatically.
3. If you say no (or just close that prompt), the new exe stays in the updates folder for
   you to run whenever you like, and the app won't ask about that same version again until
   a newer one is published.

Each release's `.exe` is named with its version, e.g. `StarStringsAutoUpdater-v1.2.0.exe`,
so downloaded builds are easy to tell apart.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on a Windows,
macOS, or Linux machine (cross-compiling to Windows works from any OS; running the result
requires Windows).

```bash
dotnet publish src/StarStringsAutoUpdater/StarStringsAutoUpdater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output is a single portable `StarStringsAutoUpdater.exe` under
`src/StarStringsAutoUpdater/bin/Release/net8.0-windows/win-x64/publish/` — no .NET runtime
install required on the target machine.

A GitHub Actions workflow (`.github/workflows/build.yml`) builds this automatically on every
push and uploads it as a build artifact; pushing a `v*.*.*` tag also attaches it to a GitHub
Release.

## Running it

1. Run `StarStringsAutoUpdater.exe`. No installer is required — it's a portable single file.
2. On first launch, it asks you to locate your Star Citizen **LIVE** folder — the one containing
   `data\Localization\english\global.ini` and `user.cfg`, typically:
   ```
   C:\Program Files\Roberts Space Industries\StarCitizen\LIVE
   ```
3. It performs an initial check right away, and every 30 minutes after that (while running).
4. Right-click the tray icon any time to check manually, change the folder, or stop/start
   automatic checking.

Settings live in `%AppData%\StarStringsAutoUpdater\settings.json` — you can edit
`CheckIntervalMinutes` there if you want a different check frequency than 30 minutes (restart
the app afterward).

## Known limitations / notes

- The exe is unsigned, since no code-signing certificate is configured. Windows SmartScreen may
  warn on first run ("Windows protected your PC") — this is expected for any unsigned
  downloaded executable; click **More info → Run anyway**. If you want this to go away, you'd
  need to buy a code-signing certificate and wire it into the CI workflow.
- Only the Star Citizen **LIVE** channel is supported, matching what was asked for. The
  StarStrings project also publishes a separate PTU build (`latest-ptu` tag /
  `StarStrings-PTU.zip`) that this app deliberately ignores — its own README recommends against
  using custom strings on PTU anyway, since new builds frequently add strings the pack hasn't
  caught up with yet.
- GitHub's public API is unauthenticated here (no token), which allows 60 requests/hour per IP.
  Each check makes at most two API calls (StarStrings content + the app's own release) plus a
  download or two when something's actually new — comfortably within the limit at the default
  30-minute check interval.
