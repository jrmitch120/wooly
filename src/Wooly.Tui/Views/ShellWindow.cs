using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Core.Posts;
using Wooly.Core.Relationships;
using Wooly.Tui.Media;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The shell laid out on a terminal: the rail down the left, the breadcrumb above the content, the content, and
///     the status row along the bottom (<c>docs/tui-shell.md</c>). Everything it draws it asks the shell for, and
///     everything a key means it hands to the shell — so this file has no idea what a boost is.
/// </summary>
internal sealed class ShellWindow : Window
{
    /// <summary>
    ///     How far one press of <c>↓</c> or <c>↑</c> moves the screen. A wheel notch's worth rather than a single row:
    ///     a picture is sixteen rows, so a row a press is sixteen presses to get past one — and it is a post nobody
    ///     could reach the foot of that these keys exist for (#51).
    /// </summary>
    private const int RowsAPress = 3;

    private readonly PaintedView _content;
    private readonly ComposeEditor _editor;
    private readonly Shell.Shell _shell;
    private readonly TimeProvider _clock;
    private readonly Action _quit;

    /// <summary>
    ///     Which screen the content region is showing, so that a screen being replaced can be told apart from the same
    ///     one changing. The scroll starts again on the first and is left alone on the second.
    /// </summary>
    private Screen? _showing;

    /// <param name="quit">
    ///     What <c>ctrl-q</c> does. Passed in rather than reached for, because the application is the thing that owns
    ///     the run loop and this window is one of the things running in it.
    /// </param>
    /// <param name="pictures">Where a drawn attachment's pixels come from.</param>
    public ShellWindow(Shell.Shell shell, ITheme theme, TimeProvider clock, Action quit, IPictures pictures)
    {
        _shell = shell;
        _clock = clock;
        _quit = quit;

        // No border and no title: the contract gives the frame two rows, and both of them say something. A box around
        // the outside would cost two more and say nothing.
        BorderStyle = Terminal.Gui.Drawing.LineStyle.None;

        var rail = new PaintedView(theme, (_, height) => RailLines.Of(shell.Rail, shell.Quota, height))
        {
            X = 0,
            Y = 0,
            Width = RailLines.Width,
            Height = Dim.Fill(1),
            CanFocus = false,
        };

        var breadcrumb = new PaintedView(theme, (width, _) =>
            [ChromeLines.Breadcrumb(shell.Breadcrumb, shell.Fetching, width)])
        {
            X = RailLines.Width + 1,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };

        // The one region that shows posts, so the one region with pictures to draw in place (docs/tui-shell.md) — and
        // the one that scrolls, which is why the arrow keys below are handed to it and to nothing else.
        _content = new PaintedView(
            theme,
            (width, _) => shell.Screen.Lines(width, clock.GetUtcNow(), pictures),
            pictures)
        {
            X = RailLines.Width + 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = false,
            Scrolls = true,
        };

        _editor = new ComposeEditor(() => _ = Send(), () => shell.Back())
        {
            X = RailLines.Width + 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Visible = false,
            WordWrap = true,
        };

        var status = new PaintedView(theme, (width, _) =>
            [ChromeLines.Status(shell.Keys, shell.Notice, shell.NoticeIsError, shell.Asking, width)])
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
        };

        Add(rail, breadcrumb, _content, _editor, status);

        shell.Changed += Refresh;
    }

    /// <summary>
    ///     Every key the shell answers to. The frame's keys mean the same thing everywhere; the rest belong to
    ///     whichever screen is on top, which is why the status row always says what they are.
    /// </summary>
    protected override bool OnKeyDown(Key key)
    {
        // A confirmation is the only thing on screen worth answering, so nothing else is listened to while one is up
        // (story 43). Anything that is not the agreeing key is a no.
        if (_shell.Asking is { } asking)
        {
            _ = _shell.Answer(agreed: key == Key.Y && asking.Confirm == "y");

            return true;
        }

        // The one key that ends the run. Bound explicitly because esc is taken — it walks up the stack and never
        // quits (docs/tui-shell.md) — and Terminal.Gui's own default quit key is esc, which this window consumes.
        if (key == Key.Q.WithCtrl)
        {
            _quit();

            return true;
        }

        if (key == Key.Tab)
        {
            _shell.Step(1);

            return true;
        }

        if (key == Key.Tab.WithShift)
        {
            _shell.Step(-1);

            return true;
        }

        // A prompt taking a query takes the letters too, so that searching for "backfeed" is not a boost, an author,
        // a compose and two more besides. Which screens do that is a fact about the screen, not a mode kept here.
        if (_shell.Screen.IsTyping && Typing(key))
        {
            return true;
        }

        // The two movements that used to be one key (#51). j and k walk posts and the screen follows them; the arrows
        // walk the screen and leave the selection alone, which is the only way to read a post taller than the
        // terminal to its end.
        //
        // k is the next post and j the one before it, which is the other way round from vim (docs/tui-shell.md).
        if (key == Key.K)
        {
            Walk(1);

            return true;
        }

        if (key == Key.J)
        {
            Walk(-1);

            return true;
        }

        if (key == Key.CursorDown)
        {
            Scrolled(RowsAPress);

            return true;
        }

        if (key == Key.CursorUp)
        {
            Scrolled(-RowsAPress);

            return true;
        }

        if (key == Key.PageDown)
        {
            Turned(1);

            return true;
        }

        if (key == Key.PageUp)
        {
            Turned(-1);

            return true;
        }

        if (key == Key.Home)
        {
            Jump(int.MinValue);

            return true;
        }

        if (key == Key.End)
        {
            Jump(int.MaxValue);

            return true;
        }

        if (key == Key.Esc)
        {
            _shell.Back();

            return true;
        }

        return Content(key) || base.OnKeyDown(key);
    }

    /// <summary>
    ///     <c>j</c> and <c>k</c>: the selection walks, and the screen goes back to following it. Whether this press
    ///     moves or reclaims is the content region's to answer, since it is the only thing that knows what is on
    ///     screen — this window binds keys and knows nothing about screens (ADR-0014).
    /// </summary>
    private void Walk(int by)
    {
        _shell.Walk(by, _content.Reclaimable);
        _content.Follow();
    }

    /// <summary>
    ///     <c>↓</c> and <c>↑</c>: the screen moves and the selection stays where it is.
    /// </summary>
    /// <remarks>
    ///     The whole frame is redrawn, not the content region alone. Every other key that changes what is on screen
    ///     ends at the shell's <c>Changed</c>, which redraws everything; these two change nothing the shell knows
    ///     about, so they are the only keys that could have redrawn one region — and the column between the rail and
    ///     the content belongs to no region. It is the window's own background, so a region redrawn on its own leaves
    ///     it holding whatever the terminal last put there, which beside a picture is part of the picture.
    /// </remarks>
    private void Scrolled(int rows)
    {
        _content.Step(rows);

        SetNeedsDraw();
    }

    /// <summary>
    ///     <c>PgUp</c> and <c>PgDn</c>: the same movement a screenful at a time, rather than a run of posts. They walk
    ///     the screen because that is what a page is — a reader asking for the next page is asking about what they are
    ///     looking at, not about how many posts happen to be on it.
    /// </summary>
    private void Turned(int pages)
    {
        _content.Turn(pages);

        SetNeedsDraw();
    }

    /// <summary>
    ///     <c>Home</c> and <c>End</c>: the first post and the last. These move the selection rather than the screen,
    ///     since the ends of a list are things rather than places, and nothing is reclaimed — a reader asking for the
    ///     top of the list is not asking about the page they were on.
    /// </summary>
    private void Jump(int by)
    {
        _shell.Move(by);
        _content.Follow();
    }

    /// <summary>
    ///     A key going into a prompt rather than at the shell: a printable character, or a backspace taking one back
    ///     out. Anything that is not printable — <c>⏎</c>, <c>esc</c>, <c>tab</c>, <c>ctrl-q</c> — falls through,
    ///     because those mean the same thing while typing as they do everywhere else.
    /// </summary>
    /// <remarks>
    ///     Two of the frame's own keys do not: <c>/</c> and <c>?</c> are typed here rather than acted on, because a
    ///     web address and a question are both things somebody is entitled to search for, and a prompt that could not
    ///     take a slash would be a prompt that refuses the one query most likely to be pasted into it. It is the only
    ///     place in the shell where a frame key means something else, and the screen says so on the status row
    ///     (<c>docs/tui-shell.md</c>).
    /// </remarks>
    private bool Typing(Key key)
    {
        if (key == Key.Backspace)
        {
            _shell.Backspace();

            return true;
        }

        if (key.IsCtrl || key.IsAlt || key.AsRune.Value < ' ')
        {
            return false;
        }

        _shell.Type((char)key.AsRune.Value);

        return true;
    }

    private bool Content(Key key)
    {
        // The capitals first, and matched on the character rather than on a modifier, so that a lower-case mark key
        // can never fire a tie or empty an inbox by accident (docs/tui-shell.md).
        if (key.AsRune.Value is 'F' or 'M' or 'B')
        {
            _ = _shell.Tie(key.AsRune.Value switch
            {
                'F' => AccountTie.Follow,
                'M' => AccountTie.Mute,
                _ => AccountTie.Block,
            });
        }
        else if (key.AsRune.Value == 'D')
        {
            _shell.AskToClear();
        }
        else if (key == Key.Enter)
        {
            _ = _shell.Press(ShellKey.Enter);
        }
        else if (key == Key.A)
        {
            _ = _shell.Press(ShellKey.Author);
        }
        else if (key == Key.B)
        {
            _ = _shell.Mark(PostMark.Boost);
        }
        else if (key == Key.F)
        {
            _ = _shell.Mark(PostMark.Favorite);
        }
        else if (key == Key.P)
        {
            _ = _shell.Mark(PostMark.Pin);
        }
        else if (key == Key.M)
        {
            // Lower case only: the capitals are matched above, so this cannot be the mute a conversation has no use
            // for (docs/tui-shell.md).
            _ = _shell.MarkRead();
        }
        else if (key == Key.C)
        {
            _shell.Compose();
        }
        else if (key == Key.R)
        {
            _shell.Reply();
        }
        else if (key == Key.E)
        {
            _shell.Edit();
        }
        else if (key == Key.D)
        {
            _ = _shell.Press(ShellKey.Discard);
        }
        else if (key == Key.X)
        {
            _ = _shell.Press(ShellKey.Reject);
        }
        else if (key.AsRune.Value == '/')
        {
            _shell.Search();
        }
        else if (key.AsRune.Value == '?')
        {
            _shell.Help();
        }
        else
        {
            return false;
        }

        return true;
    }

    private async Task Send()
    {
        if (_shell.Screen is ComposeScreen compose)
        {
            // The editor is where the text was typed and the screen is where it lives; this is the one moment the two
            // have to agree.
            compose.Text = _editor.Text;
        }

        await _shell.Send();
    }

    /// <summary>
    ///     Puts the editor in front of the content while a post is being written, and takes it away again. Which of
    ///     the two is showing is a fact about the stack, not a mode this view keeps of its own.
    /// </summary>
    /// <remarks>
    ///     Also where a screen being replaced is noticed, which is what puts the scroll back to the top: pushing a
    ///     screen, popping back to one and arriving at a destination all mean different rows, and an offset the arrows
    ///     made on the last lot says nothing about this one.
    /// </remarks>
    private void Refresh()
    {
        if (!ReferenceEquals(_showing, _shell.Screen))
        {
            _showing = _shell.Screen;

            _content.Restart();
        }

        var composing = _shell.Screen is ComposeScreen;

        if (composing && !_editor.Visible)
        {
            _editor.Text = ((ComposeScreen)_shell.Screen).Text;
            _editor.Visible = true;
            _editor.SetFocus();
        }
        else if (!composing && _editor.Visible)
        {
            _editor.Visible = false;
            SetFocus();
        }

        SetNeedsDraw();
    }
}
