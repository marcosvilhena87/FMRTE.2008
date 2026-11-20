using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using FMRTE2008.Application;

namespace FMRTE2008.Infrastructure.Windows;

/// <summary>
/// Windows native read-only adapter. Never called by production while
/// NoApprovedPlayerMaps denies all operational mappings.
/// </summary>
internal sealed class WindowsReadOnlyProcessSession : IProcessHandleBoundReadSession
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private readonly SafeProcessHandle _handle;
    private readonly NativeReadOnlyGuard _guard;

    internal WindowsReadOnlyProcessSession(int processId)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        _handle = Native.OpenProcess(ProcessVmRead | ProcessQueryInformation, false, processId);
        if (_handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            _handle.Dispose();
            throw new Win32Exception(error, "Não foi possível abrir processo para leitura");
        }
        try
        {
            _guard = new NativeReadOnlyGuard(InspectHandle, ReadNativeAsync, _handle.Dispose);
        }
        catch
        {
            _handle.Dispose();
            throw;
        }
    }

    public ProcessHandleIdentity GetLiveHandleIdentity() => _guard.GetLiveHandleIdentity();

    public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct) =>
        _guard.ReadAsync(address, length, ct);

    public void Dispose() => _guard.Dispose();

    private ProcessHandleIdentity InspectHandle()
    {
        if (_handle.IsClosed || _handle.IsInvalid)
            throw new ObjectDisposedException(nameof(WindowsReadOnlyProcessSession));
        uint pid = Native.GetProcessId(_handle);
        if (pid == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetProcessId falhou");
        if (!Native.GetProcessTimes(_handle, out long created, out _, out _, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetProcessTimes falhou");
        // STILL_ACTIVE=259 has a documented ambiguity with an exit code of 259.
        if (!Native.GetExitCodeProcess(_handle, out uint exitCode))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess falhou");
        if (created <= 0 || exitCode != 259)
            throw new InvalidOperationException("Processo associado ao handle foi encerrado");
        return new ProcessHandleIdentity(checked((int)pid), DateTime.FromFileTimeUtc(created).Ticks);
    }

    private Task<NativeReadOutcome> ReadNativeAsync(nuint address, byte[] buffer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool ok = Native.ReadProcessMemory(_handle, (nint)address,
            buffer, (nuint)buffer.Length, out nuint bytesRead);
        int error = ok ? 0 : Marshal.GetLastWin32Error();
        return Task.FromResult(new NativeReadOutcome(ok, bytesRead, error));
    }

    private sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeProcessHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle() => Native.CloseHandle(handle);
    }

    private static class Native
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetProcessId(SafeProcessHandle process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetProcessTimes(SafeProcessHandle process,
            out long creationTime, out long exitTime, out long kernelTime, out long userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeProcessHandle OpenProcess(
            uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadProcessMemory(
            SafeProcessHandle process, nint baseAddress,
            [Out] byte[] buffer, nuint size, out nuint bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint handle);
    }
}
