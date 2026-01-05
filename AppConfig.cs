using System;
using System.IO;
using System.Text.Json;

namespace AudioSwitcher
{
    /// <summary>
    /// Application configuration
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// Keyword to identify headset in device name (e.g., "PRO X 2")
        /// </summary>
        public string HeadsetKeyword { get; set; } = "PRO X 2";

        /// <summary>
        /// Keyword to identify speakers/default playback device
        /// </summary>
        public string SpeakersKeyword { get; set; } = "";

        /// <summary>
        /// Keyword to identify headset microphone
        /// </summary>
        public string HeadsetMicKeyword { get; set; } = "PRO X 2";

        /// <summary>
        /// Keyword to identify default microphone (when headset disconnected)
        /// </summary>
        public string DefaultMicKeyword { get; set; } = "";

        /// <summary>
        /// G HUB WebSocket URL
        /// </summary>
        public string GHubWebSocketUrl { get; set; } = "ws://localhost:9010";

        /// <summary>
        /// Whether to automatically switch when headset connects
        /// </summary>
        public bool AutoSwitchOnConnect { get; set; } = true;

        /// <summary>
        /// Whether to automatically switch back when headset disconnects
        /// </summary>
        public bool AutoSwitchOnDisconnect { get; set; } = true;

        /// <summary>
        /// Whether to show balloon notifications
        /// </summary>
        public bool ShowNotifications { get; set; } = true;

        /// <summary>
        /// Enable debug mode (log all G HUB messages)
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Whether to start with Windows
        /// </summary>
        public bool StartWithWindows { get; set; } = false;

        /// <summary>
        /// Language setting (Auto, English, Chinese)
        /// </summary>
        public string Language { get; set; } = "Auto";

        /// <summary>
        /// Default playback device ID (used when headset disconnects)
        /// </summary>
        public string DefaultPlaybackDeviceId { get; set; } = "";

        /// <summary>
        /// Default playback device name (for display purposes)
        /// </summary>
        public string DefaultPlaybackDeviceName { get; set; } = "";

        /// <summary>
        /// Default recording device ID (used when headset disconnects)
        /// </summary>
        public string DefaultRecordingDeviceId { get; set; } = "";

        /// <summary>
        /// Default recording device name (for display purposes)
        /// </summary>
        public string DefaultRecordingDeviceName { get; set; } = "";

        // ============================================
        // Static methods for loading/saving
        // ============================================

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GProAudioSwitcher",
            "config.json");

        /// <summary>
        /// Load configuration from file
        /// </summary>
        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            }

            return new AppConfig();
        }

        /// <summary>
        /// Save configuration to file
        /// </summary>
        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        /// <summary>
        /// Set or remove startup with Windows registry entry
        /// </summary>
        public static void SetStartWithWindows(bool enable, string exePath)
        {
            const string keyName = "GProAudioSwitcher";
            const string runKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKeyPath, true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        key.SetValue(keyName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(keyName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set startup: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if startup with Windows is enabled
        /// </summary>
        public static bool IsStartWithWindowsEnabled()
        {
            const string keyName = "GProAudioSwitcher";
            const string runKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKeyPath, false))
                {
                    return key?.GetValue(keyName) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
