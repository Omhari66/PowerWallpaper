# ⚡ PowerWallpaper

> **Automatic live ↔ static wallpaper switching based on your laptop's power state.**

[![Download Latest Release](https://img.shields.io/github/v/release/Omhari66/PowerWallpaper?style=for-the-badge&label=Download%20Latest%20Release)](https://github.com/Omhari66/PowerWallpaper/releases/latest)

PowerWallpaper is a lightweight, open-source C# Windows 11 system tray utility that works alongside [Lively Wallpaper](https://github.com/rocksdanister/lively) to give you stunning live video wallpapers when plugged in — and a battery-saving static wallpaper the moment you unplug.

---

## How it works

| Power State | Action |
|---|---|
| 🔌 **Plugged in (AC)** | Starts Lively, loads your video wallpaper — within ~1s |
| 🔋 **Unplugged (Battery)** | Terminates Lively + mpv completely, sets static image — within ~0.5s |

It hooks into the Windows `PowerModeChanged` event, so the switch happens **instantly** without any polling loops.

---

## Why use this?

Running live video wallpapers on battery is brutal for performance. Lively Wallpaper uses a dedicated `mpv` video renderer that runs at all times.

With PowerWallpaper:
- **RAM saved:** ~450MB (Lively + mpv fully terminated on battery)
- **GPU/CPU overhead:** Drops to **0%** on battery
- **App overhead:** `< 40MB RAM`, `~0% CPU` (native WinForms, no Electron)

---

## Features

- ⚡ **Sub-second switching** — non-blocking background threads, 500ms debounce
- 🎯 **Strict process targeting** — WMI scan kills only processes from Lively's install directory, never random system processes
- 🔁 **Smart startup detection** — polls until Lively is actually ready before sending the wallpaper command (no more race conditions)
- 🛡️ **Crash-resilient** — global exception handler logs all failures, AbandonedMutex recovery, path-anchored config
- 🖥️ **Interactive Dashboard** — double-click tray icon to open; browse wallpapers, test modes, toggle autostart
- 🗂️ **Store-App Compatible** — finds Lively via PowerShell AppX query or falls back to the desktop installer path

---

## Requirements

- Windows 11
- [Lively Wallpaper](https://apps.microsoft.com/detail/9pfvd125z6jx) (Microsoft Store or desktop installer)

*(Note: The downloadable `.exe` is completely self-contained. You do **not** need to install the .NET runtime!)*

---

## Installation & Setup

1. Go to the [Releases](https://github.com/Omhari66/PowerWallpaper/releases/latest) page and download `PowerWallpaper_v1.0.0.zip`.
2. Extract the `.zip` file and double-click `PowerWallpaper.exe` to launch.  
   > **Note:** If Windows Smart App Control blocks it, right-click → Properties → check **Unblock**, or add your folder to Windows Defender exclusions.

3. The Dashboard opens on first launch. Configure your wallpapers:
   - **Charging (Video):** Select any `.mp4` file for your live wallpaper
   - **Battery (Image):** Select a static `.jpg`/`.png` (dark/OLED-friendly recommended)

4. Check **Start with Windows** so it runs silently in the tray on every boot.

5. Click **Apply Now** and you're done.

---

## Usage

| Action | Result |
|---|---|
| Double-click tray icon | Opens Dashboard |
| Right-click tray icon | Quick menu — Test Battery Mode, Test Charging Mode, Pause Automation, Exit |
| **Test Battery Mode** | Forces static wallpaper + kills Lively (simulate unplug) |
| **Test Charging Mode** | Forces live wallpaper + starts Lively (simulate plug-in) |
| Pause Automation | Freezes switching — useful when watching video on battery |

---

## Building from Source

```bash
git clone https://github.com/Omhari66/PowerWallpaper.git
cd PowerWallpaper/PowerWallpaper
dotnet build -c Release
# Output: bin/Release/net10.0-windows/PowerWallpaper.exe
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

---

## Troubleshooting

| Problem | Fix |
|---|---|
| App doesn't start / nothing in tray | Check `power-wallpaper.log` next to the `.exe` for the error |
| Live wallpaper not loading after plug-in | Lively may be slow to start on your machine — wait 5–10 seconds the first time |
| Static wallpaper not showing on unplug | Make sure the image path in Dashboard exists; browse to re-select it |
| "Another instance is running" | Open Task Manager, find `PowerWallpaper`, end it, relaunch |
| Smart App Control blocking | Right-click exe → Properties → Unblock, or add folder to Defender exclusions |

---

## Architecture

```
PowerWallpaper.exe (WinForms, STA thread)
├── Program.cs          — Single-instance Mutex, global exception handler
├── AppContext.cs       — System tray, tray menu, dashboard lifecycle
├── MainForm.cs         — Dashboard UI, status display, manual test buttons
├── PowerMonitor.cs     — SystemEvents.PowerModeChanged hook, 500ms debounce
├── WallpaperController.cs — Lively process management (background threads)
├── ConfigManager.cs    — JSON config, anchored to exe directory
└── Logger.cs           — Append log, anchored to exe directory
```

---

## License

MIT — fork, modify, distribute freely.
