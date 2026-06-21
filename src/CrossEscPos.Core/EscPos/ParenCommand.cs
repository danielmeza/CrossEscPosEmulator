using System.Collections.Generic;

namespace CrossEscPos.EscPos;

/// <summary>
/// Base for the length-prefixed "ESC ( X" / "GS ( X" command family: after the prefix come
/// pL pH (little-endian byte count) followed by that many parameter bytes. Subclasses read
/// <see cref="Params"/> in <c>Execute</c>.
/// </summary>
public abstract class ParenCommand : BaseCommand
{
    public override bool HasArgs => true;

    private int _phase;   // 0 = pL, 1 = pH, 2 = params
    private int _pL;
    private int _remaining;

    protected readonly List<byte> Params = new();

    public override void Reset()
    {
        _phase = 0;
        _pL = 0;
        _remaining = 0;
        Params.Clear();
    }

    public override bool InterpretNextChar(char c)
    {
        switch (_phase)
        {
            case 0:
                _pL = (byte)c;
                _phase = 1;
                return true;
            case 1:
                _remaining = _pL + ((byte)c << 8);
                _phase = 2;
                return _remaining > 0;
            default:
                Params.Add((byte)c);
                _remaining--;
                return _remaining > 0;
        }
    }
}
