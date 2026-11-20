using System.Buffers.Binary;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class RecordConsistencyTests
{
    public static void Run()
    {
        int passed = 0;
        void Check(string title, Action action)
        {
            action();
            passed++;
            Console.WriteLine("OK: F02 " + title);
        }

        Check("registro estável é aceito", () =>
        {
            var fixture = new MutableReadFixture();
            fixture.ReadPlayerId(); // the record was read before verification
            fixture.Verify();
        });
        Check("mudança da tabela durante leitura é rejeitada", () =>
        {
            var fixture = new MutableReadFixture();
            fixture.ReadPlayerId();
            fixture.Table = 0x2500;
            Reject(fixture.Verify);
        });
        Check("mudança da quantidade durante leitura é rejeitada", () =>
        {
            var fixture = new MutableReadFixture();
            fixture.ReadPlayerId();
            fixture.Count = 3;
            Reject(fixture.Verify);
        });
        Check("mudança do ponteiro durante leitura é rejeitada", () =>
        {
            var fixture = new MutableReadFixture();
            fixture.ReadPlayerId();
            fixture.PlayerPointer = 0x3500;
            Reject(fixture.Verify);
        });
        Check("mudança entre slot e checagem final é rejeitada", () =>
        {
            var fixture = new MutableReadFixture { ChangeHeaderAfterSlot = true };
            fixture.ReadPlayerId();
            Reject(fixture.Verify);
        });
        Check("cabeçalho parcial é rejeitado", () =>
        {
            var fixture = new MutableReadFixture { PartialHeader = true };
            Reject(fixture.Verify);
        });
        Check("slot parcial é rejeitado", () =>
        {
            var fixture = new MutableReadFixture { PartialSlot = true };
            Reject(fixture.Verify);
        });
        Check("ponteiro de entrada inválido é rejeitado", () =>
        {
            var fixture = new MutableReadFixture();
            Reject(() => ReadOnlyRecordConsistency.ConfirmUnchanged(
                fixture.Read, 0x1000, 0x2000, 1, 0, 0));
        });
        Console.WriteLine($"OK: {passed} testes F02 de reconferência (memória simulada)");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("F02 deveria rejeitar registro alterado");
    }

    private sealed class MutableReadFixture
    {
        public uint Table { get; set; } = 0x2000;
        public int Count { get; set; } = 1;
        public uint PlayerPointer { get; set; } = 0x3000;
        public bool ChangeHeaderAfterSlot { get; set; }
        public bool PartialHeader { get; set; }
        public bool PartialSlot { get; set; }
        private bool _slotRead;

        public int ReadPlayerId()
        {
            // The tested record must really be read before the guard runs.
            byte[] bytes = Read(PlayerPointer + 256u, 4);
            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }

        public void Verify() => ReadOnlyRecordConsistency.ConfirmUnchanged(
            Read, 0x1000, 0x2000, 1, 0, 0x3000);

        public byte[] Read(ulong address, int length)
        {
            if (address == 0x1000 && length == 8)
            {
                if (PartialHeader) return new byte[7];
                var bytes = new byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, Table);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), Count +
                    (ChangeHeaderAfterSlot && _slotRead ? 1 : 0));
                return bytes;
            }
            if (address == 0x2000 && length == 4)
            {
                _slotRead = true;
                if (PartialSlot) return new byte[3];
                return U32(PlayerPointer);
            }
            if (address == 0x3100 && length == 4)
                return U32(107);
            throw new InvalidDataException("F02: endereço de fixture não mapeado");
        }

        private static byte[] U32(uint number)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, number);
            return bytes;
        }
    }
}
