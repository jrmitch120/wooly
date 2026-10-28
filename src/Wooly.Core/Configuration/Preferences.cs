using Wooly.Core.Posts;

namespace Wooly.Core.Configuration;

/// <summary>Settings that change how this client behaves, rather than who it talks to.</summary>
public sealed record Preferences
{
    /// <summary>
    ///     The visibility a new post gets when the command line does not say, or <see langword="null" /> to leave the
    ///     choice to the instance's own default.
    /// </summary>
    public PostVisibility? DefaultVisibility { get; init; }

    /// <summary>
    ///     The code of the language a new post is in when the command line does not say (see
    ///     <see cref="PostLanguageName" />), or <see langword="null" /> to leave it to the instance. Never applied to an
    ///     edit, where saying nothing means leaving the post's language as it was.
    /// </summary>
    public string? DefaultLanguage { get; init; }

    /// <summary>
    ///     The hashtag the TUI's rail keeps a destination for, without its leading <c>#</c>, or <see langword="null" />
    ///     where the reader has not named one yet. A setting rather than a fixed tag because the four timelines are the
    ///     same four for everybody and a tag worth a permanent place on the rail is not.
    /// </summary>
    public string? Hashtag { get; init; }

    /// <summary>
    ///     Whether a picture's caption, <c>Described</c>, hides once the picture is actually drawn on screen.
    ///     <see langword="false" /> (absent, today's behavior) always shows it. A terminal that cannot draw at all and
    ///     one that can but has not gotten this picture yet both keep showing it — there is no reveal key once hidden
    ///     (#71, <c>docs/tui-shell.md</c> "What media settled").
    /// </summary>
    public bool HideDrawnCaption { get; init; }

    /// <summary>
    ///     Whether the TUI writes the terminal's bell when an Inbox badge rises (#399). <see langword="true" /> (absent)
    ///     rings it; one setting for every profile, with no key in the TUI to change it yet.
    /// </summary>
    public bool Bell { get; init; } = true;

    /// <summary>The narrowest <see cref="ShellWidth" /> there is: half the terminal.</summary>
    public const int NarrowestShellWidth = 50;

    /// <summary>The widest <see cref="ShellWidth" />: all of the terminal, which is what an absent key means.</summary>
    public const int FullShellWidth = 100;

    /// <summary>
    ///     The TUI's <b>Shell width</b> (#413): how much of the terminal's width the shell takes, as a whole-number
    ///     percentage from <see cref="NarrowestShellWidth" /> to <see cref="FullShellWidth" />, which the config file's
    ///     reader holds it to. <see cref="FullShellWidth" /> (absent) is all of it; one setting for every profile.
    /// </summary>
    public int ShellWidth { get; init; } = FullShellWidth;

    /// <summary>
    ///     Whether the TUI opens on the <b>hidden rail</b> (#442, #445): <see langword="false" /> (absent) opens on the
    ///     shown rail, which a fresh install starts on so that the destinations are seen before they are put away. One
    ///     setting for every profile, as the shell width is.
    /// </summary>
    public bool HiddenRail { get; init; }

    /// <summary>
    ///     Whether the TUI's content panel draws its <b>Scrollbar</b> on its right edge where a screen's rows don't fit
    ///     the page (#500, #501). <see langword="true" /> (absent) draws it; one setting for every profile, read at
    ///     launch, with no key in the TUI to change it.
    /// </summary>
    public bool Scrollbar { get; init; } = true;
}
