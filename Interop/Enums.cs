using System;

namespace AudioSwitcher.Interop
{
    /// <summary>
    /// Audio data flow direction
    /// </summary>
    public enum EDataFlow
    {
        /// <summary>Playback/render devices (speakers, headphones)</summary>
        eRender = 0,
        /// <summary>Recording/capture devices (microphones)</summary>
        eCapture = 1,
        /// <summary>All devices</summary>
        eAll = 2
    }

    /// <summary>
    /// Audio device role
    /// </summary>
    public enum ERole
    {
        /// <summary>Games, system notification sounds</summary>
        eConsole = 0,
        /// <summary>Music, movies, video playback</summary>
        eMultimedia = 1,
        /// <summary>Voice communications (calls, chat)</summary>
        eCommunications = 2
    }

    /// <summary>
    /// Audio device state flags
    /// </summary>
    [Flags]
    public enum DeviceState : uint
    {
        /// <summary>Device is active and available</summary>
        Active = 0x00000001,
        /// <summary>Device is disabled by user</summary>
        Disabled = 0x00000002,
        /// <summary>Device is not present</summary>
        NotPresent = 0x00000004,
        /// <summary>Device is unplugged</summary>
        Unplugged = 0x00000008,
        /// <summary>All device states</summary>
        All = 0x0000000F
    }
}
