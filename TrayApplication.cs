using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Reflection;
using AudioSwitcher.Interop;

namespace AudioSwitcher
{
    /// <summary>
    /// Main application class with system tray UI
    /// </summary>
    public class TrayApplication : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _contextMenu;
        private AudioDeviceManager _audioManager;
        private DeviceMonitor _deviceMonitor;  // Windows native device monitor
        private HidPPMonitor _hidppMonitor;    // HID++ protocol monitor for wireless headset
        private AppConfig _config;

        private ToolStripMenuItem _playbackMenu;
        private ToolStripMenuItem _defaultDeviceMenu;  // Submenu for setting default device
        private ToolStripMenuItem _recordingMenu;
        private ToolStripMenuItem _statusMenuItem;
        private ToolStripMenuItem _autoSwitchMenuItem;
        private ToolStripMenuItem _startupMenuItem;
        private ToolStripMenuItem _settingsMenu;
        private ToolStripMenuItem _languageMenu;
        private ToolStripMenuItem _exitItem;
        private ToolStripMenuItem _showNotificationsItem;

        private bool _lastKnownHeadsetState = false;
        private string _lastHeadsetDeviceId = null;  // Track headset device ID

        public TrayApplication()
        {
            // Load configuration
            _config = AppConfig.Load();

            // Initialize localization based on config
            InitializeLocalization();

            // Initialize audio manager
            _audioManager = new AudioDeviceManager();

            // Initialize tray icon and menu
            InitializeTrayIcon();

            // Start monitoring (Windows native and HID++ for wireless headset)
            InitializeDeviceMonitor();
            InitializeHidPPMonitor();  // Primary method for detecting G PRO X 2 headset on/off
        }

        /// <summary>
        /// Initialize HID++ monitor for detecting wireless headset connection state
        /// This is the primary method for detecting G PRO X 2 headset on/off
        /// </summary>
        private void InitializeHidPPMonitor()
        {
            _hidppMonitor = new HidPPMonitor();
            
            _hidppMonitor.OnHeadsetConnectionChanged += (s, e) =>
            {
                Log($"HID++ Event: Headset {(e.IsConnected ? "CONNECTED" : "DISCONNECTED")} (DeviceIndex: {e.DeviceIndex}, Name: {e.DeviceName ?? "Unknown"})");
                
                // This is the most reliable detection for wireless headset
                bool isConnected = e.IsConnected;
                
                // Avoid duplicate events
                if (isConnected == _lastKnownHeadsetState) return;
                _lastKnownHeadsetState = isConnected;
                
                // Invoke on UI thread
                if (_trayIcon.ContextMenuStrip.InvokeRequired)
                {
                    _trayIcon.ContextMenuStrip.Invoke(new Action(() => HandleHeadsetStateChange(isConnected)));
                }
                else
                {
                    HandleHeadsetStateChange(isConnected);
                }
            };
            
            // Try to connect to receiver
            if (_hidppMonitor.Connect())
            {
                Log("HID++ Monitor connected to G PRO X 2 receiver");
                _hidppMonitor.StartMonitoring();
                Log("HID++ Monitor started - listening for headset connection events");
                
                // Check initial state
                _lastKnownHeadsetState = _hidppMonitor.IsHeadsetConnected;
                
                // Update status to show HID++ monitoring is active
                UpdateStatus(false);
            }
            else
            {
                Log("HID++ Monitor: Could not connect to G PRO X 2 receiver (G HUB may have exclusive access)");
                // Update status to show basic monitoring
                UpdateStatus(false);
            }
        }

        private void InitializeDeviceMonitor()
        {
            _deviceMonitor = new DeviceMonitor();

            _deviceMonitor.OnDeviceStateChanged += (s, e) =>
            {
                Log($"Device state changed: {e.DeviceId} -> {e.NewState}");
                
                // Check if this is our headset
                if (IsHeadsetDevice(e.DeviceId))
                {
                    bool isConnected = e.NewState == DeviceState.Active;
                    HandleDeviceStateChange(e.DeviceId, isConnected);
                }
            };

            _deviceMonitor.OnDeviceAdded += (s, deviceId) =>
            {
                Log($"Device added: {deviceId}");
                
                // Check if newly added device is our headset
                if (IsHeadsetDevice(deviceId))
                {
                    _lastHeadsetDeviceId = deviceId;
                    HandleDeviceStateChange(deviceId, true);
                }
            };

            _deviceMonitor.OnDeviceRemoved += (s, deviceId) =>
            {
                Log($"Device removed: {deviceId}");
                
                // Check if removed device was our headset
                if (deviceId == _lastHeadsetDeviceId || IsHeadsetDevice(deviceId))
                {
                    HandleDeviceStateChange(deviceId, false);
                }
            };

            _deviceMonitor.Start();
            Log("Windows device monitor started");
            
            // Find initial headset device ID
            var headset = _audioManager.FindPlaybackDevice(_config.HeadsetKeyword);
            if (headset != null)
            {
                _lastHeadsetDeviceId = headset.Id;
                _lastKnownHeadsetState = true;
                Log($"Found headset on startup: {headset.FriendlyName}");
            }
        }

        private bool IsHeadsetDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(_config.HeadsetKeyword))
                return false;

            // Check if device ID contains headset keyword (case-insensitive)
            if (deviceId.IndexOf(_config.HeadsetKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Try to get device name and check
            try
            {
                var devices = _audioManager.GetPlaybackDevices();
                var device = devices.Find(d => d.Id == deviceId);
                if (device != null && device.FriendlyName != null)
                {
                    return device.FriendlyName.IndexOf(_config.HeadsetKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { }

            return false;
        }

        private void HandleDeviceStateChange(string deviceId, bool isConnected)
        {
            // Avoid duplicate events
            if (isConnected == _lastKnownHeadsetState) return;
            _lastKnownHeadsetState = isConnected;

            Log($"Headset state: {(isConnected ? "CONNECTED" : "DISCONNECTED")} (via Windows API)");

            // Invoke on UI thread
            if (_trayIcon.ContextMenuStrip.InvokeRequired)
            {
                _trayIcon.ContextMenuStrip.Invoke(new Action(() => HandleHeadsetStateChange(isConnected)));
            }
            else
            {
                HandleHeadsetStateChange(isConnected);
            }
        }

        private void InitializeLocalization()
        {
            // Set language from config
            switch (_config.Language?.ToLower())
            {
                case "english":
                    Localization.CurrentLanguage = Language.English;
                    break;
                case "chinese":
                    Localization.CurrentLanguage = Language.Chinese;
                    break;
                default:
                    Localization.CurrentLanguage = Language.Auto;
                    break;
            }
        }

        private void InitializeTrayIcon()
        {
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Opening += (s, e) => RefreshDeviceMenus();

            // Status item (non-clickable header)
            _statusMenuItem = new ToolStripMenuItem(L.Get("Status_Connecting"))
            {
                Enabled = false
            };
            _contextMenu.Items.Add(_statusMenuItem);
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Playback devices placeholder - will be populated in RefreshDeviceMenus
            // This invisible item marks the insertion point for playback devices
            _playbackMenu = new ToolStripMenuItem("_playback_placeholder_") { Visible = false };
            _contextMenu.Items.Add(_playbackMenu);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Default device submenu (for setting fallback when headset disconnects)
            _defaultDeviceMenu = new ToolStripMenuItem(L.Get("Menu_SetDefaultDevice"));
            _contextMenu.Items.Add(_defaultDeviceMenu);

            // Recording devices submenu
            _recordingMenu = new ToolStripMenuItem(L.Get("Menu_RecordingDevices"));
            _contextMenu.Items.Add(_recordingMenu);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Auto-switch toggle
            _autoSwitchMenuItem = new ToolStripMenuItem(L.Get("Menu_AutoSwitch"))
            {
                Checked = _config.AutoSwitchOnConnect,
                CheckOnClick = true
            };
            _autoSwitchMenuItem.CheckedChanged += (s, e) =>
            {
                _config.AutoSwitchOnConnect = _autoSwitchMenuItem.Checked;
                _config.AutoSwitchOnDisconnect = _autoSwitchMenuItem.Checked;
                _config.Save();
            };
            _contextMenu.Items.Add(_autoSwitchMenuItem);

            // Start with Windows toggle
            _startupMenuItem = new ToolStripMenuItem(L.Get("Menu_StartWithWindows"))
            {
                Checked = AppConfig.IsStartWithWindowsEnabled(),
                CheckOnClick = true
            };
            _startupMenuItem.CheckedChanged += (s, e) =>
            {
                string exePath = Application.ExecutablePath;
                AppConfig.SetStartWithWindows(_startupMenuItem.Checked, exePath);
                _config.StartWithWindows = _startupMenuItem.Checked;
                _config.Save();
            };
            _contextMenu.Items.Add(_startupMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Settings submenu
            _settingsMenu = new ToolStripMenuItem(L.Get("Menu_Settings"));

            _showNotificationsItem = new ToolStripMenuItem(L.Get("Menu_ShowNotifications"))
            {
                Checked = _config.ShowNotifications,
                CheckOnClick = true
            };
            _showNotificationsItem.CheckedChanged += (s, e) =>
            {
                _config.ShowNotifications = _showNotificationsItem.Checked;
                _config.Save();
            };
            _settingsMenu.DropDownItems.Add(_showNotificationsItem);

            _settingsMenu.DropDownItems.Add(new ToolStripSeparator());

            // Language submenu
            _languageMenu = new ToolStripMenuItem(L.Get("Menu_Language"));
            BuildLanguageMenu();
            _settingsMenu.DropDownItems.Add(_languageMenu);

            _contextMenu.Items.Add(_settingsMenu);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            _exitItem = new ToolStripMenuItem(L.Get("Menu_Exit"));
            _exitItem.Click += OnExit;
            _contextMenu.Items.Add(_exitItem);

            // Create tray icon
            _trayIcon = new NotifyIcon
            {
                Icon = GetEmbeddedIcon() ?? SystemIcons.Application,
                ContextMenuStrip = _contextMenu,
                Visible = true,
                Text = L.Get("TrayTip_Default")
            };

            // Double-click to show current device
            _trayIcon.DoubleClick += (s, e) =>
            {
                var playback = _audioManager.GetDefaultDevice(EDataFlow.eRender);
                var recording = _audioManager.GetDefaultDevice(EDataFlow.eCapture);
                ShowBalloon(L.Get("Notify_CurrentDevices"),
                    $"▶ {playback?.FriendlyName ?? "None"}\n" +
                    $"● {recording?.FriendlyName ?? "None"}");
            };
        }

        private void BuildLanguageMenu()
        {
            _languageMenu.DropDownItems.Clear();

            var currentLang = _config.Language?.ToLower() ?? "auto";

            // Auto (System)
            var autoItem = new ToolStripMenuItem(L.Get("Menu_LangAuto"))
            {
                Checked = currentLang == "auto" || string.IsNullOrEmpty(_config.Language),
                Tag = "Auto"
            };
            autoItem.Click += OnLanguageSelected;
            _languageMenu.DropDownItems.Add(autoItem);

            // English
            var englishItem = new ToolStripMenuItem(L.Get("Menu_LangEnglish"))
            {
                Checked = currentLang == "english",
                Tag = "English"
            };
            englishItem.Click += OnLanguageSelected;
            _languageMenu.DropDownItems.Add(englishItem);

            // Chinese
            var chineseItem = new ToolStripMenuItem(L.Get("Menu_LangChinese"))
            {
                Checked = currentLang == "chinese",
                Tag = "Chinese"
            };
            chineseItem.Click += OnLanguageSelected;
            _languageMenu.DropDownItems.Add(chineseItem);
        }

        private void OnLanguageSelected(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            if (item?.Tag == null) return;

            string newLang = item.Tag.ToString();
            _config.Language = newLang;
            _config.Save();

            // Update localization
            switch (newLang.ToLower())
            {
                case "english":
                    Localization.CurrentLanguage = Language.English;
                    break;
                case "chinese":
                    Localization.CurrentLanguage = Language.Chinese;
                    break;
                default:
                    Localization.CurrentLanguage = Language.Auto;
                    break;
            }

            // Refresh all menu items
            RefreshMenuTexts();

            ShowBalloon(L.Get("Notify_LanguageChanged"), L.Get("Notify_RestartRequired"));
        }

        private void RefreshMenuTexts()
        {
            // Update all menu item texts based on HID++ monitor status
            if (_hidppMonitor?.IsMonitoring == true)
            {
                _statusMenuItem.Text = _hidppMonitor.IsHeadsetConnected 
                    ? L.Get("Status_HeadsetOn")
                    : L.Get("Status_HeadsetOff");
            }
            else
            {
                _statusMenuItem.Text = L.Get("Status_Monitoring");
            }

            _playbackMenu.Text = L.Get("Menu_PlaybackDevices");
            _recordingMenu.Text = L.Get("Menu_RecordingDevices");
            _autoSwitchMenuItem.Text = L.Get("Menu_AutoSwitch");
            _startupMenuItem.Text = L.Get("Menu_StartWithWindows");
            _settingsMenu.Text = L.Get("Menu_Settings");
            _showNotificationsItem.Text = L.Get("Menu_ShowNotifications");
            _languageMenu.Text = L.Get("Menu_Language");
            _exitItem.Text = L.Get("Menu_Exit");

            _trayIcon.Text = _hidppMonitor?.IsMonitoring == true
                ? (_hidppMonitor.IsHeadsetConnected ? L.Get("TrayTip_HeadsetOn") : L.Get("TrayTip_HeadsetOff"))
                : L.Get("TrayTip_Default");

            // Rebuild language menu to update checkmarks
            BuildLanguageMenu();
        }

        private Icon GetEmbeddedIcon()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("AudioSwitcher.Assets.icon.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream);
                    }
                }
            }
            catch { }
            return null;
        }

        private void RefreshDeviceMenus()
        {
            RefreshPlaybackDevices();
            RefreshRecordingDevices();
        }

        private void RefreshPlaybackDevices()
        {
            // Find the placeholder position and remove old playback device items
            int placeholderIndex = _contextMenu.Items.IndexOf(_playbackMenu);
            if (placeholderIndex < 0) return;
            
            // Remove old playback device items
            var itemsToRemove = new System.Collections.Generic.List<ToolStripItem>();
            for (int i = 0; i < _contextMenu.Items.Count; i++)
            {
                var menuItem = _contextMenu.Items[i] as ToolStripMenuItem;
                if (menuItem?.Tag is string tag && tag == "playback_device")
                {
                    itemsToRemove.Add(menuItem);
                }
            }
            foreach (var item in itemsToRemove)
            {
                _contextMenu.Items.Remove(item);
            }
            
            // Re-find placeholder index after removals
            placeholderIndex = _contextMenu.Items.IndexOf(_playbackMenu);

            var devices = _audioManager.GetPlaybackDevices();
            var currentDefault = _audioManager.GetDefaultDevice(EDataFlow.eRender);

            // === Part 1: Quick switch list (top level menu) ===
            int insertIndex = placeholderIndex;
            foreach (var device in devices)
            {
                bool isCurrentDefault = device.Id == currentDefault?.Id;
                bool isConfiguredDefault = device.Id == _config.DefaultPlaybackDeviceId;
                
                string displayName = device.FriendlyName;
                if (isConfiguredDefault)
                {
                    displayName += " " + L.Get("Menu_DefaultMarker");
                }

                var item = new ToolStripMenuItem(displayName)
                {
                    Checked = isCurrentDefault,
                    Tag = "playback_device"
                };
                
                // Store device in a local variable for closure
                var deviceCopy = device;

                // Direct click switches to this device
                item.Click += (s, e) =>
                {
                    if (_audioManager.SetDefaultDeviceAllRoles(deviceCopy.Id))
                    {
                        ShowBalloon(L.Get("Notify_PlaybackChanged"), deviceCopy.FriendlyName);
                    }
                };

                _contextMenu.Items.Insert(insertIndex++, item);
            }

            if (devices.Count == 0)
            {
                var noDevices = new ToolStripMenuItem(L.Get("Menu_NoDevices")) { Enabled = false, Tag = "playback_device" };
                _contextMenu.Items.Insert(insertIndex, noDevices);
            }
            
            // === Part 2: Default device submenu ===
            _defaultDeviceMenu.DropDownItems.Clear();
            foreach (var device in devices)
            {
                bool isConfiguredDefault = device.Id == _config.DefaultPlaybackDeviceId;
                
                var setDefaultItem = new ToolStripMenuItem(device.FriendlyName)
                {
                    Checked = isConfiguredDefault
                };
                
                var deviceCopy = device;
                setDefaultItem.Click += (s, e) =>
                {
                    _config.DefaultPlaybackDeviceId = deviceCopy.Id;
                    _config.DefaultPlaybackDeviceName = deviceCopy.FriendlyName;
                    _config.SpeakersKeyword = deviceCopy.FriendlyName;
                    _config.Save();
                    ShowBalloon(L.Get("Notify_DefaultSet"), deviceCopy.FriendlyName);
                };
                
                _defaultDeviceMenu.DropDownItems.Add(setDefaultItem);
            }
            
            if (devices.Count == 0)
            {
                _defaultDeviceMenu.DropDownItems.Add(new ToolStripMenuItem(L.Get("Menu_NoDevices")) { Enabled = false });
            }
        }

        private void RefreshRecordingDevices()
        {
            _recordingMenu.DropDownItems.Clear();

            var devices = _audioManager.GetRecordingDevices();
            var currentDefault = _audioManager.GetDefaultDevice(EDataFlow.eCapture);

            foreach (var device in devices)
            {
                var item = new ToolStripMenuItem(device.FriendlyName)
                {
                    Checked = device.Id == currentDefault?.Id,
                    Tag = device.Id
                };

                item.Click += (s, e) =>
                {
                    var deviceId = (string)((ToolStripMenuItem)s).Tag;
                    if (_audioManager.SetDefaultDeviceAllRoles(deviceId))
                    {
                        var newDevice = _audioManager.GetDefaultDevice(EDataFlow.eCapture);
                        ShowBalloon(L.Get("Notify_RecordingChanged"), newDevice?.FriendlyName ?? "Unknown");
                    }
                };

                _recordingMenu.DropDownItems.Add(item);
            }

            if (devices.Count == 0)
            {
                _recordingMenu.DropDownItems.Add(new ToolStripMenuItem(L.Get("Menu_NoDevices")) { Enabled = false });
            }
        }

        private void HandleHeadsetStateChange(bool headsetConnected)
        {
            if (headsetConnected && _config.AutoSwitchOnConnect)
            {
                SwitchToHeadset();
            }
            else if (!headsetConnected && _config.AutoSwitchOnDisconnect)
            {
                SwitchToSpeakers();
            }
        }

        private void SwitchToHeadset()
        {
            bool playbackSwitched = false;
            bool micSwitched = false;

            // Switch playback to headset
            var headsetPlayback = _audioManager.FindPlaybackDevice(_config.HeadsetKeyword);
            if (headsetPlayback != null)
            {
                playbackSwitched = _audioManager.SetDefaultDeviceAllRoles(headsetPlayback.Id);
                Log($"Switched playback to: {headsetPlayback.FriendlyName}");
            }

            // Switch microphone to headset mic
            if (!string.IsNullOrEmpty(_config.HeadsetMicKeyword))
            {
                var headsetMic = _audioManager.FindRecordingDevice(_config.HeadsetMicKeyword);
                if (headsetMic != null)
                {
                    micSwitched = _audioManager.SetDefaultDeviceAllRoles(headsetMic.Id);
                    Log($"Switched microphone to: {headsetMic.FriendlyName}");
                }
            }

            if (playbackSwitched)
            {
                ShowBalloon(L.Get("Notify_HeadsetConnected"),
                    L.Get("Notify_SwitchedTo", headsetPlayback?.FriendlyName ?? _config.HeadsetKeyword));
            }
        }

        private void SwitchToSpeakers()
        {
            bool playbackSwitched = false;
            bool micSwitched = false;
            string switchedDeviceName = null;

            // Try to switch playback to configured default device (by ID first, then by keyword)
            if (!string.IsNullOrEmpty(_config.DefaultPlaybackDeviceId))
            {
                // Try by exact device ID
                playbackSwitched = _audioManager.SetDefaultDeviceAllRoles(_config.DefaultPlaybackDeviceId);
                if (playbackSwitched)
                {
                    switchedDeviceName = _config.DefaultPlaybackDeviceName;
                    Log($"Switched playback to: {switchedDeviceName}");
                }
            }
            
            // Fallback to keyword search if ID didn't work
            if (!playbackSwitched && !string.IsNullOrEmpty(_config.SpeakersKeyword))
            {
                var speakers = _audioManager.FindPlaybackDevice(_config.SpeakersKeyword);
                if (speakers != null)
                {
                    playbackSwitched = _audioManager.SetDefaultDeviceAllRoles(speakers.Id);
                    if (playbackSwitched)
                    {
                        switchedDeviceName = speakers.FriendlyName;
                        Log($"Switched playback to: {speakers.FriendlyName}");
                    }
                }
            }

            if (playbackSwitched && switchedDeviceName != null)
            {
                ShowBalloon(L.Get("Notify_HeadsetDisconnected"),
                    L.Get("Notify_SwitchedTo", switchedDeviceName));
            }
            else
            {
                ShowBalloon(L.Get("Notify_HeadsetDisconnected"),
                    L.Get("Notify_SpeakersNotConfigured"));
            }

            // Switch microphone to default (by ID first, then by keyword)
            if (!string.IsNullOrEmpty(_config.DefaultRecordingDeviceId))
            {
                micSwitched = _audioManager.SetDefaultDeviceAllRoles(_config.DefaultRecordingDeviceId);
                if (micSwitched)
                {
                    Log($"Switched microphone to: {_config.DefaultRecordingDeviceName}");
                }
            }
            
            if (!micSwitched && !string.IsNullOrEmpty(_config.DefaultMicKeyword))
            {
                var defaultMic = _audioManager.FindRecordingDevice(_config.DefaultMicKeyword);
                if (defaultMic != null)
                {
                    micSwitched = _audioManager.SetDefaultDeviceAllRoles(defaultMic.Id);
                    Log($"Switched microphone to: {defaultMic.FriendlyName}");
                }
            }
        }

        private void UpdateStatus(bool ghubConnected)
        {
            if (_statusMenuItem.GetCurrentParent()?.InvokeRequired == true)
            {
                _statusMenuItem.GetCurrentParent().Invoke(new Action(() => UpdateStatusInternal(ghubConnected)));
            }
            else
            {
                UpdateStatusInternal(ghubConnected);
            }
        }

        private void UpdateStatusInternal(bool ghubConnected)
        {
            // Priority: G HUB > HID++ > Windows native
            string statusText;
            string tooltipText;
            
            if (ghubConnected)
            {
                statusText = L.Get("Status_Connected");
                tooltipText = L.Get("TrayTip_Connected");
            }
            else if (_hidppMonitor?.IsMonitoring == true)
            {
                // HID++ monitoring is the best fallback for wireless headsets
                statusText = L.Get("Status_HidPP");
                tooltipText = L.Get("TrayTip_HidPP");
            }
            else
            {
                // Windows monitoring is the basic fallback
                statusText = L.Get("Status_Monitoring");
                tooltipText = L.Get("TrayTip_Monitoring");
            }

            _statusMenuItem.Text = statusText;
            _trayIcon.Text = tooltipText;
        }

        private void ShowBalloon(string title, string text)
        {
            if (!_config.ShowNotifications) return;

            _trayIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _hidppMonitor?.StopMonitoring();
            _hidppMonitor?.Dispose();
            _deviceMonitor?.Stop();
            _deviceMonitor?.Dispose();
            _audioManager?.Dispose();
            Application.Exit();
        }
    }
}
