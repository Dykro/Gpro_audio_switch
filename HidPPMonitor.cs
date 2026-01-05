using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace AudioSwitcher
{
    /// <summary>
    /// Event args for headset connection state changes
    /// </summary>
    public class HeadsetConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public byte DeviceIndex { get; }
        public string DeviceName { get; }

        public HeadsetConnectionChangedEventArgs(bool isConnected, byte deviceIndex, string deviceName = null)
        {
            IsConnected = isConnected;
            DeviceIndex = deviceIndex;
            DeviceName = deviceName;
        }
    }

    /// <summary>
    /// Monitors Logitech G PRO X 2 headset connection status via HID++ protocol
    /// HID++ allows detecting when a wireless device actually connects/disconnects from its receiver
    /// </summary>
    public class HidPPMonitor : IDisposable
    {
        // Logitech vendor ID
        private const ushort LOGITECH_VID = 0x046D;
        
        // G PRO X 2 Lightspeed receiver PID
        private const ushort GPRO_X2_RECEIVER_PID = 0x0AF7;
        
        // HID++ SubIDs (from hidpp10/defs.h)
        private const byte DeviceDisconnection = 0x40;
        private const byte DeviceConnection = 0x41;
        private const byte SetRegisterShort = 0x80;
        private const byte GetRegisterShort = 0x81;
        private const byte ErrorMessage = 0x8F;
        
        // HID++ Register addresses
        private const byte EnableNotifications = 0x00;
        
        // Report types
        private const byte ShortReportId = 0x10;
        private const byte LongReportId = 0x11;
        
        // G HUB Protocol (used by G PRO X 2)
        private const byte GHubReportId = 0x51;
        private const byte GHubConnectionDeviceId = 0x05;  // Device ID for connection status
        private const byte GHubConnectionCmd = 0x03;       // Command type for connection info
        
        private SafeFileHandle _deviceHandle;
        private Thread _monitorThread;
        private volatile bool _running;
        private bool _disposed;
        private string _devicePath;
        
        // Track known connected devices by device index
        private readonly Dictionary<byte, bool> _connectedDevices = new Dictionary<byte, bool>();
        
        /// <summary>
        /// Fired when the headset connects or disconnects from the receiver
        /// </summary>
        public event EventHandler<HeadsetConnectionChangedEventArgs> OnHeadsetConnectionChanged;
        
        /// <summary>
        /// Whether monitoring is currently active
        /// </summary>
        public bool IsMonitoring => _running;
        
        /// <summary>
        /// Whether the headset is currently connected
        /// </summary>
        public bool IsHeadsetConnected { get; private set; }
        
        /// <summary>
        /// Status message for diagnostics
        /// </summary>
        public string StatusMessage { get; private set; } = "Not started";
        
        // Debug log file path
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GProAudioSwitcher", "hidpp_debug.log");
        
        private static void LogDebug(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        #region Native Methods
        
        [DllImport("hid.dll", SetLastError = true)]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);
        
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);
        
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
        
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);
        
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);
        
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(
            SafeFileHandle hidDeviceObject,
            ref HIDD_ATTRIBUTES attributes);
        
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(
            SafeFileHandle hidDeviceObject,
            out IntPtr preparsedData);
        
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);
        
        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetCaps(
            IntPtr preparsedData,
            ref HIDP_CAPS capabilities);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public uint Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }
        
        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x01;
        private const uint FILE_SHARE_WRITE = 0x02;
        private const uint OPEN_EXISTING = 3;
        
        #endregion

        /// <summary>
        /// Find and connect to the G PRO X 2 receiver
        /// </summary>
        public bool Connect()
        {
            LogDebug("Connect() called");
            var receiverPath = FindReceiverDevice();
            if (receiverPath == null)
            {
                StatusMessage = "Device not found";
                LogDebug("Device not found");
                return false;
            }
            
            _devicePath = receiverPath;
            LogDebug("Found device: " + receiverPath);
            
            // Open device for read/write with shared access
            _deviceHandle = CreateFile(
                receiverPath,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);
            
            if (_deviceHandle.IsInvalid)
            {
                // Try read-only if write access is denied (G HUB may have exclusive access)
                _deviceHandle = CreateFile(
                    receiverPath,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
                
                if (_deviceHandle.IsInvalid)
                {
                    int err = Marshal.GetLastWin32Error();
                    StatusMessage = "Cannot open device (error " + err + ")";
                    LogDebug("CreateFile failed with error: " + err);
                    return false;
                }
                LogDebug("Opened device read-only");
            }
            else
            {
                LogDebug("Opened device read-write");
            }
            
            StatusMessage = "Connected to receiver";
            
            // Try to enable wireless notifications
            EnableWirelessNotifications();
            
            return true;
        }
        
        /// <summary>
        /// Find the HID device path for the G PRO X 2 receiver
        /// We look for the interface that supports HID++ (short or long reports)
        /// </summary>
        private string FindReceiverDevice()
        {
            HidD_GetHidGuid(out Guid hidGuid);
            
            IntPtr deviceInfoSet = SetupDiGetClassDevs(
                ref hidGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            
            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
                return null;
            
            var candidates = new List<(string path, int reportLength)>();
            
            try
            {
                SP_DEVICE_INTERFACE_DATA interfaceData = new SP_DEVICE_INTERFACE_DATA();
                interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);
                
                uint index = 0;
                while (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index++, ref interfaceData))
                {
                    // Get required size
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                    
                    // Allocate buffer
                    IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        // Set cbSize - 8 for 64-bit, 6 for 32-bit (size of fixed part)
                        Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
                        
                        if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out _, IntPtr.Zero))
                        {
                            // Path starts at offset 4
                            string devicePath = Marshal.PtrToStringAuto(detailData + 4);
                            
                            // Check if this is our device
                            var deviceInfo = GetDeviceInfo(devicePath);
                            if (deviceInfo.HasValue && 
                                deviceInfo.Value.vid == LOGITECH_VID && 
                                deviceInfo.Value.pid == GPRO_X2_RECEIVER_PID)
                            {
                                // G PRO X 2 uses G HUB protocol with 64-byte reports
                                // Also accept traditional HID++ report sizes as fallback
                                int inputLen = deviceInfo.Value.inputReportLength;
                                if (inputLen == 64 || inputLen == 62 || inputLen == 20 || inputLen == 21 || inputLen == 7 || inputLen == 8)
                                {
                                    candidates.Add((devicePath, inputLen));
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailData);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
            
            // Prefer 64-byte interface (G HUB protocol with connection status)
            // Then 62-byte, then traditional HID++ sizes
            var best = candidates
                .OrderByDescending(c => c.reportLength == 64 ? 100 : c.reportLength)
                .FirstOrDefault();
            return best.path;
        }
        
        /// <summary>
        /// Get VID, PID, and report length for a HID device
        /// </summary>
        private (ushort vid, ushort pid, int inputReportLength)? GetDeviceInfo(string devicePath)
        {
            using var handle = CreateFile(
                devicePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);
            
            if (handle.IsInvalid)
                return null;
            
            HIDD_ATTRIBUTES attrs = new HIDD_ATTRIBUTES();
            attrs.Size = (uint)Marshal.SizeOf(attrs);
            
            if (!HidD_GetAttributes(handle, ref attrs))
                return null;
            
            // Get report length
            // HIDP_STATUS_SUCCESS = 0x00110000 (not 0!)
            const int HIDP_STATUS_SUCCESS = 0x00110000;
            int inputReportLength = 0;
            if (HidD_GetPreparsedData(handle, out IntPtr preparsedData))
            {
                try
                {
                    HIDP_CAPS caps = new HIDP_CAPS();
                    if (HidP_GetCaps(preparsedData, ref caps) == HIDP_STATUS_SUCCESS)
                    {
                        inputReportLength = caps.InputReportByteLength;
                    }
                }
                finally
                {
                    HidD_FreePreparsedData(preparsedData);
                }
            }
            
            return (attrs.VendorID, attrs.ProductID, inputReportLength);
        }
        
        /// <summary>
        /// Enable wireless device notifications in the receiver
        /// </summary>
        private void EnableWirelessNotifications()
        {
            if (_deviceHandle == null || _deviceHandle.IsInvalid)
                return;
            
            try
            {
                // HID++ 1.0 SetRegister request to enable notifications
                // Format: [ReportId, DeviceIndex, SubId, Address, P0, P1, P2]
                // DeviceIndex = 0xFF for receiver
                // Address = 0x00 (EnableNotifications)
                // P0[bit0] = 1 enables wireless notifications
                byte[] enableNotif = new byte[7];
                enableNotif[0] = ShortReportId;  // Report ID 0x10
                enableNotif[1] = 0xFF;           // Device index: receiver
                enableNotif[2] = SetRegisterShort; // SetRegister (short)
                enableNotif[3] = EnableNotifications; // Register address
                enableNotif[4] = 0x01;           // Enable wireless notifications
                enableNotif[5] = 0x00;
                enableNotif[6] = 0x00;
                
                WriteFile(_deviceHandle, enableNotif, (uint)enableNotif.Length, out _, IntPtr.Zero);
            }
            catch
            {
                // Ignore errors - we may not have write access
            }
        }
        
        /// <summary>
        /// Start monitoring for device connection events
        /// </summary>
        public void StartMonitoring()
        {
            if (_running || _deviceHandle == null || _deviceHandle.IsInvalid)
                return;
            
            _running = true;
            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "HID++ Monitor"
            };
            _monitorThread.Start();
        }
        
        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _running = false;
            _monitorThread?.Join(1000);
            _monitorThread = null;
        }
        
        /// <summary>
        /// Main monitoring loop - reads HID reports and processes HID++ messages
        /// </summary>
        private void MonitorLoop()
        {
            // G HUB protocol uses 64-byte reports
            byte[] buffer = new byte[65]; // 64 bytes + 1 for report ID overhead
            
            // Log that monitoring has started
            LogDebug("MonitorLoop started, device: " + _devicePath);
            
            // First, try to get current device status by requesting connection info
            RequestDeviceList();
            
            while (_running)
            {
                try
                {
                    if (ReadFile(_deviceHandle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero) && bytesRead > 0)
                    {
                        // Log raw data (first 8 bytes)
                        string hex = BitConverter.ToString(buffer, 0, Math.Min((int)bytesRead, 8));
                        LogDebug($"Read {bytesRead} bytes: {hex}");
                        
                        ProcessHidPPReport(buffer, (int)bytesRead);
                    }
                    else
                    {
                        int err = Marshal.GetLastWin32Error();
                        // Only log non-timeout errors
                        if (err != 0 && err != 995) // 995 = operation aborted
                        {
                            LogDebug($"ReadFile failed: error {err}");
                        }
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Exception in MonitorLoop: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }
        
        /// <summary>
        /// Request list of connected devices from receiver
        /// </summary>
        private void RequestDeviceList()
        {
            if (_deviceHandle == null || _deviceHandle.IsInvalid)
                return;
            
            try
            {
                // Request connection state register
                // This causes the receiver to send DeviceConnection notifications for each paired device
                byte[] request = new byte[7];
                request[0] = ShortReportId;
                request[1] = 0xFF;             // Device index: receiver
                request[2] = GetRegisterShort; // GetRegister
                request[3] = 0x02;             // ConnectionState register
                request[4] = 0x00;
                request[5] = 0x00;
                request[6] = 0x00;
                
                WriteFile(_deviceHandle, request, (uint)request.Length, out _, IntPtr.Zero);
            }
            catch
            {
                // Ignore
            }
        }
        
        /// <summary>
        /// Process an incoming HID++ report
        /// </summary>
        private void ProcessHidPPReport(byte[] data, int length)
        {
            if (length < 7) return;
            
            byte reportId = data[0];
            byte deviceId = data[1];
            byte subCmd = data[2];
            byte cmd = data[3];
            
            // G HUB Protocol (0x51) - used by G PRO X 2
            // Format: [0x51] [DeviceId] [SubCmd] [Cmd] [Data...]
            // Connection status: DeviceId=0x05, SubCmd=0x00, Cmd=0x03
            // Byte[6] = 0x01 means connected, 0x00 means disconnected
            if (reportId == GHubReportId)
            {
                LogDebug($"G HUB report: DevId={deviceId:X2} SubCmd={subCmd:X2} Cmd={cmd:X2} byte6={data[6]:X2}");
                
                if (deviceId == GHubConnectionDeviceId && subCmd == 0x00 && cmd == GHubConnectionCmd)
                {
                    bool isConnected = data[6] == 0x01;
                    LogDebug($"Connection state detected: isConnected={isConnected}");
                    HandleGHubConnectionChange(isConnected);
                    return;
                }
            }
            
            // Traditional HID++ reports (0x10 = short, 0x11 = long)
            if (reportId == ShortReportId || reportId == LongReportId)
            {
                byte subId = data[2];
                
                switch (subId)
                {
                    case DeviceConnection:
                        HandleDeviceConnection(data, length);
                        break;
                        
                    case DeviceDisconnection:
                        HandleDeviceDisconnection(data, length);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Handle G HUB protocol connection change
        /// </summary>
        private void HandleGHubConnectionChange(bool isConnected)
        {
            LogDebug($"HandleGHubConnectionChange: isConnected={isConnected}, previous={IsHeadsetConnected}");
            
            // Avoid duplicate events
            if (isConnected == IsHeadsetConnected)
            {
                LogDebug("Skipping duplicate event");
                return;
            }
            
            IsHeadsetConnected = isConnected;
            StatusMessage = isConnected ? "Headset connected" : "Headset disconnected";
            LogDebug($"Firing OnHeadsetConnectionChanged event, isConnected={isConnected}");
            
            OnHeadsetConnectionChanged?.Invoke(this, 
                new HeadsetConnectionChangedEventArgs(isConnected, 0x05, "G PRO X 2"));
        }
        
        /// <summary>
        /// Handle device connection notification
        /// </summary>
        private void HandleDeviceConnection(byte[] data, int length)
        {
            if (length < 4) return;
            
            byte deviceIndex = data[1];
            byte protocolType = data[3];
            
            // deviceIndex 0x01-0x06 are wireless device indices
            // protocolType: bit 6 = link established (1) or link lost (0)
            bool linkEstablished = (protocolType & 0x40) != 0;
            
            // Also check if device type indicates actual connection
            // Bit 4 = software present, Bit 5 = encrypted link
            bool isConnected = linkEstablished;
            
            // Get wireless PID if available (bytes 5-6, big endian)
            ushort wirelessPid = 0;
            if (length >= 7)
            {
                wirelessPid = (ushort)((data[5] << 8) | data[6]);
            }
            
            // Update tracked state
            bool wasConnected = _connectedDevices.ContainsKey(deviceIndex) && _connectedDevices[deviceIndex];
            _connectedDevices[deviceIndex] = isConnected;
            
            // Update overall headset state
            bool anyConnected = _connectedDevices.Values.Any(v => v);
            
            if (anyConnected != IsHeadsetConnected || isConnected != wasConnected)
            {
                IsHeadsetConnected = anyConnected;
                
                string deviceName = null;
                if (wirelessPid == GPRO_X2_RECEIVER_PID || wirelessPid == 0x0AF6 || wirelessPid == 0x0AF5)
                {
                    deviceName = "G PRO X 2";
                }
                
                OnHeadsetConnectionChanged?.Invoke(this, 
                    new HeadsetConnectionChangedEventArgs(isConnected, deviceIndex, deviceName));
            }
        }
        
        /// <summary>
        /// Handle device disconnection notification
        /// </summary>
        private void HandleDeviceDisconnection(byte[] data, int length)
        {
            if (length < 4) return;
            
            byte deviceIndex = data[1];
            byte disconnectType = data[3];
            
            // Update tracked state
            _connectedDevices[deviceIndex] = false;
            
            // Update overall headset state
            bool anyConnected = _connectedDevices.Values.Any(v => v);
            
            if (anyConnected != IsHeadsetConnected)
            {
                IsHeadsetConnected = anyConnected;
                OnHeadsetConnectionChanged?.Invoke(this, 
                    new HeadsetConnectionChangedEventArgs(false, deviceIndex, null));
            }
        }
        
        /// <summary>
        /// Manually check if headset is currently connected
        /// </summary>
        public bool CheckConnection()
        {
            // Request device list - this will trigger connection notifications
            RequestDeviceList();
            return IsHeadsetConnected;
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            StopMonitoring();
            
            _deviceHandle?.Dispose();
            _deviceHandle = null;
        }
    }
}
