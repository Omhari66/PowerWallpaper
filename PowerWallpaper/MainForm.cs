using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace PowerWallpaper
{
    public partial class MainForm : Form
    {
        private readonly Config _config;
        private readonly PowerMonitor _powerMonitor;
        private System.Windows.Forms.Timer _uiTimer;
        
        private Label lblPowerState;
        private Label lblWallpaperState;
        private Label lblLivelyState;
        
        private TextBox txtCharging;
        private TextBox txtBattery;
        
        private CheckBox chkStartWithWindows;
        private CheckBox chkPauseAutomation;

        public MainForm(Config config, PowerMonitor powerMonitor)
        {
            _config = config;
            _powerMonitor = powerMonitor;
            
            InitializeComponent();
            ApplyNativeStyling();
            
            LoadConfigToUI();
            
            _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
            
            // Initial update
            WallpaperController.UpdateLivelyStatus();
            UpdateStatusLabels();
        }

        private void InitializeComponent()
        {
            this.Text = "PowerWallpaper Dashboard";
            this.Size = new Size(420, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Information;

            int y = 20;

            // SECTION 1: Status
            var grpStatus = new GroupBox { Text = "Current Status", Location = new Point(15, y), Size = new Size(375, 100) };
            
            grpStatus.Controls.Add(new Label { Text = "Power:", Location = new Point(15, 25), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) });
            lblPowerState = new Label { Location = new Point(120, 25), AutoSize = true };
            grpStatus.Controls.Add(lblPowerState);

            grpStatus.Controls.Add(new Label { Text = "Wallpaper:", Location = new Point(15, 50), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) });
            lblWallpaperState = new Label { Location = new Point(120, 50), AutoSize = true };
            grpStatus.Controls.Add(lblWallpaperState);

            grpStatus.Controls.Add(new Label { Text = "Lively Engine:", Location = new Point(15, 75), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) });
            lblLivelyState = new Label { Location = new Point(120, 75), AutoSize = true };
            grpStatus.Controls.Add(lblLivelyState);
            
            this.Controls.Add(grpStatus);
            y += 115;

            // SECTION 2: Config
            var grpConfig = new GroupBox { Text = "Wallpaper Configuration", Location = new Point(15, y), Size = new Size(375, 120) };
            
            grpConfig.Controls.Add(new Label { Text = "Charging (Video):", Location = new Point(15, 25), AutoSize = true });
            txtCharging = new TextBox { Location = new Point(15, 45), Size = new Size(270, 20) };
            var btnBrowseCharging = new Button { Text = "Browse...", Location = new Point(290, 43), Size = new Size(70, 24) };
            btnBrowseCharging.Click += (s, e) => BrowseFile(txtCharging, "Video Files|*.mp4;*.webm;*.avi|All Files|*.*");
            grpConfig.Controls.Add(txtCharging);
            grpConfig.Controls.Add(btnBrowseCharging);

            grpConfig.Controls.Add(new Label { Text = "Battery (Image):", Location = new Point(15, 75), AutoSize = true });
            txtBattery = new TextBox { Location = new Point(15, 95), Size = new Size(270, 20) };
            var btnBrowseBattery = new Button { Text = "Browse...", Location = new Point(290, 93), Size = new Size(70, 24) };
            btnBrowseBattery.Click += (s, e) => BrowseFile(txtBattery, "Image Files|*.jpg;*.png;*.bmp|All Files|*.*");
            grpConfig.Controls.Add(txtBattery);
            grpConfig.Controls.Add(btnBrowseBattery);

            this.Controls.Add(grpConfig);
            y += 135;

            // SECTION 3: Settings
            var grpSettings = new GroupBox { Text = "Settings", Location = new Point(15, y), Size = new Size(375, 55) };
            chkStartWithWindows = new CheckBox { Text = "Start with Windows", Location = new Point(15, 25), AutoSize = true };
            chkPauseAutomation = new CheckBox { Text = "Pause Automation", Location = new Point(160, 25), AutoSize = true };
            grpSettings.Controls.Add(chkStartWithWindows);
            grpSettings.Controls.Add(chkPauseAutomation);
            this.Controls.Add(grpSettings);
            y += 70;

            // SECTION 4: Test
            var btnTestAC = new Button { Text = "Test Charging Mode", Location = new Point(15, y), Size = new Size(130, 30) };
            btnTestAC.Click += (s, e) => _powerMonitor.TestAcMode();
            var btnTestBat = new Button { Text = "Test Battery Mode", Location = new Point(155, y), Size = new Size(130, 30) };
            btnTestBat.Click += (s, e) => _powerMonitor.TestBatteryMode();
            this.Controls.Add(btnTestAC);
            this.Controls.Add(btnTestBat);
            y += 45;

            // SECTION 5: Actions
            var btnSave = new Button { Text = "Save Settings", Location = new Point(135, y), Size = new Size(120, 30) };
            btnSave.Click += (s, e) => SaveConfig();
            var btnApply = new Button { Text = "Apply Now", Location = new Point(270, y), Size = new Size(120, 30), BackColor = SystemColors.Highlight, ForeColor = Color.White };
            btnApply.Click += (s, e) => { SaveConfig(); _powerMonitor.DetectAndApplyState(); };
            
            this.Controls.Add(btnSave);
            this.Controls.Add(btnApply);
        }

        private void ApplyNativeStyling()
        {
            this.BackColor = Color.FromArgb(245, 245, 245);
            this.Font = new Font("Segoe UI", 9F);
            
            foreach (Control c in this.Controls)
            {
                if (c is Button btn)
                {
                    btn.FlatStyle = FlatStyle.System;
                }
            }
        }

        private void LoadConfigToUI()
        {
            txtCharging.Text = _config.ChargingWallpaper;
            txtBattery.Text = _config.BatteryWallpaper;
            chkStartWithWindows.Checked = _config.StartWithWindows;
            chkPauseAutomation.Checked = _config.PauseAutomation;
        }

        private void SaveConfig()
        {
            _config.ChargingWallpaper = txtCharging.Text;
            _config.BatteryWallpaper = txtBattery.Text;
            _config.StartWithWindows = chkStartWithWindows.Checked;
            _config.PauseAutomation = chkPauseAutomation.Checked;
            
            ConfigManager.Save(_config);
            ManageRegistryAutostart();
        }

        private void ManageRegistryAutostart()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (_config.StartWithWindows)
                    {
                        key?.SetValue("PowerWallpaper", $"\"{Application.ExecutablePath}\" --autostart");
                    }
                    else
                    {
                        key?.DeleteValue("PowerWallpaper", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to set registry key: {ex.Message}");
            }
        }

        private void BrowseFile(TextBox target, string filter)
        {
            using (var ofd = new OpenFileDialog { Filter = filter })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    target.Text = ofd.FileName;
                }
            }
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            WallpaperController.UpdateLivelyStatus();
            UpdateStatusLabels();
        }

        private void UpdateStatusLabels()
        {
            // Power
            lblPowerState.Text = _powerMonitor.CurrentPowerState == PowerState.AC ? "🔌 AC Power" :
                                 _powerMonitor.CurrentPowerState == PowerState.BATTERY ? "🔋 Battery" : "Unknown";

            // Wallpaper
            switch (WallpaperController.CurrentWallpaperStatus)
            {
                case WallpaperStatus.LiveActive: lblWallpaperState.Text = "Live Wallpaper Active"; break;
                case WallpaperStatus.DarkActive: lblWallpaperState.Text = "Dark Wallpaper Active"; break;
                case WallpaperStatus.Transitioning: lblWallpaperState.Text = "Transitioning..."; break;
                case WallpaperStatus.Error: lblWallpaperState.Text = "Error"; break;
                default: lblWallpaperState.Text = "Unknown"; break;
            }

            // Lively
            switch (WallpaperController.CurrentLivelyStatus)
            {
                case LivelyStatus.Running: lblLivelyState.Text = "Running"; lblLivelyState.ForeColor = Color.Green; break;
                case LivelyStatus.NotRunning: lblLivelyState.Text = "Not Running"; lblLivelyState.ForeColor = Color.DarkGray; break;
                case LivelyStatus.Starting: lblLivelyState.Text = "Starting..."; lblLivelyState.ForeColor = Color.Orange; break;
                case LivelyStatus.Stopping: lblLivelyState.Text = "Stopping..."; lblLivelyState.ForeColor = Color.Orange; break;
                case LivelyStatus.Error: lblLivelyState.Text = "Error"; lblLivelyState.ForeColor = Color.Red; break;
                default: lblLivelyState.Text = "Unknown"; lblLivelyState.ForeColor = Color.Black; break;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide(); // Minimize to tray
            }
            base.OnFormClosing(e);
        }
    }
}
