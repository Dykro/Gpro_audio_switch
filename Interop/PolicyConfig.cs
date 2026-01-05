using System;
using System.Runtime.InteropServices;

namespace AudioSwitcher.Interop
{
    // ============================================
    // IPolicyConfig Interface
    // UNDOCUMENTED Windows interface for setting default audio device
    // This is widely used by audio switching tools (SoundSwitch, etc.)
    // GUID works on Windows 7/8/10/11
    // ============================================

    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPolicyConfig
    {
        [PreserveSig]
        int GetMixFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr format);

        [PreserveSig]
        int GetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.Bool)] bool isDefault,
            IntPtr format);

        [PreserveSig]
        int ResetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        [PreserveSig]
        int SetDeviceFormat(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr endpointFormat,
            IntPtr mixFormat);

        [PreserveSig]
        int GetProcessingPeriod(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.Bool)] bool isDefault,
            IntPtr defaultPeriod,
            IntPtr minPeriod);

        [PreserveSig]
        int SetProcessingPeriod(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr period);

        [PreserveSig]
        int GetShareMode(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr mode);

        [PreserveSig]
        int SetShareMode(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            IntPtr mode);

        [PreserveSig]
        int GetPropertyValue(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            ref PropertyKey key,
            out PropVariant value);

        [PreserveSig]
        int SetPropertyValue(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            ref PropertyKey key,
            ref PropVariant value);

        /// <summary>
        /// Set the default audio endpoint for a specific role
        /// This is the key method for switching audio devices
        /// </summary>
        [PreserveSig]
        int SetDefaultEndpoint(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            ERole role);

        [PreserveSig]
        int SetEndpointVisibility(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            [MarshalAs(UnmanagedType.I4)] int isVisible);
    }

    // ============================================
    // PolicyConfigClient COM Class
    // Used to instantiate IPolicyConfig
    // ============================================

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    public class PolicyConfigClient { }
}
