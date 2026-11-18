using System.Buffers.Binary;
using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>Resolve entradas de uma tabela de ponteiros x86, somente leitura.</summary>
/// <remarks>
/// A camada chamadora precisa obter um IProcessReader de uma sessão previamente
/// validada com hash do executável do jogo. Não usa offsets de pesquisa diretamente.
/// </remarks>
public sealed class PointerTableReader(IProcessReader processReader)
{
    public async Task<uint> ReadPointerAtIndexAsync(
        uint tableAddress, int index, int entryCount,
        CancellationToken cancellationToken = default)
    {
        if (tableAddress == 0)
            throw new ArgumentOutOfRangeException(nameof(tableAddress), "Endereço base nulo");
        if (index < 0 || entryCount <= 0 || index >= entryCount)
            throw new ArgumentOutOfRangeException(nameof(index), "Índice fora dos limites da tabela");
        cancellationToken.ThrowIfCancellationRequested();

        // 4 bytes por entrada, conforme IL de getPlayerPtrAtIndex (x86).
        uint entryAddress = checked(tableAddress + checked((uint)index * 4u));
        byte[] data = await processReader.ReadAsync((nuint)entryAddress, 4, cancellationToken);

        if (data.Length != 4)
            throw new InvalidDataException("Leitura parcial: ponteiro exige exatamente 4 bytes");

        uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (pointer == 0)
            throw new InvalidDataException("Entrada contém ponteiro nulo");
        return pointer;
    }
}
