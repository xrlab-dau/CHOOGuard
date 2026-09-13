"""Lazy Windows stdlib process ownership: suspended launch -> job assignment -> resume.

Native Windows execution has not been exercised by the preparation-only authoring task.
"""
import ctypes


class WindowsJob:
    def __init__(self):
        # ctypes.WinDLL is accessed only when actually running on Windows.
        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        handle, uint, size = ctypes.c_void_p, ctypes.c_uint32, ctypes.c_size_t

        class Basic(ctypes.Structure):
            _fields_ = [("PerProcessUserTimeLimit", ctypes.c_int64), ("PerJobUserTimeLimit", ctypes.c_int64),
                        ("LimitFlags", uint), ("MinimumWorkingSetSize", size), ("MaximumWorkingSetSize", size),
                        ("ActiveProcessLimit", uint), ("Affinity", size), ("PriorityClass", uint), ("SchedulingClass", uint)]

        class Io(ctypes.Structure):
            _fields_ = [(name, ctypes.c_uint64) for name in ("ReadOperationCount", "WriteOperationCount", "OtherOperationCount",
                                                          "ReadTransferCount", "WriteTransferCount", "OtherTransferCount")]

        class Extended(ctypes.Structure):
            _fields_ = [("BasicLimitInformation", Basic), ("IoInfo", Io), ("ProcessMemoryLimit", size),
                        ("JobMemoryLimit", size), ("PeakProcessMemoryUsed", size), ("PeakJobMemoryUsed", size)]

        self.kernel.CreateJobObjectW.argtypes = (handle, ctypes.c_wchar_p)
        self.kernel.CreateJobObjectW.restype = handle
        self.kernel.SetInformationJobObject.argtypes = (handle, ctypes.c_int, handle, uint)
        self.kernel.SetInformationJobObject.restype = ctypes.c_int
        self.kernel.AssignProcessToJobObject.argtypes = (handle, handle)
        self.kernel.AssignProcessToJobObject.restype = ctypes.c_int
        self.kernel.TerminateJobObject.argtypes = (handle, uint)
        self.kernel.TerminateJobObject.restype = ctypes.c_int
        self.kernel.CloseHandle.argtypes = (handle,)
        self.kernel.CloseHandle.restype = ctypes.c_int
        self.handle = self.kernel.CreateJobObjectW(None, None)
        if not self.handle:
            raise OSError("WINDOWS_JOB_CREATE_FAILED")
        limits = Extended()
        limits.BasicLimitInformation.LimitFlags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if not self.kernel.SetInformationJobObject(self.handle, 9, ctypes.byref(limits), ctypes.sizeof(limits)):
            self.close()
            raise OSError("WINDOWS_JOB_LIMIT_FAILED")

    def assign(self, process_handle):
        if not self.kernel.AssignProcessToJobObject(self.handle, int(process_handle)):
            raise OSError("WINDOWS_JOB_ASSIGN_FAILED")

    def resume(self, pid):
        # Popen closes the initial thread handle, so find the sole suspended process
        # thread before any child code can run. Every process is already in our job.
        uint, handle = ctypes.c_uint32, ctypes.c_void_p

        class Thread(ctypes.Structure):
            _fields_ = [("dwSize", uint), ("cntUsage", uint), ("th32ThreadID", uint),
                        ("th32OwnerProcessID", uint), ("tpBasePri", ctypes.c_int32),
                        ("tpDeltaPri", ctypes.c_int32), ("dwFlags", uint)]

        k = self.kernel
        k.CreateToolhelp32Snapshot.argtypes = (uint, uint)
        k.CreateToolhelp32Snapshot.restype = handle
        k.Thread32First.argtypes = (handle, ctypes.POINTER(Thread))
        k.Thread32Next.argtypes = (handle, ctypes.POINTER(Thread))
        k.OpenThread.argtypes = (uint, ctypes.c_int, uint)
        k.OpenThread.restype = handle
        k.ResumeThread.argtypes = (handle,)
        k.ResumeThread.restype = uint
        snapshot = k.CreateToolhelp32Snapshot(4, 0)
        if snapshot == handle(-1).value:
            raise OSError("WINDOWS_THREAD_SNAPSHOT_FAILED")
        try:
            entry = Thread()
            entry.dwSize = ctypes.sizeof(entry)
            found = k.Thread32First(snapshot, ctypes.byref(entry))
            while found:
                if entry.th32OwnerProcessID == pid:
                    thread = k.OpenThread(2, False, entry.th32ThreadID)
                    if not thread:
                        raise OSError("WINDOWS_THREAD_OPEN_FAILED")
                    try:
                        if k.ResumeThread(thread) == 0xFFFFFFFF:
                            raise OSError("WINDOWS_THREAD_RESUME_FAILED")
                    finally:
                        k.CloseHandle(thread)
                    return
                found = k.Thread32Next(snapshot, ctypes.byref(entry))
            raise OSError("WINDOWS_SUSPENDED_THREAD_MISSING")
        finally:
            k.CloseHandle(snapshot)

    def terminate(self):
        if self.handle and not self.kernel.TerminateJobObject(self.handle, 1):
            raise OSError("WINDOWS_JOB_TERMINATE_FAILED")

    def close(self):
        if self.handle:
            self.kernel.CloseHandle(self.handle)
            self.handle = None
