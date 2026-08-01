using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Core.Posts;
using Wooly.Core.Relationships;
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
    private readonly ComposeEditor _editor;
    private readonly Shell.Shell _shell;
    private readonly TimeProvider _clock;
    private readonly Action _quit;

    /// <param name="quit">
    ///     What <c>ctrl-q</c> does. Passed in rather than reached for, because the application is the thing that owns
    ///     the run loop and this window is one of the things running in it.
    /// </param>
    public ShellWindow(Shell.Shell shell, ITheme theme, TimeProvider clock, Action quit)
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

        var content = new PaintedView(theme, (width, _) => shell.Screen.Lines(width, clock.GetUtcNow()))
        {
            X = RailLines.Width + 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = false,
            FollowsSelection = true,
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

        Add(rail, breadcrumb, content, _editor, status);

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

        if (key == Key.CursorDown || key == Key.J)
        {
            _shell.Move(1);

            return true;
        }

        if (key == Key.CursorUp || key == Key.K)
        {
            _shell.Move(-1);

            return true;
        }

        if (key == Key.PageDown)
        {
            _shell.Move(10);

            return true;
        }

        if (key == Key.PageUp)
        {
            _shell.Move(-10);

            return true;
        }

        if (key == Key.Home)
        {
            _shell.Move(int.MinValue);

            return true;
        }

        if (key == Key.End)
        {
            _shell.Move(int.MaxValue);

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
    ///     A key going into a prompt rather than at the shell: a letter, or a backspace taking one back out. Anything
    ///     else — <c>⏎</c>, <c>esc</c>, <c>tab</c>, <c>ctrl-q</c> — is left to fall through, because those mean the
    ///     same thing while typing as they do everywhere else.
    /// </summary>
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
    private void Refresh()
    {
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
