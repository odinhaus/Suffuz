using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections.IO
{
    public unsafe static class MemoryMappedViewAccessorEx
    {
        static SYSTEM_INFO info;

        static MemoryMappedViewAccessorEx()
        {
            GetSystemInfo(ref info);
        }

        public static byte* Pointer(this MemoryMappedViewAccessor acc, long offset)
        {
            var num = offset % info.dwAllocationGranularity;

            byte* tmp_ptr = null;

            RuntimeHelpers.PrepareConstrainedRegions();

            acc.SafeMemoryMappedViewHandle.AcquirePointer(ref tmp_ptr);

            tmp_ptr += num;

            return tmp_ptr;
        }

        public static void Release(this MemoryMappedViewAccessor acc)
        {
            acc.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        internal struct SYSTEM_INFO
        {
            internal int dwOemId;
            internal int dwPageSize;
            internal IntPtr lpMinimumApplicationAddress;
            internal IntPtr lpMaximumApplicationAddress;
            internal IntPtr dwActiveProcessorMask;
            internal int dwNumberOfProcessors;
            internal int dwProcessorType;
            internal int dwAllocationGranularity;
            internal short wProcessorLevel;
            internal short wProcessorRevision;
        }
    }
}
