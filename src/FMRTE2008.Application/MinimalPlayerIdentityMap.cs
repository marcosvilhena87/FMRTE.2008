using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>
/// Mapa mínimo experimental respaldado por duas partidas do FM2008 8.0.2v1.
/// NÃO é catálogo aprovado; não contém abertura de processo, operações de escrita
/// nem autorização de acesso à interface.
/// </summary>
public sealed record MinimalPlayerIdentityMap(
    string Variant, string ExecutableSha256, uint HeaderRva, uint PlayerIdOffset,
    uint FirstNameOffset, uint LastNameOffset, uint CommonNameOffset,
    uint NameObjectInnerOffset, uint NameInnerTextOffset)
{
    public const int MaxUtf16CodeUnits = 96;

    public static readonly MinimalPlayerIdentityMap Research802V1 = new(
        "8.0.2v1", SupportedBuildCatalog.Known802Sha256,
        0x0171ED4C, 256, 280, 284, 288, 36, 8);

    public bool IsApprovedForOperationalUse => false;

    public bool Matches(ExecutableIdentity identity) =>
        SupportedBuildCatalog.Recognize(identity) is { IsRecognized: true, Variant: "8.0.2v1" } &&
        string.Equals(identity.Sha256, ExecutableSha256, StringComparison.OrdinalIgnoreCase);

    public uint ResolveHeaderAddress(uint moduleBase)
    {
        if (moduleBase == 0) throw new ArgumentOutOfRangeException(nameof(moduleBase));
        ulong address = (ulong)moduleBase + HeaderRva;
        if (address > uint.MaxValue - 7ul)
            throw new OverflowException("Cabeçalho x86 excede a faixa de leitura");
        return (uint)address;
    }
}

public sealed record MinimalPlayerIdentity(int Index, uint PlayerPointer, int Id,
    string FirstName, string LastName, string? CommonName);

/// <summary>
/// Decodificador independente da plataforma para testes em memória simulada.
/// O chamador precisa fornecer uma memória legível: esta classe nunca abre processos.
/// </summary>
public sealed class MinimalPlayerIdentityReader(IProcessReader memory, MinimalPlayerIdentityMap map)
{
    public async Task<MinimalPlayerIdentity> ReadAsync(
        PlayerListHeader header, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(map);
        if (header.Count <= 0 || header.Count > PlayerListReader.MaxFixtureEntries)
            throw new ArgumentOutOfRangeException(nameof(header));
        uint player = await new PointerTableReader(memory).ReadPointerAtIndexAsync(
            header.TableAddress, index, header.Count, cancellationToken);
        int id = unchecked((int)await ReadU32Async(Add(player, map.PlayerIdOffset), cancellationToken));
        if (id <= 0) throw new InvalidDataException("ID de jogador inválido");
        string? first = await ReadNameAsync(player, map.FirstNameOffset, cancellationToken);
        string? last = await ReadNameAsync(player, map.LastNameOffset, cancellationToken);
        string? common = await ReadNameAsync(player, map.CommonNameOffset, cancellationToken);
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
            throw new InvalidDataException("Nome ou sobrenome ausente");
        return new MinimalPlayerIdentity(index, player, id, first, last, common);
    }

    private async Task<string?> ReadNameAsync(uint player, uint offset, CancellationToken ct)
    {
        uint nameObject = await ReadU32Async(Add(player, offset), ct);
        if (nameObject == 0) return null;
        uint nested = await ReadU32Async(Add(nameObject, map.NameObjectInnerOffset), ct);
        if (nested == 0) return null;
        uint text = await ReadU32Async(Add(nested, map.NameInnerTextOffset), ct);
        if (text == 0) return null;
        var bytes = new byte[MinimalPlayerIdentityMap.MaxUtf16CodeUnits * 2];
        for (int i = 0; i < MinimalPlayerIdentityMap.MaxUtf16CodeUnits; i++)
        {
            byte[] unit = await ReadExactAsync(Add(text, (uint)(i * 2), 2), 2, ct);
            if (unit[0] == 0 && unit[1] == 0)
                return Encoding.Unicode.GetString(bytes, 0, i * 2);
            bytes[i * 2] = unit[0];
            bytes[i * 2 + 1] = unit[1];
        }
        throw new InvalidDataException("Texto sem terminador UTF-16 no limite de 96 caracteres");
    }

    private async Task<uint> ReadU32Async(uint address, CancellationToken ct) =>
        BinaryPrimitives.ReadUInt32LittleEndian(await ReadExactAsync(address, 4, ct));

    private async Task<byte[]> ReadExactAsync(uint address, int length, CancellationToken ct)
    {
        var data = await memory.ReadAsync((nuint)address, length, ct);
        if (data.Length != length)
            throw new InvalidDataException("Leitura de memória parcial");
        return data;
    }

    private static uint Add(uint pointer, uint offset, uint length = 4)
    {
        if (pointer == 0) throw new InvalidDataException("Ponteiro nulo");
        ulong address = (ulong)pointer + offset;
        if (address == 0 || address + length > (ulong)uint.MaxValue + 1ul)
            throw new OverflowException("Acesso ultrapassaria espaço x86");
        return (uint)address;
    }
}
