namespace FMRTE2008.Application;

/// <summary>
/// Read-only, deterministic resource budget for one verified process session.
/// The inner process reader is never given write permissions or write methods.
/// Each request consumes a bounded number of calls and bytes; exhaustion fails
/// closed with InvalidDataException and must invalidate the owning session.
/// </summary>
public sealed class BudgetedReadOnlySession : IProcessReadSession
{
    public const int DefaultMaxReads = 8192;
    public const long DefaultMaxBytes = 2 * 1024 * 1024;

    private readonly IProcessReadSession _inner;
    private readonly int _maxReads;
    private readonly long _maxBytes;
    private readonly object _gate = new();
    private int _reads;
    private long _bytes;
    private bool _disposed;
    private bool _innerDisposed;
    private int _activeReads;

    public BudgetedReadOnlySession(IProcessReadSession inner,
        int maxReads = DefaultMaxReads, long maxBytes = DefaultMaxBytes)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxReads < 1) throw new ArgumentOutOfRangeException(nameof(maxReads));
        if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        _maxReads = maxReads;
        _maxBytes = maxBytes;
    }

    public async Task<byte[]> ReadAsync(nuint address, int length,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            if (_reads >= _maxReads || length > _maxBytes - _bytes)
                throw new InvalidDataException("F07: orçamento de leituras da sessão esgotado");

            // Atomically reserve the budget and a lifetime lease before any
            // asynchronous operation can begin. Failed attempts are charged.
            _reads++;
            _bytes += length;
            _activeReads++;
        }
        try
        {
            byte[] bytes = await _inner.ReadAsync(address, length, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                // A disposed wrapper must never publish an in-flight result.
                ObjectDisposedException.ThrowIf(_disposed, this);
                return bytes;
            }
        }
        finally
        {
            bool close = false;
            lock (_gate)
            {
                _activeReads--;
                if (_disposed && _activeReads == 0 && !_innerDisposed)
                {
                    _innerDisposed = true;
                    close = true;
                }
            }
            if (close) _inner.Dispose();
        }
    }

    public void Dispose()
    {
        bool close = false;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_activeReads == 0 && !_innerDisposed)
            {
                _innerDisposed = true;
                close = true;
            }
        }
        if (close) _inner.Dispose();
    }
}
