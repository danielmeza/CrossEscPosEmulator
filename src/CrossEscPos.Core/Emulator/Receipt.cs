using System;
using System.Collections.Generic;
using System.Linq;
using CrossEscPos;
using CrossEscPos.Graphics;
using CrossEscPos.Emulator.Printables;

namespace CrossEscPos.Emulator;

public class Receipt
{
    private readonly PaperConfiguration _paperConfiguration;
    private readonly IReceiptImageFactory _imageFactory;
    private readonly ITypefaceProvider _typefaces;

    public readonly string Guid;

    private int PaperWidth => _paperConfiguration.GetPaperWidthInPixels();
    private int PrintWidth => _paperConfiguration.GetPrintWidthInPixels();
    private int PaperMargins => (PaperWidth - PrintWidth) / 2;

    private PrintMode _printMode;
    private List<IReceiptPrintable> _renderLines;
    private ReceiptTextLine? _currentTextLine;
    private int _lineSpacing;
    private int _tabSpacing;

    public bool IsEmpty => (_currentTextLine == null || _currentTextLine.IsEmpty) && _renderLines.Count == 0;

    public Receipt(PaperConfiguration paperConfiguration, PrintMode printMode, int lineSpacing,
        IReceiptImageFactory imageFactory, ITypefaceProvider typefaces)
    {
        Guid = System.Guid.NewGuid().ToString();

        _paperConfiguration = paperConfiguration;
        _imageFactory = imageFactory;
        _typefaces = typefaces;

        _printMode = printMode;
        _renderLines = new();
        _currentTextLine = null;
        _lineSpacing = lineSpacing;
        _tabSpacing = paperConfiguration.DefaultTabSpacing;
    }

    public void ChangeFontConfiguration(PrintMode printMode)
    {
       // FinalizeTextLine(false);

        _printMode = printMode.Clone();
    }

    public void SetLineSpacing(int value)
    {
        _lineSpacing = value;
    }

    public void SetTabSpacing(int value)
    {
        _tabSpacing = value;
    }

    private ReceiptTextLine CreateNewTextLine() => new(_paperConfiguration, _printMode, _typefaces);

    public void PrintText(string text,PrintMode printMode)
    {
        if (_currentTextLine is null)
            _currentTextLine = CreateNewTextLine();

        for (var i = 0; i < text.Length; i++)
        {
            var canContinue = _currentTextLine.TryWriteChar(text[i],printMode);

            if (!canContinue)
            {
                FinalizeTextLine(false);

                _currentTextLine = CreateNewTextLine();
                canContinue = _currentTextLine.TryWriteChar(text[i],printMode);

                if (!canContinue)
                    throw new Exception("Logic error - line must be able to contain > 0 chars");
            }
        }
    }

    public void FinalizeTextLine(bool insertLineSpacing)
    {
        if (_currentTextLine != null)
        {
            if (!_currentTextLine.IsEmpty)
                _renderLines.Add(_currentTextLine);
            _currentTextLine = null;
        }

        if (insertLineSpacing)
        {
            _renderLines.Add(new ReceiptEmptyLine(_lineSpacing));
        }
    }

    public void AdvanceToNewLine() => FinalizeTextLine(true);

    public void PrintBitmap(IReceiptImage image)
    {
        FinalizeTextLine(false);

        _renderLines.Add(new ReceiptBitmapLine(_paperConfiguration, image));
    }

    public int GetTotalPrintHeight()
        => _renderLines.Sum(line => line.GetPrintHeight());

    public int GetTotalPaperHeight() =>
        GetTotalPrintHeight() + (PaperMargins * 2);

    public IReceiptImage Render(bool drawPartials = true)
    {
        var paperWidth = PaperWidth;
        var paperHeight = Math.Max(1, GetTotalPaperHeight());

        var image = _imageFactory.Create(paperWidth, paperHeight, ReceiptColor.White);
        using var canvas = _imageFactory.CreateCanvas(image);

        // Draw all rendered lines
        var offsetX = PaperMargins;
        var offsetY = PaperMargins;

        foreach (var line in _renderLines)
        {
            line.Render(canvas, offsetX, offsetY);
            offsetY += line.GetPrintHeight();
        }

        canvas.Flush();
        return image;
    }
}
