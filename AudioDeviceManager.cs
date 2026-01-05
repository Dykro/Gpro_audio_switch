using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AudioSwitcher.Interop;

namespace AudioSwitcher
{
    /// <summary>
    /// Represents an audio device
    /// </summary>
    public class AudioDevice
    {
        /// <summary>Device ID (used for setting as default)</summary>
        public string Id { get; set; }

        /// <summary>Friendly name shown in Windows (e.g., "Speakers (Realtek Audio)")</summary>
        public string FriendlyName { get; set; }

        /// <summary>Whether this is a playback or recording device</summary>
        public EDataFlow DataFlow { get; set; }

        /// <summary>Current device state</summary>
        public DeviceState State { get; set; }

        /// <summary>Whether this is the current default device</summary>
        public bool IsDefault { get; set; }

        public override string ToString() => FriendlyName ?? Id;
    }

    /// <summary>
    /// Manages Windows audio devices using Core Audio API
    /// No external dependencies - uses COM interop directly
    /// </summary>
    public class AudioDeviceManager : IDisposable
    {
        private IMMDeviceEnumerator _enumerator;
        private IPolicyConfig _policyConfig;
        private bool _disposed;

        public AudioDeviceManager()
        {
            // Create COM instances
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            _policyConfig = (IPolicyConfig)new PolicyConfigClient();
        }

        /// <summary>
        /// Get all active audio devices of specified type
        /// </summary>
        public List<AudioDevice> GetAllDevices(EDataFlow dataFlow)
        {
            var devices = new List<AudioDevice>();

            int hr = _enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active, out var collection);
            if (hr != 0 || collection == null) return devices;

            try
            {
                collection.GetCount(out uint count);

                // Get current default to mark it
                var currentDefault = GetDefaultDevice(dataFlow);

                for (uint i = 0; i < count; i++)
                {
                    hr = collection.Item(i, out var device);
                    if (hr != 0 || device == null) continue;

                    try
                    {
                        device.GetId(out string id);
                        device.GetState(out var state);
                        string friendlyName = GetDeviceFriendlyName(device);

                        devices.Add(new AudioDevice
                        {
                            Id = id,
                            FriendlyName = friendlyName,
                            DataFlow = dataFlow,
                            State = state,
                            IsDefault = currentDefault?.Id == id
                        });
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(collection);
            }

            return devices;
        }

        /// <summary>
        /// Get all playback devices
        /// </summary>
        public List<AudioDevice> GetPlaybackDevices() => GetAllDevices(EDataFlow.eRender);

        /// <summary>
        /// Get all recording devices
        /// </summary>
        public List<AudioDevice> GetRecordingDevices() => GetAllDevices(EDataFlow.eCapture);

        /// <summary>
        /// Get the current default device for a specific role
        /// </summary>
        public AudioDevice GetDefaultDevice(EDataFlow dataFlow, ERole role = ERole.eMultimedia)
        {
            int hr = _enumerator.GetDefaultAudioEndpoint(dataFlow, role, out var device);
            if (hr != 0 || device == null) return null;

            try
            {
                device.GetId(out string id);
                device.GetState(out var state);
                string name = GetDeviceFriendlyName(device);

                return new AudioDevice
                {
                    Id = id,
                    FriendlyName = name,
                    DataFlow = dataFlow,
                    State = state,
                    IsDefault = true
                };
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }

        /// <summary>
        /// Get the friendly name of a device
        /// </summary>
        private string GetDeviceFriendlyName(IMMDevice device)
        {
            int hr = device.OpenPropertyStore(0, out var propertyStore);
            if (hr != 0 || propertyStore == null) return "Unknown Device";

            try
            {
                var key = PropertyKey.PKEY_Device_FriendlyName;
                hr = propertyStore.GetValue(ref key, out var value);

                if (hr == 0)
                {
                    string name = value.GetString();
                    value.Clear();
                    return name ?? "Unknown Device";
                }

                return "Unknown Device";
            }
            finally
            {
                Marshal.ReleaseComObject(propertyStore);
            }
        }

        /// <summary>
        /// Set the default audio device for a specific role
        /// </summary>
        public bool SetDefaultDevice(string deviceId, ERole role)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;

            try
            {
                int hr = _policyConfig.SetDefaultEndpoint(deviceId, role);
                return hr == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set default device: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set the default audio device for all roles (Console, Multimedia, Communications)
        /// </summary>
        public bool SetDefaultDeviceAllRoles(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;

            bool success = true;
            success &= SetDefaultDevice(deviceId, ERole.eConsole);
            success &= SetDefaultDevice(deviceId, ERole.eMultimedia);
            success &= SetDefaultDevice(deviceId, ERole.eCommunications);
            return success;
        }

        /// <summary>
        /// Find a device by name (partial match, case-insensitive)
        /// </summary>
        public AudioDevice FindDeviceByName(EDataFlow dataFlow, string nameContains)
        {
            if (string.IsNullOrEmpty(nameContains)) return null;

            var devices = GetAllDevices(dataFlow);
            return devices.Find(d =>
                d.FriendlyName != null &&
                d.FriendlyName.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Find a playback device by name
        /// </summary>
        public AudioDevice FindPlaybackDevice(string nameContains)
            => FindDeviceByName(EDataFlow.eRender, nameContains);

        /// <summary>
        /// Find a recording device by name
        /// </summary>
        public AudioDevice FindRecordingDevice(string nameContains)
            => FindDeviceByName(EDataFlow.eCapture, nameContains);

        /// <summary>
        /// Switch to a device by name (all roles)
        /// </summary>
        public bool SwitchToDevice(EDataFlow dataFlow, string nameContains)
        {
            var device = FindDeviceByName(dataFlow, nameContains);
            if (device == null) return false;
            return SetDefaultDeviceAllRoles(device.Id);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_enumerator != null)
            {
                Marshal.ReleaseComObject(_enumerator);
                _enumerator = null;
            }

            if (_policyConfig != null)
            {
                Marshal.ReleaseComObject(_policyConfig);
                _policyConfig = null;
            }
        }
    }
}
