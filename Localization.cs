using System;
using System.Collections.Generic;
using System.Globalization;

namespace AudioSwitcher
{
    /// <summary>
    /// Supported languages
    /// </summary>
    public enum Language
    {
        Auto,       // Auto-detect from system
        English,
        Chinese
    }

    /// <summary>
    /// Localization manager for multi-language support
    /// </summary>
    public static class Localization
    {
        private static Language _currentLanguage = Language.Auto;
        private static Dictionary<string, string> _strings;

        /// <summary>
        /// Current language setting
        /// </summary>
        public static Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                _currentLanguage = value;
                LoadStrings();
            }
        }

        /// <summary>
        /// Get the effective language (resolving Auto to actual language)
        /// </summary>
        public static Language EffectiveLanguage
        {
            get
            {
                if (_currentLanguage == Language.Auto)
                {
                    return DetectSystemLanguage();
                }
                return _currentLanguage;
            }
        }

        static Localization()
        {
            LoadStrings();
        }

        /// <summary>
        /// Detect system language
        /// </summary>
        private static Language DetectSystemLanguage()
        {
            var culture = CultureInfo.CurrentUICulture;
            
            // Check for Chinese variants
            if (culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return Language.Chinese;
            }
            
            return Language.English;
        }

        /// <summary>
        /// Load string resources for current language
        /// </summary>
        private static void LoadStrings()
        {
            var lang = EffectiveLanguage;
            
            if (lang == Language.Chinese)
            {
                _strings = ChineseStrings;
            }
            else
            {
                _strings = EnglishStrings;
            }
        }

        /// <summary>
        /// Get localized string by key
        /// </summary>
        public static string Get(string key)
        {
            if (_strings != null && _strings.TryGetValue(key, out string value))
            {
                return value;
            }
            
            // Fallback to English
            if (EnglishStrings.TryGetValue(key, out string fallback))
            {
                return fallback;
            }
            
            return key; // Return key if not found
        }

        /// <summary>
        /// Get localized string with format parameters
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string format = Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        // ============================================
        // English Strings
        // ============================================
        private static readonly Dictionary<string, string> EnglishStrings = new Dictionary<string, string>
        {
            // Status
            ["Status_Connecting"] = "Connecting...",
            ["Status_Connected"] = "✅ Monitoring Active",
            ["Status_Disconnected"] = "❌ Disconnected",
            ["Status_Monitoring"] = "✅ Monitoring Active",
            ["Status_HidPP"] = "✅ HID++ Wireless Monitoring",
            ["Status_HeadsetOn"] = "🎧 Headset ON",
            ["Status_HeadsetOff"] = "🔇 Headset OFF",
            ["TrayTip_Connected"] = "G PRO Audio Switcher",
            ["TrayTip_Disconnected"] = "G PRO Audio Switcher - Disconnected",
            ["TrayTip_Monitoring"] = "G PRO Audio Switcher",
            ["TrayTip_HidPP"] = "G PRO Audio Switcher - HID++ Monitor",
            ["TrayTip_HeadsetOn"] = "G PRO Audio Switcher - Headset ON",
            ["TrayTip_HeadsetOff"] = "G PRO Audio Switcher - Headset OFF",
            ["TrayTip_Default"] = "G PRO Audio Switcher",

            // Menu Items
            ["Menu_PlaybackDevices"] = "🔊 Playback Devices",
            ["Menu_RecordingDevices"] = "🎤 Recording Devices",
            ["Menu_SetDefaultDevice"] = "⭐ Set Default Device",
            ["Menu_AutoSwitch"] = "Auto Switch",
            ["Menu_StartWithWindows"] = "Start with Windows",
            ["Menu_Settings"] = "⚙️ Settings",
            ["Menu_ShowNotifications"] = "Show Notifications",
            ["Menu_DebugMode"] = "Debug Mode (Log Messages)",
            ["Menu_ConfigHeadset"] = "Configure Headset Keyword...",
            ["Menu_ConfigSpeakers"] = "Configure Speakers Keyword...",
            ["Menu_Language"] = "🌐 Language",
            ["Menu_LangAuto"] = "Auto (System)",
            ["Menu_LangEnglish"] = "English",
            ["Menu_LangChinese"] = "中文",
            ["Menu_Exit"] = "Exit",
            ["Menu_NoDevices"] = "No devices found",
            ["Menu_DefaultMarker"] = "★",
            ["Menu_SetAsDefault"] = "Set as default (when headset disconnects)",

            // Dialogs
            ["Dialog_ConfigHeadset_Title"] = "Configure Headset",
            ["Dialog_ConfigHeadset_Prompt"] = "Enter the keyword to identify your headset device name:",
            ["Dialog_ConfigSpeakers_Title"] = "Configure Speakers",
            ["Dialog_ConfigSpeakers_Prompt"] = "Available playback devices:\n\n{0}\nEnter keyword to identify your speakers:",
            ["Dialog_AlreadyRunning_Title"] = "Already Running",
            ["Dialog_AlreadyRunning_Text"] = "G PRO Audio Switcher is already running.\n\nCheck the system tray (notification area) for the icon.",

            // Notifications
            ["Notify_HeadsetConnected"] = "🎧 Headset Connected",
            ["Notify_HeadsetDisconnected"] = "🔊 Headset Disconnected",
            ["Notify_SwitchedTo"] = "Switched to {0}",
            ["Notify_SpeakersNotConfigured"] = "Default device not set (right-click device to set)",
            ["Notify_PlaybackChanged"] = "Playback Changed",
            ["Notify_RecordingChanged"] = "Recording Changed",
            ["Notify_DefaultSet"] = "Default device set",
            ["Notify_CurrentDevices"] = "Current Devices",

            // Buttons
            ["Button_OK"] = "OK",
            ["Button_Cancel"] = "Cancel",

            // Language change
            ["Notify_LanguageChanged"] = "Language Changed",
            ["Notify_RestartRequired"] = "Please restart the application for full effect"
        };

        // ============================================
        // Chinese Strings (简体中文)
        // ============================================
        private static readonly Dictionary<string, string> ChineseStrings = new Dictionary<string, string>
        {
            // Status
            ["Status_Connecting"] = "连接中...",
            ["Status_Connected"] = "✅ 监控中",
            ["Status_Disconnected"] = "❌ 已断开",
            ["Status_Monitoring"] = "✅ 监控中",
            ["Status_HidPP"] = "✅ HID++ 无线监控",
            ["Status_HeadsetOn"] = "🎧 耳机已开启",
            ["Status_HeadsetOff"] = "🔇 耳机已关闭",
            ["TrayTip_Connected"] = "G PRO 音频切换器",
            ["TrayTip_Disconnected"] = "G PRO 音频切换器 - 已断开",
            ["TrayTip_Monitoring"] = "G PRO 音频切换器",
            ["TrayTip_HidPP"] = "G PRO 音频切换器 - HID++ 监控",
            ["TrayTip_HeadsetOn"] = "G PRO 音频切换器 - 耳机已开启",
            ["TrayTip_HeadsetOff"] = "G PRO 音频切换器 - 耳机已关闭",
            ["TrayTip_Default"] = "G PRO 音频切换器",

            // Menu Items
            ["Menu_PlaybackDevices"] = "🔊 播放设备",
            ["Menu_RecordingDevices"] = "🎤 录音设备",
            ["Menu_SetDefaultDevice"] = "⭐ 设置默认设备",
            ["Menu_AutoSwitch"] = "自动切换",
            ["Menu_StartWithWindows"] = "开机自启动",
            ["Menu_Settings"] = "⚙️ 设置",
            ["Menu_ShowNotifications"] = "显示通知",
            ["Menu_DebugMode"] = "调试模式（记录消息）",
            ["Menu_ConfigHeadset"] = "配置耳机关键字...",
            ["Menu_ConfigSpeakers"] = "配置扬声器关键字...",
            ["Menu_Language"] = "🌐 语言",
            ["Menu_LangAuto"] = "自动（跟随系统）",
            ["Menu_LangEnglish"] = "English",
            ["Menu_LangChinese"] = "中文",
            ["Menu_Exit"] = "退出",
            ["Menu_NoDevices"] = "未找到设备",
            ["Menu_DefaultMarker"] = "★",
            ["Menu_SetAsDefault"] = "设为默认（耳机断开时切换）",

            // Dialogs
            ["Dialog_ConfigHeadset_Title"] = "配置耳机",
            ["Dialog_ConfigHeadset_Prompt"] = "输入耳机设备名称中的关键字：",
            ["Dialog_ConfigSpeakers_Title"] = "配置扬声器",
            ["Dialog_ConfigSpeakers_Prompt"] = "可用的播放设备：\n\n{0}\n输入扬声器设备名称中的关键字：",
            ["Dialog_AlreadyRunning_Title"] = "程序已运行",
            ["Dialog_AlreadyRunning_Text"] = "G PRO 音频切换器已在运行中。\n\n请在系统托盘（通知区域）查看图标。",

            // Notifications
            ["Notify_HeadsetConnected"] = "🎧 耳机已连接",
            ["Notify_HeadsetDisconnected"] = "🔊 耳机已断开",
            ["Notify_SwitchedTo"] = "已切换到 {0}",
            ["Notify_SpeakersNotConfigured"] = "未设置默认设备（右键设备进行设置）",
            ["Notify_PlaybackChanged"] = "播放设备已更改",
            ["Notify_RecordingChanged"] = "录音设备已更改",
            ["Notify_DefaultSet"] = "已设为默认设备",
            ["Notify_CurrentDevices"] = "当前设备",

            // Buttons
            ["Button_OK"] = "确定",
            ["Button_Cancel"] = "取消",

            // Language change
            ["Notify_LanguageChanged"] = "语言已更改",
            ["Notify_RestartRequired"] = "请重启应用程序以完全生效"
        };
    }

    /// <summary>
    /// Shorthand accessor for localized strings
    /// </summary>
    public static class L
    {
        public static string Get(string key) => Localization.Get(key);
        public static string Get(string key, params object[] args) => Localization.Get(key, args);
    }
}
