using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using FMRTE2008.Application;
using FMRTE2008.Infrastructure.Windows;

// Diagnóstico isolado: não aprova mapas e usa somente permissões de leitura.
// --first limitado a 1..20; --repeat limitado a 1..10; total máximo 200 registros.
if (!OperatingSystem.IsWindows() || args.Length < 3 ||
    args[0] != "--pid" || !int.TryParse(args[1], out int pid) || pid <= 0 ||
    args[2] != "--confirm-read-only")
{
    Console.Error.WriteLine("Uso: --pid <PID> --confirm-read-only [--first 1..20] [--repeat 1..10]");
    return 2;
}
int firstCount = 1;
int repeatCount = 1;
bool seenFirst = false, seenRepeat = false;
bool validArgs = (args.Length - 3) % 2 == 0;
for (int i = 3; validArgs && i < args.Length; i += 2)
{
    if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int value))
    {
        validArgs = false;
        break;
    }
    switch (args[i])
    {
        case "--first" when !seenFirst && value is >= 1 and <= 20:
            firstCount = value;
            seenFirst = true;
            break;
        case "--repeat" when !seenRepeat && value is >= 1 and <= 10:
            repeatCount = value;
            seenRepeat = true;
            break;
        default:
            validArgs = false;
            break;
    }
}
if (!validArgs)
{
    Console.Error.WriteLine("Parâmetros inválidos. Uso: --pid <PID> --confirm-read-only [--first 1..20] [--repeat 1..10]");
    return 2;
}

const uint preferredBase = 0x00400000;
const uint legacyHeaderVA = 0x01B1ED4C;
const uint headerRva = legacyHeaderVA - preferredBase;
const uint playerIdOffset = 256;
const int maxDiagnosticCount = 100_000;

try
{
    using var process = Process.GetProcessById(pid);
    if (process.HasExited || !string.Equals(process.ProcessName, "fm", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("PID não corresponde a fm.exe em execução");
    var module = process.MainModule ?? throw new InvalidOperationException("Módulo principal indisponível");
    string path = module.FileName;
    if (!string.Equals(Path.GetFileName(path), "fm.exe", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Arquivo principal não é fm.exe");
    var identity = ExecutableFingerprint.InspectFile(path);
    var recognized = SupportedBuildCatalog.Recognize(identity);
    if (!recognized.IsRecognized || recognized.Variant != "8.0.2v1")
        throw new UnauthorizedAccessException("Build não coincide com o fm.exe 8.0.2 / 52395 previamente identificado");
    // Não é necessário (nem permitido) promover o mapa legado para realizar este diagnóstico.
    Console.WriteLine($"Build identificada: {recognized.Variant}. Mapa operacional aprovado: NÃO.");
    ulong moduleBase = unchecked((ulong)module.BaseAddress.ToInt64());
    if (moduleBase == 0 || moduleBase > uint.MaxValue)
        throw new InvalidDataException("Base de módulo não corresponde ao espaço x86");
    ulong headerAddress = checked(moduleBase + headerRva);
    if (headerAddress > uint.MaxValue - 7ul)
        throw new InvalidDataException("Endereço calculado fora de faixa x86");

    using var handle = Native.OpenProcess(0x0010 | 0x0400, false, pid); // VM_READ | QUERY_INFORMATION
    if (handle.IsInvalid)
        throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess somente leitura falhou");
    if (process.HasExited || process.MainModule?.BaseAddress != module.BaseAddress)
        throw new InvalidOperationException("Processo mudou durante a abertura");
    var afterOpen = ExecutableFingerprint.InspectFile(path);
    if (afterOpen != identity)
        throw new UnauthorizedAccessException("Executável foi alterado durante a validação");

    Console.WriteLine($"ModuleBase=0x{moduleBase:X8}; HeaderRVA=0x{headerRva:X8}; HeaderVA=0x{headerAddress:X8}");
    Console.WriteLine($"Amostragem solicitada: primeiros {firstCount} registros, {repeatCount} leitura(s).");
    List<(uint Table, int Count, List<(uint Pointer, int Id)> Records)> samples = new();

    for (int sampleIndex = 0; sampleIndex < repeatCount; sampleIndex++)
    {
        if (process.HasExited)
            throw new InvalidOperationException("Jogo encerrado durante o diagnóstico");
        byte[] header = ReadExact(handle, headerAddress, 8);
        uint tablePtr = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        Console.WriteLine($"Amostra {sampleIndex + 1}/{repeatCount}: tabela=0x{tablePtr:X8}; quantidade={count}");
        if (tablePtr == 0 || count <= 0 || count > maxDiagnosticCount || firstCount > count)
            throw new InvalidDataException("Cabeçalho não passou nos invariantes preliminares");
        ulong lastEntryEnd = checked((ulong)tablePtr + checked((ulong)(count - 1) * 4ul) + 4ul);
        if (lastEntryEnd > (ulong)uint.MaxValue + 1)
            throw new InvalidDataException("Tabela excede o espaço x86");

        var records = new List<(uint Pointer, int Id)>();
        for (int index = 0; index < firstCount; index++)
        {
            ulong slot = checked((ulong)tablePtr + (ulong)index * 4ul);
            uint playerPtr = BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(handle, slot, 4));
            if (playerPtr == 0 || (ulong)playerPtr + playerIdOffset + 4 > (ulong)uint.MaxValue + 1)
                throw new InvalidDataException($"Ponteiro nulo ou inválido no índice {index}");
            int playerId = BinaryPrimitives.ReadInt32LittleEndian(
                ReadExact(handle, (ulong)playerPtr + playerIdOffset, 4));
            if (playerId <= 0)
                throw new InvalidDataException($"ID não positivo no índice {index}");
            records.Add((playerPtr, playerId));
            Console.WriteLine($"  [{index}] ponteiro=0x{playerPtr:X8}; id={playerId}");
        }
        samples.Add((tablePtr, count, records));
    }

    bool stable = samples.Skip(1).All(sample =>
        sample.Table == samples[0].Table &&
        sample.Count == samples[0].Count &&
        sample.Records.SequenceEqual(samples[0].Records));
    Console.WriteLine(repeatCount == 1
        ? "Estabilidade: não avaliada (uma amostra)."
        : stable ? "Estabilidade: amostras idênticas."
                 : "Estabilidade: divergência encontrada entre amostras.");
    Console.WriteLine("Diagnóstico somente leitura; mapa NÃO aprovado. Confirmar IDs com fonte independente e em outra partida.");
    return 0;
}
catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException
    or UnauthorizedAccessException or ArgumentException or OverflowException)
{
    Console.Error.WriteLine($"BLOQUEADO / NÃO VALIDADO: {error.Message}");
    return 1;
}

static byte[] ReadExact(SafeProcessHandle process, ulong address, int length)
{
    if (length <= 0 || length > 8 || address == 0 ||
        address > uint.MaxValue || address + (ulong)length > (ulong)uint.MaxValue + 1)
        throw new InvalidDataException("Leitura rejeitada: tamanho/endereço inválido");
    // Exigir páginas comprometidas, legíveis e sem guard/noaccess antes da leitura.
    nuint queried = Native.VirtualQueryEx(process, (nint)(nuint)address, out var page,
        (nuint)Marshal.SizeOf<MemoryBasicInformation>());
    if (queried == 0 || page.State != 0x1000 || (page.Protect & 0x101) != 0 ||
        (page.Protect & 0xFF) is not (0x02 or 0x04 or 0x08 or 0x20 or 0x40 or 0x80))
        throw new InvalidDataException("Região de memória não está legível/comprometida");
    ulong regionEnd = checked((ulong)page.BaseAddress + (ulong)page.RegionSize);
    if (address + (ulong)length > regionEnd)
        throw new InvalidDataException("Leitura ultrapassaria fronteira da região");
    var data = new byte[length];
    if (!Native.ReadProcessMemory(process, (nint)(nuint)address, data, (nuint)length, out nuint bytesRead))
        throw new Win32Exception(Marshal.GetLastWin32Error(), "ReadProcessMemory falhou");
    if (bytesRead != (nuint)length)
        throw new InvalidDataException($"Leitura parcial: {bytesRead}/{length}");
    return data;
}

[StructLayout(LayoutKind.Sequential)]
struct MemoryBasicInformation
{
    public nint BaseAddress;
    public nint AllocationBase;
    public uint AllocationProtect;
    public nuint RegionSize;
    public uint State;
    public uint Protect;
    public uint Type;
}

static class Native
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        [Out] byte[] buffer, nuint count, out nuint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(SafeProcessHandle process, nint address,
        out MemoryBasicInformation buffer, nuint length);
}
