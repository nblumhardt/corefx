﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    internal partial class mincore
    {
        [DllImport(Libraries.CoreFile_L1, EntryPoint = "GetFileAttributesExW", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern bool GetFileAttributesEx(string name, GET_FILEEX_INFO_LEVELS fileInfoLevel, ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);

        internal enum GET_FILEEX_INFO_LEVELS : uint
        {
            GetFileExInfoStandard = 0x0u,
            GetFileExMaxInfoLevel = 0x1u,
        }

        internal struct WIN32_FILE_ATTRIBUTE_DATA
        {
            internal int fileAttributes;
            internal uint ftCreationTimeLow;
            internal uint ftCreationTimeHigh;
            internal uint ftLastAccessTimeLow;
            internal uint ftLastAccessTimeHigh;
            internal uint ftLastWriteTimeLow;
            internal uint ftLastWriteTimeHigh;
            internal uint fileSizeHigh;
            internal uint fileSizeLow;

            internal void PopulateFrom(ref WIN32_FIND_DATA findData)
            {
                // Copy the information to data
                fileAttributes = (int)findData.dwFileAttributes;
                ftCreationTimeLow = findData.ftCreationTime.dwLowDateTime;
                ftCreationTimeHigh = findData.ftCreationTime.dwHighDateTime;
                ftLastAccessTimeLow = findData.ftLastAccessTime.dwLowDateTime;
                ftLastAccessTimeHigh = findData.ftLastAccessTime.dwHighDateTime;
                ftLastWriteTimeLow = findData.ftLastWriteTime.dwLowDateTime;
                ftLastWriteTimeHigh = findData.ftLastWriteTime.dwHighDateTime;
                fileSizeHigh = findData.nFileSizeHigh;
                fileSizeLow = findData.nFileSizeLow;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        [BestFitMapping(false)]
        internal unsafe struct WIN32_FIND_DATA
        {
            internal uint dwFileAttributes;
            internal FILE_TIME ftCreationTime;
            internal FILE_TIME ftLastAccessTime;
            internal FILE_TIME ftLastWriteTime;
            internal uint nFileSizeHigh;
            internal uint nFileSizeLow;
            internal uint dwReserved0;
            internal uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            internal string cAlternateFileName;
        }

        internal struct FILE_TIME
        {
            internal uint dwLowDateTime;
            internal uint dwHighDateTime;

            internal FILE_TIME(long fileTime)
            {
                dwLowDateTime = (uint)fileTime;
                dwHighDateTime = (uint)(fileTime >> 32);
            }

            internal long ToTicks()
            {
                return ((long)dwHighDateTime << 32) + dwLowDateTime;
            }
        }
    }
}
