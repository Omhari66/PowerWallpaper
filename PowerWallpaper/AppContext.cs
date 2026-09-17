using System;
using System.Drawing;
using System.Windows.Forms;

namespace PowerWallpaper
{
    public class PowerWallpaperContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly MainForm _mainForm;
        private readonly PowerMonitor _powerMonitor;
        private readonly Config _config;

        public PowerWallpaperContext(Config config, PowerMonitor powerMonitor, bool isAutostart)
        {
            _config = config;
            _powerMonitor = powerMonitor;
            
            _mainForm = new MainForm(config, powerMonitor);
            
            var menu = new ContextMenuStrip();

            var statusItem = new ToolStripMenuItem("Status: Unknown") { Enabled = false };
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(new ToolStripMenuItem("Open Dashboard", null, (s, e) => ShowDashboard()));
            
            var pauseItem = new ToolStripMenuItem(_config.PauseAutomation ? "Resume Automation" : "Pause Automation", null, OnTogglePause);
            menu.Items.Add(pauseItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Test Battery Mode", null, (s, e) => _powerMonitor.TestBatteryMode()));
            menu.Items.Add(new ToolStripMenuItem("Test Charging Mode", null, (s, e) => _powerMonitor.TestAcMode()));
            
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                ContextMenuStrip = menu,
                Visible = true,
                Text = "Power Wallpaper Utility"
            };
            
            _trayIcon.DoubleClick += (s, e) => ShowDashboard();
            
            UpdateTrayStatusUI(statusItem, pauseItem);
            
            // Show dashboard if manually launched by the user (not autostarted by Windows)
            if (!isAutostart)
            {
                ShowDashboard();
            }
        }
        
        private void ShowDashboard()
        {
            _mainForm.Show();
            if (_mainForm.WindowState == FormWindowState.Minimized)
            {
                _mainForm.WindowState = FormWindowState.Normal;
            }
            _mainForm.Activate();
        }

        private void UpdateTrayStatusUI(ToolStripMenuItem statusItem, ToolStripMenuItem pauseItem)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, e) =>
            {
                var state = _powerMonitor.CurrentPowerState;
                string stateStr = state == PowerState.AC ? "AC Power" : state == PowerState.BATTERY ? "Battery" : "Unknown";
                
                string wpStr = WallpaperController.CurrentWallpaperStatus == WallpaperStatus.LiveActive ? "Live" :
                               WallpaperController.CurrentWallpaperStatus == WallpaperStatus.DarkActive ? "Dark" :
                               WallpaperController.CurrentWallpaperStatus == WallpaperStatus.Transitioning ? "Transitioning..." : "";
                
                statusItem.Text = $"[{stateStr}] {wpStr}";
                
                pauseItem.Text = _config.PauseAutomation ? "Resume Automation" : "Pause Automation";
            };
            timer.Start();
        }

        private void OnTogglePause(object sender, EventArgs e)
        {
            _config.PauseAutomation = !_config.PauseAutomation;
            ConfigManager.Save(_config);

            if (!_config.PauseAutomation)
            {
                _powerMonitor.DetectAndApplyState();
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _powerMonitor.Stop();
            Application.Exit();
        }
    }
}
