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

    public BudgetedReadOnlySession(IProcessReadSession inner,
        int maxReads = DefaultMaxReads, long maxBytes = DefaultMaxBytes)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxReads < 1) throw new ArgumentOutOfRangeException(nameof(maxReads));
        if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        _maxReads = maxReads;
        _maxBytes = maxBytes;
    }

    public Task<byte[]> ReadAsync(nuint address, int length,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            if (_reads >= _maxReads || length > _maxBytes - _bytes)
                throw new InvalidDataException("F07: orçamento de leituras da sessão esgotado");

            // Consume the budget before invoking the underlying reader, even
            // if the read fails or is canceled. No retries on a failed session.
            _reads++;
            _bytes += length;
        }
        return _inner.ReadAsync(address, length, cancellationToken);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _inner.Dispose();
    }
}
