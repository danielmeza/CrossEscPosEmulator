using System;

namespace CrossEscPos.Graphics;

/// <summary>
/// A resolved font at a specific pixel size, used to measure and draw receipt text. Obtained from an
/// <see cref="ITypefaceProvider"/>; the caller owns it and disposes it.
/// </summary>
public interface IReceiptFont : IDisposable
{
    /// <summary>The em size in device pixels this font was created at.</summary>
    float Size { get; }

    FontMetrics Metrics { get; }

    /// <summary>The advance width of <paramref name="text"/> in device pixels at this font's size.</summary>
    float MeasureText(string text);
}
