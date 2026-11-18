using System.Buffers.Binary;
using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>
/// Decodifica o caminho de leitura observado no IL do FMRTE legado:
/// header -> [table pointer, count] -> table[index] -> player + idOffset.
/// Não seleciona builds nem conhece endereços operacionais.
/// </summary>
public sealed class PlayerListReader(IProcessReader memory)
{
    // Limite defensivo do protótipo; não representa um limite do FM2008.
    public const int MaxFixtureEntries = 100_000;

    public async Task<PlayerListHeader> ReadHeaderAsync(
        uint headerAddress, CancellationToken cancellationToken = default)
    {
        if (headerAddress == 0 || headerAddress > uint.MaxValue - 7)
            throw new ArgumentOutOfRangeException(nameof(headerAddress));

        var bytes = await memory.ReadAsync((nuint)headerAddress, 8, cancellationToken);
        if (bytes.Length != 8)
            throw new InvalidDataException("Cabeçalho incompleto: esperados 8 bytes");

        uint tableAddress = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        if (tableAddress == 0)
            throw new InvalidDataException("Ponteiro da tabela é nulo");
        if (count <= 0 || count > MaxFixtureEntries)
            throw new InvalidDataException("Quantidade de jogadores fora do intervalo do simulador");
        // Checagem antecipada de overflow para a última entrada da tabela x86.
        _ = checked(tableAddress + checked((uint)(count - 1) * 4u));
        return new PlayerListHeader(tableAddress, count);
    }

    public async Task<PlayerIdRecord> ReadPlayerIdAsync(
        PlayerListHeader header, int index, int playerIdOffset,
        CancellationToken cancellationToken = default)
    {
        if (playerIdOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(playerIdOffset));
        if (header.Count <= 0 || header.Count > MaxFixtureEntries)
            throw new ArgumentOutOfRangeException(nameof(header));
        uint playerAddress = await new PointerTableReader(memory).ReadPointerAtIndexAsync(
            header.TableAddress, index, header.Count, cancellationToken);
        uint address = checked(playerAddress + (uint)playerIdOffset);
        var bytes = await memory.ReadAsync((nuint)address, 4, cancellationToken);
        if (bytes.Length != 4)
            throw new InvalidDataException("ID incompleto: esperados 4 bytes");
        int id = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (id <= 0)
            throw new InvalidDataException("ID fictício inválido");
        return new PlayerIdRecord(index, playerAddress, id);
    }
}

public sealed record PlayerListHeader(uint TableAddress, int Count);
public sealed record PlayerIdRecord(int Index, uint PlayerAddress, int Id);
