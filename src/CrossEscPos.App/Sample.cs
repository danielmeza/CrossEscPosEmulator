using System;
using System.IO;

namespace CrossEscPos.App;

/// <summary>The bundled sample ESC/POS ticket (text + barcodes + QR), embedded in the shared app.</summary>
public static class Sample
{
    public static byte[] Ticket { get; } = Load();

    private static byte[] Load()
    {
        using var stream = typeof(Sample).Assembly.GetManifestResourceStream("sample.escpos");
        if (stream is null)
            return Array.Empty<byte>();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
