using System.IO;

namespace CrossEscPos.Graphics;

/// <summary>Encodes an <see cref="IReceiptImage"/> to PNG — the portable handoff to UI hosts and export.</summary>
public interface IImageEncoder
{
    void EncodePng(IReceiptImage image, Stream destination);

    byte[] EncodePng(IReceiptImage image);
}
