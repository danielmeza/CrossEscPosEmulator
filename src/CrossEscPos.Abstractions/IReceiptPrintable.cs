using CrossEscPos.Graphics;

namespace CrossEscPos;

/// <summary>
/// One renderable element of a receipt (a text line, a bit image, vertical space). Draws itself onto a
/// backend-agnostic <see cref="IReceiptCanvas"/>.
/// </summary>
public interface IReceiptPrintable
{
    /// <summary>Draws this line onto the receipt canvas at the given top-left offset.</summary>
    void Render(IReceiptCanvas canvas, int offsetX, int offsetY);

    int GetPrintHeight();
}
