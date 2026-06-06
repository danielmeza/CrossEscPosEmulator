using System;
using System.Collections.Generic;
using System.Text;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.EscPos.Commands.ESC;
using ReceiptPrinterEmulator.EscPos.Commands.GS;
using ReceiptPrinterEmulator.EscPos.Commands.FS;
using ReceiptPrinterEmulator.Logging;

namespace ReceiptPrinterEmulator.EscPos;

public class EscPosInterpreter
{
    private readonly ReceiptPrinter _printer;
    private readonly StringBuilder _printBuffer;
    private readonly StringBuilder _commandBuffer;
    private readonly Dictionary<string, BaseCommand> _commandRegistry;

    private int _maxCommandPrefixLength;

    private bool _interpretingCommandPrefix;
    private bool _interpretingCommandArgs;
    private BaseCommand? _activeCommand;

    private bool _realtimeMode;
    private readonly List<int> _realtimeBuffer = new();

    public EscPosInterpreter(ReceiptPrinter printer)
    {
        _printer = printer;
        _printBuffer = new StringBuilder();
        _commandBuffer = new StringBuilder();
        _commandRegistry = new();

        _maxCommandPrefixLength = 0;

        _interpretingCommandPrefix = false;
        _interpretingCommandArgs = false;
        _activeCommand = null;

        RegisterCommands();
    }

    #region Command registry

    private void RegisterCommands()
    {
        // ESC = 0x1B
        RegisterCommand(new InitializePrinterCommand());
        RegisterCommand(new ItalicOffCommand());
        RegisterCommand(new ItalicOnCommand());
        RegisterCommand(new SelectFontCommand());
        RegisterCommand(new SelectCharsetCommand());
        RegisterCommand(new SelectCharTableCommand()); 
        RegisterCommand(new SelectJustificationCommand());
        RegisterCommand(new SetDefaultLineSpacingCommand());
        RegisterCommand(new SetLineSpacingCommand());
        RegisterCommand(new ToggleEmphasizeCommand());
        RegisterCommand(new ToggleUnderlineCommand());
        RegisterCommand(new SetPrintTextMode()); // 0x1B, 0x21, n
        RegisterCommand(new PaperFullCut()); // 0x1B, 0x6D
        RegisterCommand(new PaperPartialCut()); // 0x1B, 0x69
        RegisterCommand(new PaperPrintFeednLines()); // 0x1B, 0x64
        RegisterCommand(new PaperPrintFeed()); // 0x1B, 0x4A
        RegisterCommand(new GeneratePulseCommand()); // 0x1B, 0x70 (cash drawer kick)
        
        // FS = 0x1C
        RegisterCommand(new PrintStoredLogo()); // 0x1C, 0x70, n, m
        RegisterCommand(new PaperAutoCut()); // 0x1C, 0x7D, 0x60, n
        
        // GS = 0x1D
        RegisterCommand(new SelectCharacterSizeCommand());
        RegisterCommand(new SelectCutModeAndCutCommand());
        RegisterCommand(new PaperEjectCommand()); // 0x1D, 0x65, n, [m, t]
        RegisterCommand(new PrintRasterBitImageCommand());

        // GS - barcodes (1D) and QR (2D)
        RegisterCommand(new SetBarcodeHeightCommand());   // 0x1D, 0x68, n
        RegisterCommand(new SetBarcodeWidthCommand());    // 0x1D, 0x77, n
        RegisterCommand(new SelectHriPositionCommand());  // 0x1D, 0x48, n
        RegisterCommand(new SelectHriFontCommand());      // 0x1D, 0x66, n
        RegisterCommand(new PrintBarcodeCommand());       // 0x1D, 0x6B, m ...
        RegisterCommand(new PrintQrCommand());            // 0x1D, 0x28, 0x6B, ...

        // GS - status / transmit-back
        RegisterCommand(new TransmitStatusCommand());     // 0x1D, 0x72, n
        RegisterCommand(new TransmitPrinterIdCommand());  // 0x1D, 0x49, n
        RegisterCommand(new EnableAutoStatusBackCommand()); // 0x1D, 0x61, n

        // Buzzer / configuration
        RegisterCommand(new BeeperCommand());             // ESC ( A — manufacturer beeper
        RegisterCommand(new SetMotionUnitsCommand());     // GS P x y
        RegisterCommand(new Commands.NoOpParenCommand(EscPosInterpreter.GS + "(E", "GS ( E user setup"));
        RegisterCommand(new Commands.NoOpParenCommand(EscPosInterpreter.GS + "(K", "GS ( K print control"));
        RegisterCommand(new Commands.NoOpParenCommand(EscPosInterpreter.GS + "(H", "GS ( H response request"));
    }

    private void RegisterCommand(BaseCommand command)
    {
        var prefix = command.Prefix;

        if (_commandRegistry.ContainsKey(prefix))
            throw new ArgumentException($"Cannot register command with duplicate prefix: {prefix}");

        _commandRegistry.Add(prefix, command);

        if (prefix.Length > _maxCommandPrefixLength)
            _maxCommandPrefixLength = prefix.Length;
    }

    #endregion

    #region Buffers

    public void ClearBuffers()
    {
        FinalizePrintBuffer();
        FinalizeCommandBuffer();
    }
    
    private string FinalizePrintBuffer()
    {
        var result = _printBuffer.ToString();
        _printBuffer.Clear();
        return result;
    }

    private string FinalizeCommandBuffer()
    {
        var result = _commandBuffer.ToString();
        _commandBuffer.Clear();
        return result;
    }

    #endregion

    /// <summary>
    /// Dispatches a real-time DLE command once enough bytes have accumulated. Returns true when the
    /// command is complete (or unsupported and abandoned), false when more bytes are needed.
    /// </summary>
    private bool TryDispatchRealtime()
    {
        int first = _realtimeBuffer[0];
        switch (first)
        {
            case 0x04: // DLE EOT n — real-time status
                if (_realtimeBuffer.Count < 2) return false;
                _printer.TransmitRealtimeStatus(_realtimeBuffer[1]);
                return true;

            case 0x05: // DLE ENQ — real-time recover
                _printer.RealtimeRecover();
                return true;

            case 0x14: // DLE DC4 fn ... — real-time request
                if (_realtimeBuffer.Count < 2) return false;
                int fn = _realtimeBuffer[1];
                if (fn == 0x01) // generate pulse (cash drawer): DLE DC4 1 m t
                {
                    if (_realtimeBuffer.Count < 4) return false;
                    _printer.KickCashDrawer(_realtimeBuffer[2]);
                    return true;
                }
                Logger.Info($"Unsupported real-time DLE DC4 fn={fn}");
                return true;

            default:
                Logger.Info($"Unsupported real-time DLE sequence: 0x{first:X2}");
                return true;
        }
    }

    public void Interpret(string ascii)
    {
        for (var i = 0; i < ascii.Length; i++)
        {
            var currentChar = ascii[i];

            // Real-time commands (DLE ...) bypass the print buffer and are answered immediately.
            if (_realtimeMode)
            {
                _realtimeBuffer.Add((byte)currentChar);
                if (TryDispatchRealtime())
                {
                    _realtimeMode = false;
                    _realtimeBuffer.Clear();
                }
                continue;
            }

            #region Command modes

            if (_interpretingCommandArgs)
            {
                // Reading command args: keep reading until the command is done interpreting
                _commandBuffer.Append(currentChar);

                var shouldContinue = _activeCommand!.InterpretNextChar(currentChar);

                if (!shouldContinue)
                {
                    var finalArgs = FinalizeCommandBuffer();

                    Logger.Info($"Execute [{_activeCommand.GetType().Name}] with args [{(finalArgs.Length > 8 ? $"{finalArgs[..8]}..." : finalArgs)}]");

                    _activeCommand.Execute(_printer, finalArgs);
                    _activeCommand = null;

                    _interpretingCommandPrefix = false;
                    _interpretingCommandArgs = false;
                }

                continue;
            }

            if (_interpretingCommandPrefix)
            {
                // Reading command prefix: keep reading until we find a match or hit _maxCommandPrefixLength
                _commandBuffer.Append(currentChar);

                var commandText = _commandBuffer.ToString();

                if (commandText.Length > _maxCommandPrefixLength)
                {
                	string byteText;
                	
                	if (i > 0) byteText = string.Format("0x{0:X2} 0x{1:X2}", (int)ascii[i - 1], (int)ascii[i]);
                	else byteText = string.Format("0x{0:X2}", (int)ascii[i]);
                	
                	throw new InvalidOperationException("Invalid or unsupported command encountered: " + byteText);
                }

                if (_commandRegistry.ContainsKey(commandText))
                {
                    // Found matching registered command
                    _activeCommand = _commandRegistry[commandText];
                    _activeCommand.Reset();
                    
                    _commandBuffer.Clear();

                    if (_activeCommand.HasArgs)
                    {
                        // This command has arguments: begin interpreting those
                        _interpretingCommandPrefix = false;
                        _interpretingCommandArgs = true;
                    }
                    else
                    {
                        // This command has NO arguments: execute immediately and return to normal mode
                        _interpretingCommandPrefix = false;
                        _interpretingCommandArgs = false;

                        Logger.Info($"Execute [{_activeCommand.GetType().Name}]");

                        _activeCommand.Execute(_printer, null);
                        _activeCommand = null;
                    }
                }

                continue;
            }

            #endregion

            #region Normal mode

            if (currentChar == BEL)
            {
                // Bell — sound the buzzer/beeper
                _printer.Buzz();
                continue;
            }

            if (currentChar == HT)
            {
                // Horizontal tab
                _printer.PrintTab();
            }

            if (currentChar == LF || currentChar == CR)
            {
                // Print and line feed
                _printer.PrintAndLineFeed(FinalizePrintBuffer());
                continue;
            }

            if (currentChar == FF)
            {
                // Print and return to Standard mode (in Page mode)
                //throw new NotImplementedException("Not supported: page mode");
                continue;
            }

            if (currentChar == DLE)
            {
                // Prefix for real-time commands (status, pulse/drawer, recover, etc).
                _realtimeMode = true;
                _realtimeBuffer.Clear();
                continue;
            }

            if (currentChar == CAN)
            {
                // Cancel print data in the current page-mode area (page mode handled below).
                _printer.CancelPageData();
                continue;
            }

            // (page-mode FF/CAN are wired through the printer's page-mode methods)

            if (currentChar == ESC || currentChar == FS || currentChar == GS)
            {
                // ESC, FS and GS commands - begin command mode
                _printer.PrintText(FinalizePrintBuffer());
                _interpretingCommandPrefix = true;

                _commandBuffer.Clear();
                _commandBuffer.Append(currentChar);
                continue;
            }

            if (currentChar == NUL)
            {
                // Null byte outside of command context; do nothing
                continue;
            }

            // Regular character, not in command mode: append to print buffer
            _printBuffer.Append(currentChar);

            #endregion
        }
    }

    public static readonly char NUL = Convert.ToChar(0);
    public static readonly char BEL = Convert.ToChar(7);  // 0x07
    public static readonly char HT = Convert.ToChar(9);
    public static readonly char LF = Convert.ToChar(10);  // 0x0A
    public static readonly char FF = Convert.ToChar(12);  // 0x0C
    public static readonly char CR = Convert.ToChar(13);  // 0x0D
    public static readonly char DLE = Convert.ToChar(16); // 0x10
    public static readonly char CAN = Convert.ToChar(24); // 0x18
    public static readonly char ESC = Convert.ToChar(27); // 0x1B
    public static readonly char FS = Convert.ToChar(28);  // 0x1C
    public static readonly char GS = Convert.ToChar(29);  // 0x1D
}