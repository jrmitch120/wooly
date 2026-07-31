using Terminal.Gui.Input;

namespace Wooly.Tui.Prototype;

/// <summary>
///     C·0 — Cycle, settling. Tab moves the cursor at once — a keypress that draws nothing reads as lag — and the
///     selection follows it once the tabbing has stopped for <see cref="Settle" />, which is also when the destination
///     is asked for.
///     <para>
///         So the rail carries two marks and they are never both needed: <c>▶</c> is where the tabbing has got to, and
///         <c>▸</c> — with the row lit — is what is selected. A run of presses moves the first six times and the
///         second once, which is why six tabs are one fetch.
///     </para>
/// </summary>
internal sealed class CycleRail : RailShell
{
    /// <summary>
    ///     How still the tabbing has to be before the selection follows the cursor. Overridable through
    ///     <c>WOOLY_SETTLE_MS</c> so the gap between the two can be held open long enough to watch.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(
        int.TryParse(Environment.GetEnvironmentVariable("WOOLY_SETTLE_MS"), out var ms) ? ms : 250);

    /// <summary>Where the tabbing has got to. Moves on the keypress, every time.</summary>
    private int _cursor;

    /// <summary>What is selected, and therefore what has been asked for. Moves only when the tabbing stops.</summary>
    private int _at;

    private int _token;

    public CycleRail() : base(4)
    {
    }

    protected override string Hint => "tab/shift-tab destination · j/k post · ⏎ read · a author · esc back";

    protected override int Selected => _at;

    protected override string Prefix(int index)
    {
        if (index == _at)
        {
            return "▸  ";
        }

        return index == _cursor ? " ▶ " : "   ";
    }

    protected override bool RailKey(Key key)
    {
        if (key == Key.Tab)
        {
            Step(1);

            return true;
        }

        if (key == Key.Tab.WithShift)
        {
            Step(-1);

            return true;
        }

        return false;
    }

    private void Step(int by)
    {
        _cursor = (_cursor + by + Stops.Length) % Stops.Length;

        // Drawn now, so the key never feels swallowed. Every press abandons the settle the press before it left, so
        // only the last one in a run moves the selection and asks for anything.
        Redraw();

        var mine = ++_token;

        GetApp()?.AddTimeout(Settle, () =>
        {
            if (mine == _token)
            {
                _at = _cursor;
                Go(_at);
            }

            return false;
        });
    }
}

/// <summary>
///     C·1 — Highlight, then enter. The rail has a cursor of its own that costs nothing to move: tab puts you on the
///     rail, j/k walk it, and nothing is asked of the instance until you press enter. Walking Home → Search is one
///     fetch however long you take about it, and the rail can show you where you are going before you commit.
/// </summary>
internal sealed class HighlightRail : RailShell
{
    private bool _onRail;
    private int _at;

    public HighlightRail() : base(5)
    {
    }

    protected override string Hint => _onRail
        ? "j/k walk the rail · ⏎ go there · esc back to the feed"
        : "tab the rail · j/k post · ⏎ read · a author · esc back";

    protected override string Prefix(int index)
    {
        if (_onRail && index == _at)
        {
            return index == ShowingAt ? "▸ ▶" : "  ▶";
        }

        return index == ShowingAt ? "▸  " : "   ";
    }

    protected override bool RailKey(Key key)
    {
        if (!_onRail)
        {
            if (key != Key.Tab)
            {
                return false;
            }

            _onRail = true;
            _at = Math.Max(0, ShowingAt);
            Redraw();

            return true;
        }

        if (key == Key.CursorDown || key == Key.J || key == Key.Tab)
        {
            _at = (_at + 1) % Stops.Length;
            Redraw();

            return true;
        }

        if (key == Key.CursorUp || key == Key.K || key == Key.Tab.WithShift)
        {
            _at = (_at - 1 + Stops.Length) % Stops.Length;
            Redraw();

            return true;
        }

        if (key == Key.Enter)
        {
            _onRail = false;
            Go(_at);

            return true;
        }

        if (key == Key.Esc)
        {
            _onRail = false;
            Redraw();

            return true;
        }

        // While the rail has the cursor it keeps the keyboard, so a stray key cannot act on the post underneath.
        return true;
    }
}

/// <summary>
///     C·2 — Direct keys. The rail stops being a cursor and becomes a legend: every destination wears the key that
///     goes to it, and you never pass through anything. One keypress, one fetch, always.
///     <para>
///         The catch is the alphabet. <c>r</c> already means reply, so follow requests had to take <c>q</c> — and every
///         destination added after v1 (lists, saved searches, a second profile) has to find a letter nobody is using.
///     </para>
/// </summary>
internal sealed class DirectRail : RailShell
{
    public DirectRail() : base(6)
    {
    }

    protected override string Hint => "1-4 timelines · n notifs · d dms · q requests · s search · p profile";

    protected override string Prefix(int index) => index == ShowingAt ? $"▸{Stops[index].Key} " : $" {Stops[index].Key} ";

    protected override bool RailKey(Key key)
    {
        var pressed = (char)key.AsRune.Value;

        var at = pressed switch
        {
            '1' => 0,
            '2' => 1,
            '3' => 2,
            '4' => 3,
            'n' => 4,
            'd' => 5,
            'q' => 6,
            's' => 7,
            'p' => 8,
            _ => -1,
        };

        if (at < 0)
        {
            return false;
        }

        Go(at);

        return true;
    }
}

/// <summary>
///     C·3 — Jump list. The rail is a display, not a control: it shows where you are and what is waiting, and you get
///     anywhere by pressing <c>g</c> and typing enough of the name. Nothing is passed through, nothing needs its own
///     letter, and it keeps working when there are forty destinations instead of nine.
///     <para>
///         It costs a keystroke that a direct key does not, and it is the one shape here that has to be explained.
///     </para>
/// </summary>
internal sealed class PaletteRail : RailShell
{
    private bool _open;
    private string _typed = string.Empty;

    public PaletteRail() : base(7)
    {
    }

    protected override string Hint => _open
        ? "type to narrow · ⏎ go · esc close"
        : "g jump to… · j/k post · ⏎ read · a author · esc back";

    protected override string Prefix(int index) => index == ShowingAt ? "▸  " : "   ";

    protected override IReadOnlyList<string> Overlay
    {
        get
        {
            if (!_open)
            {
                return [];
            }

            var lines = new List<string> { $" jump to: {_typed}▏" };

            lines.AddRange(Matches().Select(at =>
                $"   {Stops[at].Label}{(Stops[at].Badge.Length > 0 ? $"  ({Stops[at].Badge} waiting)" : string.Empty)}"));

            if (lines.Count == 1)
            {
                lines.Add("   nothing by that name");
            }

            return lines;
        }
    }

    protected override bool RailKey(Key key)
    {
        if (!_open)
        {
            if (key.AsRune.Value != 'g')
            {
                return false;
            }

            _open = true;
            _typed = string.Empty;
            Redraw();

            return true;
        }

        if (key == Key.Esc)
        {
            _open = false;
            Redraw();

            return true;
        }

        if (key == Key.Enter)
        {
            var first = Matches().FirstOrDefault(-1);
            _open = false;

            if (first >= 0)
            {
                Go(first);
            }
            else
            {
                Redraw();
            }

            return true;
        }

        if (key == Key.Backspace)
        {
            _typed = _typed.Length > 0 ? _typed[..^1] : _typed;
            Redraw();

            return true;
        }

        if (key.AsRune.Value >= 32)
        {
            _typed += (char)key.AsRune.Value;
            Redraw();
        }

        return true;
    }

    private IEnumerable<int> Matches()
    {
        for (var index = 0; index < Stops.Length; index++)
        {
            if (Stops[index].Label.Contains(_typed, StringComparison.OrdinalIgnoreCase))
            {
                yield return index;
            }
        }
    }
}
