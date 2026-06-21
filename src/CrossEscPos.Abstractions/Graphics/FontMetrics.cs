namespace CrossEscPos.Graphics;

/// <summary>
/// Vertical font metrics in device pixels, following the Skia convention used by the receipt layout:
/// <see cref="Ascent"/> is negative (distance above the baseline) and <see cref="Descent"/> is positive
/// (distance below it).
/// </summary>
public readonly record struct FontMetrics(float Ascent, float Descent);
