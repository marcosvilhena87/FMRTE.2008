using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

/// <summary>Memória x86 fictícia esparsa. Não abre processos nem oferece escrita pública.</summary>
internal sealed class SimulatedProcessMemory : IProcessReader
{
    private readonly Dictionary<uint, byte> _bytes = new();
    private readonly List<(ulong Start, ulong End)> _regions = new();

    public void AddRegion(uint start, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (start == 0 || bytes.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        ulong end = (ulong)start + (ulong)bytes.Length;
        if (end > (ulong)uint.MaxValue + 1)
            throw new OverflowException("Região excede espaço de 32 bits");
        if (_regions.Any(r => (ulong)start < r.End && (ulong)r.Start < end))
            throw new InvalidOperationException("Regiões sobrepostas");
        _regions.Add((start, end));
        for (int i = 0; i < bytes.Length; i++)
            _bytes.Add(start + (uint)i, bytes[i]);
    }

    public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (length <= 0 || length > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(length));
        if (address == 0 || (ulong)address > uint.MaxValue ||
            (ulong)address + (ulong)length > (ulong)uint.MaxValue + 1)
            throw new ArgumentOutOfRangeException(nameof(address));
        uint current = (uint)address;
        byte[] result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_bytes.TryGetValue(current + (uint)i, out result[i]))
                throw new InvalidDataException($"Endereço fictício não mapeado: 0x{current + (uint)i:X8}");
        }
        return Task.FromResult(result);
    }
}
