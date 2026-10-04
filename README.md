# NEONBEAR Debloater

A dark, neon-styled Windows 11 debloater with a switch for every action. One self-contained `.exe`, no terminal window, no .NET install needed on the target PC.

> **Status:** untested. The source was written without access to a Windows machine or the .NET SDK, so the first build may need a small fix. Run it on a spare PC or make a backup first.

---

## Build

1. Install the free [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Double-click `build.bat` (or run `dotnet publish -c Release -o out`).
3. Your app is at `out\NeonBear-Debloat.exe`.

The app asks for administrator rights when it opens.

## Using it

1. Pick a page on the left and flip the switches you want. Anything switched off is left alone.
2. Optionally pick a **preset** or load a saved **profile** at the bottom.
3. Press **Apply changes**. A confirmation lists any risky steps you turned on.
4. Watch the progress bar and live log on the **Apply** page, then restart your PC.

## Pages

| Page | What you can control |
|---|---|
| **Appearance** | Dark mode, taskbar icons left, Explorer opens to This PC, wallpaper (built-in or your own), mouse acceleration off, Sticky Keys off, show file extensions, classic right-click menu |
| **AI** | Remove Copilot, disable Recall and Click to Do, no web results in Start search |
| **Apps** | Master switch plus one switch per built-in app (about 35), and a box to add your own package name (wildcards like `*Spotify*` work) |
| **Start menu** | Hide Recommended, grid view for All apps, and which apps stay pinned |
| **Privacy and security** | Telemetry, location tracking, Smart App Control, BitLocker, hibernation |
| **Performance** | Ultimate Performance power plan |
| **Software** | QuickLook (Space to preview a file), browser picker, remove Edge, extra apps by winget ID |
| **Apply** | Restore point switch, progress bar, live log, Restart PC button |

## Presets and profiles

- **Recommended** - the default set of options.
- **Light touch** - appearance and AI tweaks only.
- **Aggressive** - every switch on, including Photos, Paint, Snipping Tool and Terminal.
- **Nothing** - clears every switch.

**Save profile / Load profile** store your whole setup (switches, custom apps, browser, wallpaper path, extra apps) as a `.json` file. Your last choices are also remembered automatically in `%AppData%\NeonBear\last-profile.json`.

## What it never touches

Calculator, Clock, Notepad and the Microsoft Store are not in the removal list. Photos, Paint, Snipping Tool and Windows Terminal are listed but off by default.

## Browser step

The browser step always runs **last**:

1. The browser you picked is installed (or updated if already present) through winget.
2. Only if that succeeded, and **Remove Edge** is on, Edge is uninstalled. This is so you are never left without a browser.
3. Choose **Keep Edge** to just update it, or **Don't change my browser** to skip the step.

The Edge WebView2 runtime is kept because many apps depend on it. Set your default browser yourself in Settings > Apps > Default apps afterwards.

## Things to know before you apply

- **BitLocker:** drives get decrypted. This can take a long time and runs in the background.
- **Smart App Control:** once off, it cannot be turned back on without resetting Windows.
- **Hibernation:** turning it off also turns off Fast Startup.
- **Telemetry:** Home and Pro editions still send a minimal "required" level. That is a Windows limit.
- **Start menu:** hiding Recommended and the custom pins use policy settings that depend on your Windows edition and build. Some builds ignore them.
- **Edge removal:** Microsoft sometimes blocks the uninstall. If so, the log says so and Edge stays.
- **Restore point:** one is created first by default (switch on the Apply page). Windows can still refuse, for example if System Protection is off.
- **Run as the right user:** most settings apply to the account that launches the app, so run it from the account you actually use.

## Undoing things

Use the restore point for most changes. Removed apps can be reinstalled from the Microsoft Store. Individual settings can be flipped back in Windows Settings.

## Project layout

| File | Purpose |
|---|---|
| `Program.cs` | Entry point and admin check |
| `MainForm.cs` | The window, pages, presets and profiles |
| `Ui.cs` | Custom controls (toggle switch, cards, buttons, progress bar) and the theme |
| `Engine.cs` | The code that actually applies each tweak |
| `app.manifest` | Asks Windows for administrator rights |
| `wallpaper.png` | The NEONBEAR wallpaper, embedded into the exe |
| `build.bat` | One-click build |

## Troubleshooting

- **Build fails:** make sure the .NET 8 SDK is installed, then paste the error message so it can be fixed.
- **A setting did not change:** some need a restart or sign-out. Check the log for lines starting with `!`.
- **winget not found:** install or update "App Installer" from the Microsoft Store. QuickLook, browsers and extra apps need it.

Use at your own risk.
