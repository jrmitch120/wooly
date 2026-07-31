using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Wooly.Core.Posts;
using Wooly.Tui.Rendering;
using Wooly.Tui.Screens;
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

    public ShellWindow(Shell.Shell shell, ITheme theme, TimeProvider clock)
    {
        _shell = shell;
        _clock = clock;

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

        if (key == Key.Esc)
        {
            _shell.Back();

            return true;
        }

        return Content(key) || base.OnKeyDown(key);
    }

    private bool Content(Key key)
    {
        if (key == Key.Enter)
        {
            _ = _shell.Enter();
        }
        else if (key == Key.A)
        {
            _ = _shell.OpenAuthor();
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
            _shell.AskToDelete();
        }
        else if (key == Key.X)
        {
            _shell.Reveal();
        }
        else if (key == Key.F5)
        {
            _ = _shell.Refresh();
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
