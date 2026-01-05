using System;
using System.Runtime.InteropServices;

namespace AudioSwitcher.Interop
{
    // ============================================
    // COM Class - MMDeviceEnumerator
    // This is the main entry point for audio device enumeration
    // ============================================

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    public class MMDeviceEnumerator { }

    // ============================================
    // IMMDeviceEnumerator Interface
    // Used to enumerate audio devices and get default device
    // ============================================

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(
            EDataFlow dataFlow,
            DeviceState stateMask,
            out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(
            EDataFlow dataFlow,
            ERole role,
            out IMMDevice device);

        [PreserveSig]
        int GetDevice(
            [MarshalAs(UnmanagedType.LPWStr)] string id,
            out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(
            IMMNotificationClient client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(
            IMMNotificationClient client);
    }

    // ============================================
    // IMMDeviceCollection Interface
    // Collection of audio devices
    // ============================================

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int Item(uint index, out IMMDevice device);
    }

    // ============================================
    // IMMDevice Interface
    // Represents a single audio device
    // ============================================

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            int clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

        [PreserveSig]
        int OpenPropertyStore(
            int stgmAccess,
            out IPropertyStore propertyStore);

        [PreserveSig]
        int GetId(
            [MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out DeviceState state);
    }

    // ============================================
    // IPropertyStore Interface
    // Used to read device properties (like friendly name)
    // ============================================

    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint index, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    // ============================================
    // IMMNotificationClient Interface
    // Callback interface for device change notifications
    // ============================================

    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient
    {
        void OnDeviceStateChanged(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            DeviceState newState);

        void OnDeviceAdded(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        void OnDeviceRemoved(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId);

        void OnDefaultDeviceChanged(
            EDataFlow flow,
            ERole role,
            [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);

        void OnPropertyValueChanged(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
            PropertyKey key);
    }
}
