using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// Test tool to verify HID++ protocol communication with Logitech receiver
/// </summary>
class HidPPTest
{
    // Logitech vendor ID
    const ushort LOGITECH_VID = 0x046D;
    
    // G PRO X 2 Lightspeed receiver PID
    const ushort GPRO_X2_RECEIVER_PID = 0x0AF7;
    
    // HID++ SubIDs
    const byte DeviceDisconnection = 0x40;
    const byte DeviceConnection = 0x41;
    const byte SetRegisterShort = 0x80;
    const byte GetRegisterShort = 0x81;
    const byte ErrorMessage = 0x8F;
    
    // Report types - HID++
    const byte ShortReportId = 0x10;
    const byte LongReportId = 0x11;
    
    // DJ Protocol report IDs (used by G HUB devices like G PRO X 2)
    const byte DJShortReportId = 0x20;
    const byte DJLongReportId = 0x21;
    const byte GHubReportId = 0x51;  // G HUB proprietary protocol
    
    #region Native Methods
    
    [DllImport("hid.dll", SetLastError = true)]
    static extern void HidD_GetHidGuid(out Guid hidGuid);
    
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr hwndParent,
        uint flags);
    
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);
    
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);
    
    [DllImport("setupapi.dll", SetLastError = true)]
    static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        IntPtr lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);
    
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetAttributes(
        SafeFileHandle hidDeviceObject,
        ref HIDD_ATTRIBUTES attributes);
    
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_GetPreparsedData(
        SafeFileHandle hidDeviceObject,
        out IntPtr preparsedData);
    
    [DllImport("hid.dll", SetLastError = true)]
    static extern bool HidD_FreePreparsedData(IntPtr preparsedData);
    
    [DllImport("hid.dll", SetLastError = true)]
    static extern int HidP_GetCaps(
        IntPtr preparsedData,
        ref HIDP_CAPS capabilities);
    
    [StructLayout(LayoutKind.Sequential)]
    struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    struct HIDD_ATTRIBUTES
    {
        public uint Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
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
    
    const uint DIGCF_PRESENT = 0x02;
    const uint DIGCF_DEVICEINTERFACE = 0x10;
    const uint GENERIC_READ = 0x80000000;
    const uint GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 0x01;
    const uint FILE_SHARE_WRITE = 0x02;
    const uint OPEN_EXISTING = 3;
    
    #endregion

    static void Main(string[] args)
    {
        Console.WriteLine("=== HID++ G PRO X 2 Connection Monitor Test ===\n");
        
        // Find all G PRO X 2 HID interfaces
        Console.WriteLine("Scanning for G PRO X 2 receiver interfaces...\n");
        
        var devices = FindReceiverDevices();
        
        if (devices.Count == 0)
        {
            Console.WriteLine("No G PRO X 2 receiver found!");
            Console.WriteLine("Make sure the USB receiver is plugged in.");
            return;
        }
        
        Console.WriteLine($"Found {devices.Count} HID interface(s):\n");
        foreach (var d in devices)
        {
            Console.WriteLine($"  Path: {d.path}");
            Console.WriteLine($"  Report Length: Input={d.inputLen}, Output={d.outputLen}");
            Console.WriteLine();
        }
        
        // Try all interfaces, starting with ones that have proper HID++ report sizes
        // HID++ short = 7 bytes, long = 20 bytes, very long = 64 bytes
        // Note: G PRO X 2 uses 62/64 byte reports - the 64-byte interface (col03) showed activity!
        var sortedDevices = devices.OrderBy(d => {
            // Prefer 64 (where we saw DJ protocol 0x51 reports), then 62, then others
            if (d.inputLen == 64) return 0;
            if (d.inputLen == 62) return 1;
            if (d.inputLen == 20 || d.inputLen == 21) return 2;
            if (d.inputLen == 7 || d.inputLen == 8) return 3;
            return 4;
        }).ToList();
        
        // Try to connect to each interface and find the one that works
        foreach (var device in sortedDevices)
        {
            Console.WriteLine($"\n--- Trying: {device.path} (InputLen={device.inputLen}) ---");
            
            // Try read/write access first
            var handle = CreateFile(
                device.path,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);
            
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Cannot open with R/W access (Error: {error})");
                
                // Try read-only
                handle = CreateFile(
                    device.path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
                
                if (handle.IsInvalid)
                {
                    error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"Cannot open with read-only access either (Error: {error})");
                    continue;
                }
                
                Console.WriteLine("Opened with read-only access");
            }
            else
            {
                Console.WriteLine("Opened with read/write access");
                
                // Try to enable notifications
                Console.WriteLine("Enabling wireless notifications...");
                byte[] enableNotif = new byte[7];
                enableNotif[0] = ShortReportId;  // Report ID 0x10
                enableNotif[1] = 0xFF;           // Device index: receiver
                enableNotif[2] = SetRegisterShort;
                enableNotif[3] = 0x00;           // EnableNotifications register
                enableNotif[4] = 0x01;           // Enable wireless notifications
                enableNotif[5] = 0x00;
                enableNotif[6] = 0x00;
                
                if (WriteFile(handle, enableNotif, (uint)enableNotif.Length, out uint written, IntPtr.Zero))
                {
                    Console.WriteLine($"  Sent enable notifications command ({written} bytes)");
                }
                else
                {
                    Console.WriteLine($"  Failed to send command (Error: {Marshal.GetLastWin32Error()})");
                }
            }
            
            Console.WriteLine("\nListening for HID++ events (turn headset ON/OFF)...");
            Console.WriteLine("Press Ctrl+C to exit\n");
            
            // Read reports
            byte[] buffer = new byte[64];
            int reportCount = 0;
            DateTime lastReport = DateTime.Now;
            
            while (true)
            {
                if (ReadFile(handle, buffer, (uint)buffer.Length, out uint bytesRead, IntPtr.Zero) && bytesRead > 0)
                {
                    reportCount++;
                    lastReport = DateTime.Now;
                    
                    // Parse HID++ report
                    byte reportId = buffer[0];
                    byte deviceIndex = buffer[1];
                    byte subId = buffer[2];
                    
                    string reportType = reportId switch
                    {
                        ShortReportId => "HID++ Short",
                        LongReportId => "HID++ Long",
                        DJShortReportId => "DJ Short",
                        DJLongReportId => "DJ Long",
                        GHubReportId => "G-HUB",
                        _ => $"Unknown(0x{reportId:X2})"
                    };
                    
                    string subIdName = subId switch
                    {
                        DeviceConnection => "DEVICE_CONNECTION",
                        DeviceDisconnection => "DEVICE_DISCONNECTION",
                        SetRegisterShort => "SetRegisterShort",
                        GetRegisterShort => "GetRegisterShort",
                        ErrorMessage => "Error",
                        _ => $"0x{subId:X2}"
                    };
                    
                    // For G-HUB protocol (0x51), the structure is different
                    // Byte 3 seems to be a sub-command type
                    byte ghubCmd = buffer[3];
                    
                    Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] {reportType} #{reportCount}: ");
                    
                    if (reportId == GHubReportId)
                    {
                        // G-HUB protocol format: 51 [DevIdx] [SubCmd] [Cmd] [Data...]
                        Console.Write($"Dev=0x{deviceIndex:X2} SubCmd=0x{subId:X2} Cmd=0x{ghubCmd:X2} ");
                    }
                    else
                    {
                        Console.Write($"DevIdx=0x{deviceIndex:X2} SubID={subIdName} ");
                    }
                    
                    // Print raw bytes
                    Console.Write("Data: ");
                    for (int i = 0; i < Math.Min(bytesRead, 20); i++)
                    {
                        Console.Write($"{buffer[i]:X2} ");
                    }
                    Console.WriteLine();
                    
                    // Parse connection/disconnection events
                    if (reportId == GHubReportId)
                    {
                        // Try to detect connection state changes in G-HUB protocol
                        // Looking at the data pattern, deviceIndex might indicate device slot
                        // SubCmd 0x00 with Cmd 0x03 seems to be device status
                        
                        // DevIdx 0x05, 0x0B, 0x09 etc seem to be different "channels" or features
                        // The pattern suggests device 0x03 is a keep-alive or status ping
                        
                        if (subId == 0x00 && ghubCmd == 0x03)
                        {
                            // This could be device status - byte[4] might indicate state
                            byte stateOrFeature = buffer[4];
                            if (stateOrFeature == 0x00 && buffer[5] == 0x00 && buffer[6] == 0x01)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"  >>> Possible HEADSET CONNECTED signal");
                                Console.ResetColor();
                            }
                        }
                    }
                    else if (subId == DeviceConnection)
                    {
                        byte protocolType = buffer[3];
                        bool linkEstablished = (protocolType & 0x40) != 0;
                        ushort wirelessPid = (ushort)((buffer[5] << 8) | buffer[6]);
                        
                        Console.ForegroundColor = linkEstablished ? ConsoleColor.Green : ConsoleColor.Yellow;
                        Console.WriteLine($"  >>> HEADSET {(linkEstablished ? "CONNECTED" : "LINK LOST")} (DeviceIndex: {deviceIndex}, WirelessPID: 0x{wirelessPid:X4})");
                        Console.ResetColor();
                    }
                    else if (subId == DeviceDisconnection)
                    {
                        byte disconnectType = buffer[3];
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  >>> HEADSET DISCONNECTED (DeviceIndex: {deviceIndex}, Type: 0x{disconnectType:X2})");
                        Console.ResetColor();
                    }
                }
                else
                {
                    // Check if we've been idle for a while
                    if ((DateTime.Now - lastReport).TotalSeconds > 5 && reportCount > 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] (waiting for events... received {reportCount} reports so far)");
                        lastReport = DateTime.Now;
                    }
                    Thread.Sleep(10);
                }
            }
        }
    }
    
    static List<(string path, int inputLen, int outputLen)> FindReceiverDevices()
    {
        var result = new List<(string, int, int)>();
        
        HidD_GetHidGuid(out Guid hidGuid);
        
        IntPtr deviceInfoSet = SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        
        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            return result;
        
        try
        {
            SP_DEVICE_INTERFACE_DATA interfaceData = new SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);
            
            uint index = 0;
            while (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index++, ref interfaceData))
            {
                SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                
                IntPtr detailData = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
                    
                    if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailData, requiredSize, out _, IntPtr.Zero))
                    {
                        string devicePath = Marshal.PtrToStringAuto(detailData + 4);
                        
                        var info = GetDeviceInfo(devicePath);
                        if (info.HasValue && info.Value.vid == LOGITECH_VID)
                        {
                            // Show all Logitech devices for debugging
                            Console.WriteLine($"  [0x{info.Value.pid:X4}] Input={info.Value.inputLen} Output={info.Value.outputLen} {devicePath}");
                            
                            if (info.Value.pid == GPRO_X2_RECEIVER_PID)
                            {
                                result.Add((devicePath, info.Value.inputLen, info.Value.outputLen));
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
        
        return result;
    }
    
    // HIDP_STATUS_SUCCESS is actually 0x00110000, not 0
    const int HIDP_STATUS_SUCCESS = 0x00110000;
    
    static (ushort vid, ushort pid, int inputLen, int outputLen)? GetDeviceInfo(string devicePath)
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
        
        int inputLen = 0, outputLen = 0;
        if (HidD_GetPreparsedData(handle, out IntPtr preparsedData))
        {
            try
            {
                HIDP_CAPS caps = new HIDP_CAPS();
                int status = HidP_GetCaps(preparsedData, ref caps);
                if (status == HIDP_STATUS_SUCCESS)
                {
                    inputLen = caps.InputReportByteLength;
                    outputLen = caps.OutputReportByteLength;
                }
            }
            finally
            {
                HidD_FreePreparsedData(preparsedData);
            }
        }
        
        return (attrs.VendorID, attrs.ProductID, inputLen, outputLen);
    }
}
