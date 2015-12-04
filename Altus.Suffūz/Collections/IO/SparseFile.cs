using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Collections.IO
{
    /// <summary>
    ///  Due to wrapping native code, we'll have to pass Structs to the methods   
    /// </summary>
    #region Structs

    public struct FILE_ZERO_DATA_INFORMATION
    {
        public long _fileOffset;
        public long _beyondFinalZero;
    }

    public struct FILE_ALLOCATED_RANGE_BUFFER
    {
        public long _offset;
        public long _length;
    }

    #endregion
    /// <summary>
    /// This static class is a wrapper for native code
    /// It allows us to set a file as a sparse one,check the sparse flag,
    /// set zero ranges and query the allocated areas in the file.
    /// </summary>
    public static class SparseFile
    {
        /// <summary>
        ///  The FSCTL ints are the Io control code. 
        /// </summary>
        #region Const ints
        const uint FSCTL_SET_SPARSE = 590020;
        const uint FSCTL_SET_ZERO_DATA = 622792;
        const uint FSCTL_QUERY_ALLOCATED_RANGES = 606415;
        const int ERROR_MORE_DATA = 234;
        #endregion

        #region Methods

        #region Check Sparse
        static public bool CheckSparse(string path)  // Checks wheather a file is set as a sparse file or not
        {
            FileAttributes f = File.GetAttributes(path);
            return ((f & FileAttributes.SparseFile) == FileAttributes.SparseFile);
        }

        #endregion

        #region Set Sparse Flag
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DeviceIoControl")/* Using this enrty point will allow us to change the function's name and improve readability */]
        static extern bool DeviceIoControlSetSparseFlag([In] SafeFileHandle hDevice, [In] uint dwIoControlCode, [In] IntPtr lpInBuffer, [In] int nInBufferSize, [Out] IntPtr lpOutBuffer, [In] int nOutBufferSize, [Out] out int lpBytesReturned, [In] IntPtr lpOverlapped);

        public static void MakeSparse(FileStream f) // Setting on a file's sparse flag
        {
            int _bytes;
            if (!DeviceIoControlSetSparseFlag(f.SafeFileHandle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, out _bytes, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        #endregion

        #region Set Zero Range

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DeviceIoControl")]

        static extern bool DeviceIoControlSetZeroRange([In] SafeFileHandle hDevice, [In] uint dwIoControlCode, [In] ref FILE_ZERO_DATA_INFORMATION fZeroDataInfo, [In] int nInBufferSize, [Out] IntPtr lpOutBuffer, [In] int nOutBufferSize, out int lpBytesReturned, [In] IntPtr lpOverlapped);

        /// <summary>
        /// This function fills the requested area with zeroes 
        /// Please note that if the file is not defined as sparse it won't work!
        /// Important! Please note that the chunks the system work with are of 64kb size (64*1024),
        /// setting smaller chunks or in between chunks would be meaningless for the query
        /// </summary>
        /// <param name="f"></param>
        /// <param name="offset">start address</param>
        /// <param name="end">end address, not length</param>

        public static void SetZero(FileStream f, long offset, long end) // Please note that you send the start and end addresses, not the size
        {
            FILE_ZERO_DATA_INFORMATION _fileZero;
            _fileZero._fileOffset = offset;
            _fileZero._beyondFinalZero = end;
            int _bytes;
            if (!DeviceIoControlSetZeroRange(f.SafeFileHandle, FSCTL_SET_ZERO_DATA, ref _fileZero, Marshal.SizeOf(_fileZero), IntPtr.Zero, 0, out _bytes, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        #endregion

        #region Query allocated range

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DeviceIoControl")]

        static extern bool DeviceIoControlQueryAllocatedRanges([In] SafeFileHandle hDevice, [In] uint dwIoControlCode, [In] ref FILE_ALLOCATED_RANGE_BUFFER fAllocRangeBuffer, [In] int nInBufferSize, [Out] out FILE_ALLOCATED_RANGE_BUFFER fRanges, [In] int nOutBufferSize, out int lpBytesReturned, [In] IntPtr lpOverlapped);
        /// <summary>
        /// This function query the file and returns a list of allocated ranges (The non-zero ranges!)
        /// Please note that the file can contain zero-written areas but if the is not defined
        /// as sparse and/or the chunks are smaller than 64kb (or 128 kb if the chunk is not
        /// located in a start of a 64 kb chunk) the query will recognize those areas as allocated ones.
        /// PLEASE NOTE! This function is the only one that doesn't work well on 64-bit systems
        /// </summary>
        /// <param name="fs"></param>
        /// <returns></returns>
        public static PersistentList<FILE_ALLOCATED_RANGE_BUFFER> QueryAllocatedRanges(FileStream fs)
        {
            FILE_ALLOCATED_RANGE_BUFFER _queryRange;
            _queryRange._offset = 0;
            _queryRange._length = fs.Length;
            FILE_ALLOCATED_RANGE_BUFFER[] _ranges = new FILE_ALLOCATED_RANGE_BUFFER[64];
            PersistentList<FILE_ALLOCATED_RANGE_BUFFER> _resultList = new PersistentList<FILE_ALLOCATED_RANGE_BUFFER>();
            bool _br = false;
            int _bytesReturned;
            int _errNumber;
            do
            {
                _br = false; //In case this is not the first cycle of the loop
                if (!DeviceIoControlQueryAllocatedRanges(fs.SafeFileHandle, FSCTL_QUERY_ALLOCATED_RANGES, ref _queryRange, Marshal.SizeOf(_queryRange), out _ranges[0], _ranges.Length * Marshal.SizeOf(_queryRange), out _bytesReturned, IntPtr.Zero))
                {
                    /*If the error accors because there's some data left (the array was not big enough) the whole process will happen again and again untill all the data is transfered*/
                    if ((_errNumber = Marshal.GetLastWin32Error()) == ERROR_MORE_DATA)
                        _br = true;
                    else
                    {
                        throw new Win32Exception(_errNumber);
                    }
                }
                int _nBytes = _bytesReturned / Marshal.SizeOf(_queryRange);
                for (int i = 0; i < _nBytes; i++)
                {
                    FILE_ALLOCATED_RANGE_BUFFER _tempBuf;
                    _tempBuf._offset = _ranges[i]._offset;
                    _tempBuf._length = _ranges[i]._length;
                    _resultList.Add(_tempBuf);
                }
                if (_br)
                {
                    _queryRange._offset = _ranges[_nBytes - 1]._offset + _ranges[_nBytes - 1]._length;
                    _queryRange._length = fs.Length - _queryRange._offset;
                }

            } while (_br);
            return _resultList;
        }


        #endregion

        #endregion

    }
}
