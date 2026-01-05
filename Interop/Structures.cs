using System;
using System.Runtime.InteropServices;

namespace AudioSwitcher.Interop
{
    /// <summary>
    /// Property key structure for device properties
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }

        /// <summary>
        /// Device friendly name (e.g., "Speakers (Realtek Audio)")
        /// </summary>
        public static readonly PropertyKey PKEY_Device_FriendlyName = new PropertyKey(
            new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

        /// <summary>
        /// Device description
        /// </summary>
        public static readonly PropertyKey PKEY_Device_DeviceDesc = new PropertyKey(
            new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);

        /// <summary>
        /// Device interface friendly name
        /// </summary>
        public static readonly PropertyKey PKEY_DeviceInterface_FriendlyName = new PropertyKey(
            new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);
    }

    /// <summary>
    /// PROPVARIANT structure for property values
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PropVariant
    {
        public ushort vt;           // VARTYPE
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr Data1;
        public IntPtr Data2;

        // VARTYPE constants
        private const ushort VT_LPWSTR = 31;

        /// <summary>
        /// Get string value from PROPVARIANT
        /// </summary>
        public string GetString()
        {
            if (vt == VT_LPWSTR && Data1 != IntPtr.Zero)
            {
                return Marshal.PtrToStringUni(Data1);
            }
            return null;
        }

        /// <summary>
        /// Clear and release resources
        /// </summary>
        public void Clear()
        {
            PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }
}
