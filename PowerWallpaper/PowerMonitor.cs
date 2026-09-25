using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Forms;

namespace PowerWallpaper
{
    public enum PowerState
    {
        UNKNOWN,
        AC,
        BATTERY
    }

    public enum WallpaperState
    {
        UNKNOWN,
        LIVE,
        DARK
    }

    public class PowerMonitor
    {
        private readonly Config _config;
        public PowerState CurrentPowerState { get; private set; } = PowerState.UNKNOWN;
        
        private readonly object _debounceLock = new object();
        private int _debounceToken = 0;
        private const int DebounceMs = 500;

        public PowerMonitor(Config config)
        {
            _config = config;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        public void Start()
        {
            Logger.Log("Starting power monitor...");
            // Initial check on startup
            DetectAndApplyState();

            // Schedule a boot enforcer that polls for 60s to catch Lively if it starts late
            // via its own "Start with Windows" setting. Without this, Lively
            // can start itself 20-30s into boot and appear on battery.
            Task.Run(async () =>
            {
                for (int i = 0; i < 12; i++)
                {
                    await Task.Delay(5000);
                    var status = SystemInformation.PowerStatus.PowerLineStatus;
                    bool onBattery = status != PowerLineStatus.Online;
                    if (onBattery && WallpaperController.IsLivelyRunning())
                    {
                        Logger.Log($"Boot enforcer ({i+1}/12): Lively started on battery after initial check. Killing it.");
                        WallpaperController.SetWindowsStaticWallpaper(_config.BatteryWallpaper);
                        WallpaperController.KillLivelySafely();
                        break;
                    }
                }
                Logger.Log("Boot enforcer: finished checking.");
            });
        }

        public void Stop()
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            Logger.Log($"OS Power Event Received: {e.Mode}");
            if (e.Mode == PowerModes.StatusChange || e.Mode == PowerModes.Resume)
            {
                TriggerDebounce();
            }
        }

        private void TriggerDebounce()
        {
            int currentToken;
            lock (_debounceLock)
            {
                _debounceToken++;
                currentToken = _debounceToken;
            }

            Task.Run(async () =>
            {
                await Task.Delay(DebounceMs);

                lock (_debounceLock)
                {
                    if (currentToken != _debounceToken)
                    {
                        // A newer event arrived, cancel this evaluation
                        return;
                    }
                }

                DetectAndApplyState();
            });
        }

        public void DetectAndApplyState()
        {
            if (_config.PauseAutomation)
            {
                Logger.Log("Automation paused. Ignoring state change.");
                return;
            }

            var actualStatus = SystemInformation.PowerStatus.PowerLineStatus;
            PowerState detectedState = actualStatus == PowerLineStatus.Online ? PowerState.AC : PowerState.BATTERY;

            Logger.Log($"Actual Power State Detected: {detectedState}");

            if (detectedState == CurrentPowerState)
            {
                Logger.Log("State unchanged. No action required.");
                // Ensure initial wallpaper state is set even if power state "hasn't changed" on startup
                if (WallpaperController.CurrentWallpaperStatus == WallpaperStatus.Unknown)
                {
                    ApplyState(detectedState);
                }
                return;
            }

            CurrentPowerState = detectedState;
            ApplyState(detectedState);
        }

        public void TestAcMode()
        {
            Logger.Log("MANUAL TEST: AC Mode");
            CurrentPowerState = PowerState.AC;
            ApplyAcMode(true);
        }

        public void TestBatteryMode()
        {
            Logger.Log("MANUAL TEST: Battery Mode");
            CurrentPowerState = PowerState.BATTERY;
            ApplyBatteryMode(true);
        }

        private void ApplyState(PowerState state)
        {
            if (state == PowerState.BATTERY)
            {
                ApplyBatteryMode(false);
            }
            else if (state == PowerState.AC)
            {
                ApplyAcMode(false);
            }
        }

        private void ApplyBatteryMode(bool force)
        {
            if (!force && WallpaperController.CurrentWallpaperStatus == WallpaperStatus.DarkActive) return;

            Logger.Log("Executing Battery Transition Sequence...");

            // 1. Set Windows Dark Wallpaper
            bool wpSet = WallpaperController.SetWindowsStaticWallpaper(_config.BatteryWallpaper);
            if (!wpSet)
            {
                Logger.Log("Warning: Failed to set static wallpaper.");
            }

            // 2. Safely close Lively
            WallpaperController.KillLivelySafely();

            Logger.Log("Battery mode transition complete.");
        }

        private void ApplyAcMode(bool force)
        {
            if (!force && WallpaperController.CurrentWallpaperStatus == WallpaperStatus.LiveActive) return;

            Logger.Log("Executing AC Transition Sequence...");
            
            // 1 & 2. Check/Start Lively and set live wallpaper
            WallpaperController.StartLively(_config.ChargingWallpaper);

            Logger.Log("AC mode transition complete.");
        }
    }
}
