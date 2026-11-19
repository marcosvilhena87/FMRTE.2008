using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using FMRTE2008.Domain;
using FMRTE2008.Application;

namespace FMRTE2008.Infrastructure.Windows;

/// <summary>
/// Adaptador Win32 de baixo nível, INTERNAL e somente leitura.
/// Não pode ser usado em jogo real antes da validação da build e catálogo.
/// </summary>
internal sealed class WindowsReadOnlyProcessSession : IProcessReadSession
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private readonly SafeProcessHandle _handle;

    internal WindowsReadOnlyProcessSession(int processId)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));
        _handle = Native.OpenProcess(ProcessVmRead | ProcessQueryInformation, false, processId);
        if (_handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            _handle.Dispose();
            throw new Win32Exception(error, "Não foi possível abrir o processo para leitura");
        }
    }

    public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_handle.IsClosed || _handle.IsInvalid)
            throw new ObjectDisposedException(nameof(WindowsReadOnlyProcessSession));
        if (address == 0 || length < 1 || length > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(length), "Endereço inválido ou leitura maior que 1 MiB");
        if (IntPtr.Size == 4 && address > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(address));

        byte[] buffer = new byte[length];
        bool ok = Native.ReadProcessMemory(_handle, (nint)address, buffer, (nuint)length, out nuint read);
        if (!ok)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ReadProcessMemory falhou");
        if (read != (nuint)length)
            throw new InvalidDataException($"Leitura parcial: {read} de {length} bytes");
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(buffer);
    }

    public void Dispose() => _handle.Dispose();

    private sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeProcessHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle() => Native.CloseHandle(handle);
    }

    private static class Native
    {
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
