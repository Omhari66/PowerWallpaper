# PowerWallpaper (Windows 11 Battery Saver)

PowerWallpaper is a lightweight, open-source C# Windows 11 system tray utility that automatically controls your desktop wallpaper based on your laptop's power state. 

It works seamlessly alongside [Lively Wallpaper](https://github.com/rocksdanister/lively) to give you stunning live video wallpapers when your laptop is plugged in, and perfectly efficient, static OLED-friendly dark wallpapers when you switch to battery.

## Why use this?
Running live video wallpapers drastically increases GPU, CPU, and RAM usage. While Lively Wallpaper is fantastic, leaving it running on a laptop can shred your battery life. 

PowerWallpaper acts as an intelligent bridge:
* **🔌 On AC Power:** Instantly starts Lively and loads your favorite video.
* **🔋 On Battery:** Swaps your desktop to a static dark image and safely *terminates* Lively Wallpaper, reducing GPU/CPU rendering overhead to **0%**.

## Features
- **Zero Background Footprint:** The utility is built in native WinForms, taking up less than ~40MB of RAM and effectively 0% CPU.
- **Smart Debouncing:** A built-in overlapping token debouncer prevents the app from firing rapid transitions if your charging port is loose or Windows fires multiple events waking from sleep.
- **Strict Process Targeting:** Safe by default. It uses WMI to scan process paths and only terminates processes strictly originating from the Lively installation directory. It will never randomly kill your browser or media player!
- **Interactive Dashboard:** Double-click the tray icon to access a native dashboard for configuration. No need to touch JSON files manually.
- **Store-App Compatible:** Dynamically locates your Lively installation path via PowerShell (handles Windows Store `.msix` container resolution automatically).

## Requirements
- Windows 11
- .NET 10.0 Runtime
- [Lively Wallpaper](https://apps.microsoft.com/detail/9pfvd125z6jx) (Installed from Microsoft Store or Desktop)

## Installation & Setup
1. Download the latest compiled `.exe` from the Releases tab (or clone and build this repository).
2. Double-click `PowerWallpaper.exe`.
3. The Dashboard will appear. Click `Browse...` next to **Charging (Video)** and select a video on your PC.
4. Click `Browse...` next to **Battery (Image)** and select a static dark image.
5. Click **Apply Now**. 

The app will instantly hide in your system tray and take over!

## Building from Source
This project uses `.NET 10.0`. 
1. Clone this repository.
2. Navigate to the project directory: `cd PowerWallpaper`
3. Restore packages: `dotnet restore`
4. Build in release mode: `dotnet build -c Release`

## License
MIT License. Feel free to fork, modify, and distribute!
