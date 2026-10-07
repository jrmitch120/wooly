using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Wooly.Core.Posts;
using Wooly.Tui.Screens;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Views;

/// <summary>
///     The list of languages hung under compose's Lang (ADR-0024, #340): the people-to-mention list's machinery
///     (<see cref="PickList{T}" />) offering whatever the field's text matches. It opens as a code or a name is typed,
///     and on a click or <c>⏎</c> in the field; picking a language writes it into the field, and closing the list
///     — by <c>esc</c>, by a click elsewhere, or by the typing leaving Lang — leaves the field as it was.
/// </summary>
internal sealed class LanguageList
{
    /// <summary>The list's <c>Id</c> among compose's views.</summary>
    internal const string Id = "languages";

    private readonly Shell.Shell _shell;
    private readonly ComposeLangField _field;
    private readonly View _over;
    private readonly PickList<PostLanguage> _list;

    /// <summary>Whether the field's text is being set from here rather than typed, which opens nothing.</summary>
    private bool _filling;

    /// <param name="field">Lang, which the list hangs under.</param>
    /// <param name="over">
    ///     The view Lang is laid in, over the content viewport: this list is laid beside Lang in it, and kept inside it.
    /// </param>
    public LanguageList(ITheme theme, Shell.Shell shell, ComposeLangField field, View over)
    {
        _shell = shell;
        _field = field;
        _over = over;

        _list = new PickList<PostLanguage>(
            theme,
            Id,
            LanguageLines.Row,
            LanguageLines.Columns,
            Pick,
            Close,
            picks: "choose");

        View.X = Pos.Func(_ => field.Frame.X);
        View.Y = Pos.Func(_ => field.Frame.Bottom);
        View.Width = Dim.Func(_ => Size().Width);
        View.Height = Dim.Func(_ => Size().Height);

        field.Ahead = Took;
        field.Asked = Ask;
        field.TextChanged += (_, _) => Typed();
        field.HasFocusChanged += (_, focus) =>
        {
            if (!focus.NewValue)
            {
                Close();
            }
        };
    }

    /// <summary>The list as drawn, which compose's view lays over whatever is under Lang.</summary>
    public PaintedView View => _list.View;

    /// <summary>
    ///     Puts <paramref name="text" /> in the field as the screen holds it — as compose opens, or as a language is
    ///     picked — rather than as typed, so it opens no list.
    /// </summary>
    public void Fill(string text)
    {
        _filling = true;

        try
        {
            _field.Text = text;
            _field.MoveEnd();
        }
        finally
        {
            _filling = false;
        }
    }

    /// <summary>Closes the list without changing what Lang holds.</summary>
    public void Close()
    {
        if (!_list.Open)
        {
            return;
        }

        _list.Offer([], keepingThePick: false);
        _shell.ChangeCompose(compose => compose.OfferLanguages(false));
    }

    /// <summary>First refusal on a mouse event anywhere: a click outside the open list closes it.</summary>
    public bool ClosedBy(Mouse mouse) => _list.ClosedBy(mouse);

    /// <summary>The list's own keys while it is open (<see cref="PickList{T}.Took" />).</summary>
    private bool Took(Key key) => _list.Took(key);

    /// <summary>A click on Lang or <c>⏎</c> in it: the list opened on every language, picked on the one Lang holds.</summary>
    private void Ask()
    {
        if (_shell.Screen is not ComposeScreen compose)
        {
            return;
        }

        var offered = compose.Lang.Offered;
        var held = compose.Lang.Language is { } language ? IndexOf(offered, language) : 0;

        Offer(offered, held);
    }

    /// <summary>The field's text changed: the screen told, and the list narrowed to what it now matches.</summary>
    private void Typed()
    {
        if (_filling)
        {
            return;
        }

        _shell.ChangeCompose(compose => compose.RewriteLanguage(_field.Text));

        if (_shell.Screen is not ComposeScreen || _field.Text.Trim().Length == 0)
        {
            Close();

            return;
        }

        Offer(PostLanguageName.Matching(_field.Text), 0);
    }

    private void Offer(IReadOnlyList<PostLanguage> languages, int picking)
    {
        var was = _list.Open;

        _list.Offer(languages, keepingThePick: false, picking);

        if (_list.Open != was)
        {
            _shell.ChangeCompose(compose => compose.OfferLanguages(_list.Open));
        }
    }

    /// <summary>A language picked: written into Lang, and the list closed, with the typing still in Lang.</summary>
    private void Pick(PostLanguage language)
    {
        _shell.ChangeCompose(compose => compose.PickLanguage(language));

        if (_shell.Screen is ComposeScreen compose)
        {
            Fill(compose.Lang.Held);
        }

        Close();
    }

    private static int IndexOf(IReadOnlyList<PostLanguage> languages, PostLanguage language)
    {
        for (var at = 0; at < languages.Count; at++)
        {
            if (languages[at] == language)
            {
                return at;
            }
        }

        return 0;
    }

    /// <summary>
    ///     How big the list is drawn: wide enough for its widest language, and no further right or down than the content
    ///     viewport Lang is laid in.
    /// </summary>
    private Size Size()
    {
        var viewport = _over.Viewport.Size;

        return _list.Within(
            Math.Max(0, viewport.Width - _field.Frame.X),
            Math.Max(0, viewport.Height - _field.Frame.Bottom));
    }
}
