using System.ComponentModel;

namespace FMRTE2008.Application;

/// <summary>Exact native read outcome; failure must never return bytes.</summary>
public readonly record struct NativeReadOutcome(bool Success, nuint BytesRead, int ErrorCode);

/// <summary>
/// Offline-testable lifetime guard around a read-only native handle.
/// An in-flight read owns a lease; Dispose invalidates immediately and releases
/// the native resource exactly once after the last read finishes.
/// </summary>
public sealed class NativeReadOnlyGuard : IProcessHandleBoundReadSession
{
    private readonly Func<ProcessHandleIdentity> _inspect;
    private readonly Func<nuint, byte[], CancellationToken, Task<NativeReadOutcome>> _read;
    private readonly Action _close;
    private readonly object _gate = new();
    private bool _disposed;
    private bool _invalidated;
    private bool _reading;
    private bool _closed;

    public NativeReadOnlyGuard(
        Func<ProcessHandleIdentity> inspect,
        Func<nuint, byte[], CancellationToken, Task<NativeReadOutcome>> read,
        Action close)
    {
        _inspect = inspect ?? throw new ArgumentNullException(nameof(inspect));
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _close = close ?? throw new ArgumentNullException(nameof(close));
    }

    public ProcessHandleIdentity GetLiveHandleIdentity()
    {
        bool close = false;
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed || _invalidated, this);
                // A standalone inspection is also a resource operation. A
                // provider failure poisons the handle. If a read owns a lease,
                // closing is deferred until that read has unwound.
                try
                {
                    return _inspect();
                }
                catch
                {
                    _invalidated = true;
                    if (!_reading && !_closed)
                    {
                        _closed = true;
                        close = true;
                    }
                    throw;
                }
            }
        }
        finally
        {
            if (close) _close();
        }
    }

    public async Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken)
    {
        if (address == 0) throw new ArgumentOutOfRangeException(nameof(address));
        if (length < 1 || length > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (IntPtr.Size == 4 && address > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(address));

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed || _invalidated, this);
            if (_reading) throw new InvalidOperationException("Leitura nativa concorrente bloqueada");
            cancellationToken.ThrowIfCancellationRequested();
            _reading = true;
        }

        try
        {
            var before = _inspect();
            cancellationToken.ThrowIfCancellationRequested();
            byte[] buffer = new byte[length];
            NativeReadOutcome outcome = await _read(address, buffer, cancellationToken);
            if (!outcome.Success)
                throw new Win32Exception(outcome.ErrorCode, "ReadProcessMemory falhou");
            if (outcome.BytesRead != (nuint)length)
                throw new InvalidDataException($"Leitura nativa parcial: {outcome.BytesRead} de {length} bytes");
            cancellationToken.ThrowIfCancellationRequested();
            var after = _inspect();
            if (before != after)
                throw new InvalidOperationException("Identidade do handle mudou durante leitura");
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed || _invalidated, this);
                cancellationToken.ThrowIfCancellationRequested();
                return buffer;
            }
        }
        catch
        {
            lock (_gate) _invalidated = true;
            throw;
        }
        finally
        {
            bool close = false;
            lock (_gate)
            {
                _reading = false;
                if ((_disposed || _invalidated) && !_closed)
                {
                    _closed = true;
                    close = true;
                }
            }
            if (close) _close();
        }
    }

    public void Dispose()
    {
        bool close = false;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (!_reading && !_closed)
            {
                _closed = true;
                close = true;
            }
        }
        if (close) _close();
    }
}
