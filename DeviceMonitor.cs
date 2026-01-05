using System;
using System.Runtime.InteropServices;
using AudioSwitcher.Interop;

namespace AudioSwitcher
{
    /// <summary>
    /// Event args for device state changes
    /// </summary>
    public class DeviceStateChangedEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public DeviceState NewState { get; }
        public string DeviceName { get; }

        public DeviceStateChangedEventArgs(string deviceId, DeviceState newState, string deviceName = null)
        {
            DeviceId = deviceId;
            NewState = newState;
            DeviceName = deviceName;
        }
    }

    /// <summary>
    /// Event args for default device changes
    /// </summary>
    public class DefaultDeviceChangedEventArgs : EventArgs
    {
        public EDataFlow DataFlow { get; }
        public ERole Role { get; }
        public string DeviceId { get; }

        public DefaultDeviceChangedEventArgs(EDataFlow dataFlow, ERole role, string deviceId)
        {
            DataFlow = dataFlow;
            Role = role;
            DeviceId = deviceId;
        }
    }

    /// <summary>
    /// Monitors Windows audio device changes using Core Audio API
    /// This is a reliable alternative to G HUB WebSocket
    /// </summary>
    public class DeviceMonitor : IMMNotificationClient, IDisposable
    {
        private IMMDeviceEnumerator _enumerator;
        private bool _disposed;
        private bool _registered;

        /// <summary>
        /// Fired when a device state changes (Active, Disabled, NotPresent, Unplugged)
        /// </summary>
        public event EventHandler<DeviceStateChangedEventArgs> OnDeviceStateChanged;

        /// <summary>
        /// Fired when a new device is added
        /// </summary>
        public event EventHandler<string> OnDeviceAdded;

        /// <summary>
        /// Fired when a device is removed
        /// </summary>
        public event EventHandler<string> OnDeviceRemoved;

        /// <summary>
        /// Fired when the default device changes
        /// </summary>
        public event EventHandler<DefaultDeviceChangedEventArgs> OnDefaultDeviceChanged;

        public DeviceMonitor()
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        }

        /// <summary>
        /// Start monitoring device changes
        /// </summary>
        public void Start()
        {
            if (_registered) return;

            int hr = _enumerator.RegisterEndpointNotificationCallback(this);
            if (hr == 0)
            {
                _registered = true;
            }
        }

        /// <summary>
        /// Stop monitoring device changes
        /// </summary>
        public void Stop()
        {
            if (!_registered) return;

            try
            {
                _enumerator?.UnregisterEndpointNotificationCallback(this);
            }
            catch { }
            
            _registered = false;
        }

        // IMMNotificationClient implementation

        void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            OnDeviceStateChanged?.Invoke(this, new DeviceStateChangedEventArgs(deviceId, newState));
        }

        void IMMNotificationClient.OnDeviceAdded(string deviceId)
        {
            OnDeviceAdded?.Invoke(this, deviceId);
        }

        void IMMNotificationClient.OnDeviceRemoved(string deviceId)
        {
            OnDeviceRemoved?.Invoke(this, deviceId);
        }

        void IMMNotificationClient.OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
        {
            OnDefaultDeviceChanged?.Invoke(this, new DefaultDeviceChangedEventArgs(flow, role, defaultDeviceId));
        }

        void IMMNotificationClient.OnPropertyValueChanged(string deviceId, PropertyKey key)
        {
            // Not used for now
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();

            if (_enumerator != null)
            {
                Marshal.ReleaseComObject(_enumerator);
                _enumerator = null;
            }
        }
    }
}
