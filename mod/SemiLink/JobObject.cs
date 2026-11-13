using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SemiLink;

/// <summary>
/// Windows job object with "kill on close": child processes assigned to it are terminated by Windows when
/// the game process exits for any reason, including a crash. Prevents an orphaned server from holding the ports.
/// </summary>
internal static class JobObject
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private static IntPtr _handle;

    public static bool TryAssign(Process process)
    {
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return false;
            }

            if (_handle == IntPtr.Zero)
            {
                var job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero)
                {
                    return false;
                }

                var info = new JobObjectExtendedLimitInformationStruct
                {
                    BasicLimitInformation = { LimitFlags = JobObjectLimitKillOnJobClose },
                };
                var size = Marshal.SizeOf(typeof(JobObjectExtendedLimitInformationStruct));
                var ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(info, ptr, false);
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)size))
                    {
                        CloseHandle(job);
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                // Intentionally never closed: the handle is released when the game exits, which kills the server.
                _handle = job;
            }

            return AssignProcessToJobObject(_handle, process.Handle);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not tie the server process to the game process: {e.Message}");
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformationStruct
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
