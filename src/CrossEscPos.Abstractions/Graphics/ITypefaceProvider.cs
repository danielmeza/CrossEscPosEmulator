namespace CrossEscPos.Graphics;

/// <summary>
/// Resolves a font family + style + size into a drawable <see cref="IReceiptFont"/>. Backends decide
/// how families are resolved (embedded TTF, system fonts, …). Implementations should cache.
/// </summary>
public interface ITypefaceProvider
{
    IReceiptFont GetFont(string family, bool bold, bool italic, float sizePx);
}
