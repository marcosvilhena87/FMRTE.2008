using System.Buffers.Binary;

namespace FMRTE2008.Application;

/// <summary>
/// Revalidates the live header and selected table slot before accepting a record.
/// Pure read-only diagnostic guard; it cannot open a process or approve a mapping.
/// </summary>
public static class ReadOnlyRecordConsistency
{
    public static void ConfirmUnchanged(
        Func<ulong, int, byte[]> readExact,
        ulong headerAddress, uint expectedTable, int expectedCount,
        int index, uint expectedPlayerPointer)
    {
        ArgumentNullException.ThrowIfNull(readExact);
        if (headerAddress == 0 || headerAddress > uint.MaxValue - 7ul ||
            expectedTable == 0 || expectedCount <= 0 || index < 0 ||
            index >= expectedCount || expectedPlayerPointer == 0)
            throw new InvalidDataException("F02: referência inválida para reconferência");

        // First confirm table ownership and count, then re-read the selected slot.
        byte[] header = readExact(headerAddress, 8);
        if (header.Length != 8 ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)) != expectedTable ||
            BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4)) != expectedCount)
            throw new InvalidDataException("F02: cabeçalho/tabela/quantidade alterados durante a leitura");

        ulong slotAddress = checked((ulong)expectedTable + (ulong)index * 4ul);
        if (slotAddress > uint.MaxValue - 3ul)
            throw new InvalidDataException("F02: posição da tabela fora do espaço x86");
        byte[] slot = readExact(slotAddress, 4);
        if (slot.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(slot) != expectedPlayerPointer)
            throw new InvalidDataException("F02: ponteiro do jogador alterado durante a leitura");

        // Check the header once more after the slot, to catch changes between
        // the preceding reads (not an atomic snapshot; ABA remains possible).
        header = readExact(headerAddress, 8);
        if (header.Length != 8 ||
            BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)) != expectedTable ||
            BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4)) != expectedCount)
            throw new InvalidDataException("F02: cabeçalho alterado após reconferir ponteiro");
    }
}
