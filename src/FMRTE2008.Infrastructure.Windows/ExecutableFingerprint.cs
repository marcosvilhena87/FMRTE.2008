using System.Diagnostics;
using System.Security.Cryptography;
using FMRTE2008.Application;

namespace FMRTE2008.Infrastructure.Windows;

/// <summary>Inspeciona somente um arquivo PE local; não abre processos.</summary>
public static class ExecutableFingerprint
{
    public static ExecutableIdentity InspectFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Caminho obrigatório", nameof(path));
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException("Executável não encontrado", path);
        // Um executável do FM2008 analisado deve ser PE32 x86. Rejeita outros formatos.
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
            throw new InvalidDataException("Assinatura MZ ausente");
        stream.Position = 0x3C;
        uint peOffset = reader.ReadUInt32();
        if (peOffset > stream.Length - 24)
            throw new InvalidDataException("Cabeçalho PE fora do arquivo");
        stream.Position = peOffset;
        if (reader.ReadUInt32() != 0x00004550 || reader.ReadUInt16() != 0x014C)
            throw new InvalidDataException("Arquivo não é PE x86");
        stream.Position = 0;
        string sha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var version = FileVersionInfo.GetVersionInfo(file.FullName);
        return new ExecutableIdentity(sha, version.FileVersion ?? string.Empty,
            version.FilePrivatePart, "x86");
    }
}
