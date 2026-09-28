using System.Globalization;
using Wooly.Core;
using Wooly.Core.Profiles;
using Wooly.Tui.Rendering;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>
///     Adding a profile, which <c>a</c> on the profiles screen pushes: the instance, a sign-in through the browser or a
///     pasted token, the token checked, and a name — ADR-0004 as <c>profile add</c> follows it, in the order ADR-0020
///     puts it, since a name is easiest to choose once you know who it names (#245).
/// </summary>
/// <remarks>
///     What has been typed, which step the reader is on and what the last step was told lives here, for the reason
///     ADR-0015 gives about the compose editor: it is then a fact about the shell a test can set and read. What each
///     step asks of an instance or of this machine is the shell's, which holds the ports; this screen is told how that
///     went and draws it.
///     <para>
///         The sign-in in progress is this screen's too, because it belongs to the screen it was begun on: whatever
///         takes the screen off the stack gives the loopback port back (<see cref="Left" />), so a reader who walks
///         away never leaves a listener open behind them.
///     </para>
///     <para>
///         Two ways it opens besides <c>a</c>, both where the shell has nobody to act as (#247). Standing alone, it is
///         the shell's only screen: <c>esc</c> has nothing under it, so it starts the steps over rather than leaving,
///         and <c>ctrl-q</c> is offered in its place. Signing a profile in again, the instance and the name are that
///         profile's and fixed, so that what is written replaces its token rather than adding a second profile beside
///         it.
///     </para>
/// </remarks>
public sealed class AddProfileScreen : Screen
{
    /// <summary>What stands in for each letter of a token, so that none of it is ever drawn in the clear.</summary>
    public const char Mask = '*';

    /// <summary>
    ///     The caret, a mark rather than a colour, so a terminal with none still says where the typing goes.
    /// </summary>
    private const string Caret = "▌";

    /// <summary>What quits, offered where <c>esc</c> has nowhere to go.</summary>
    private static readonly KeyHint Quit = new("ctrl-q", "quit");

    /// <summary>A step back: to the list, or to the first step where the screen stands alone.</summary>
    private static readonly KeyHint Back = new("esc", "back");

    /// <summary>The profile being signed in again, whose instance and name are fixed — or none, adding one.</summary>
    private readonly ProfileSummary? _again;

    private string _instance = string.Empty;
    private string _token = string.Empty;
    private string _name = string.Empty;

    /// <summary>
    ///     What the last step was told, said on the screen rather than on the status row, where it is about.
    /// </summary>
    private string? _refusal;

    /// <summary>Where the browser was sent, and whether it went there.</summary>
    private (Uri Address, bool Opened)? _sentTo;

    /// <summary>Who the token signs in as, once an instance has said.</summary>
    private string? _account;

    /// <summary>
    ///     Called off when the reader stops waiting on a sign-in: a key, <c>esc</c>, or leaving the stack.
    /// </summary>
    private CancellationTokenSource? _signingIn;

    /// <summary>
    ///     The sign-in whose redirect is being waited on, holding the loopback port until it is given back.
    /// </summary>
    private IBrowserAuthorization? _authorization;

    /// <summary>Adds a profile from the instance up.</summary>
    /// <param name="alone">
    ///     Whether this is the shell's only screen, with nothing under it for <c>esc</c> to go back to (#247).
    /// </param>
    public AddProfileScreen(bool alone = false)
    {
        Alone = alone;
    }

    private AddProfileScreen(ProfileSummary again, string? why, bool alone)
        : this(alone)
    {
        _again = again;
        _instance = again.Instance;
        _name = again.Name;
        _account = again.Account;
        _refusal = why;
    }

    /// <summary>Where the reader is in adding a profile.</summary>
    public enum Step
    {
        /// <summary>Typing the instance.</summary>
        Instance,

        /// <summary>
        ///     The instance is being asked to register this client, before the browser can be sent anywhere.
        /// </summary>
        Registering,

        /// <summary>Sent to the browser — waiting on it to come back, or told why it did not.</summary>
        Browser,

        /// <summary>Pasting a token, the fallback ADR-0004 keeps for a machine the browser will not serve.</summary>
        Token,

        /// <summary>The token is being checked against the instance.</summary>
        Checking,

        /// <summary>Typing the name, which begins as the handle the token signs in as.</summary>
        Name,
    }

    /// <summary>
    ///     Signs <paramref name="profile" /> in again: its instance and name fixed, and its token replaced by what the
    ///     sign-in brings back (#247).
    /// </summary>
    /// <param name="profile">The profile whose token is missing or refused.</param>
    /// <param name="why">What was wrong with the token it had, said on the screen until the reader moves on.</param>
    /// <param name="alone">Whether this is the shell's only screen.</param>
    public static AddProfileScreen Again(ProfileSummary profile, string? why, bool alone) => new(profile, why, alone);

    /// <summary>Whether this is the shell's only screen, with nothing under it for <c>esc</c> to go back to.</summary>
    public bool Alone { get; }

    /// <summary>
    ///     Whether a profile already set up is being signed in again, with its instance and name fixed — which is also
    ///     why nothing is asked about the name before its token is replaced.
    /// </summary>
    public bool SignsInAgain => _again is not null;

    /// <summary>
    ///     Who the profile being signed in again signs in as on record — <c>username@instance</c> — or
    ///     <see langword="null" /> where it has not said, or none is being signed in again.
    /// </summary>
    public string? OnRecord => _again?.Account;

    /// <summary>Which step the reader is on.</summary>
    public Step At { get; private set; } = Step.Instance;

    /// <summary>Whether the browser is still out, which is what <c>⏎</c> and the waiting mark turn on.</summary>
    public bool Waiting => At == Step.Browser && _authorization is not null;

    /// <summary>The instance typed, without the whitespace a paste brings.</summary>
    public string Instance => _instance.Trim();

    /// <summary>
    ///     The token pasted, trimmed as <c>profile add</c> trims one: a paste carries whatever came with it.
    /// </summary>
    public string Token => _token.Trim();

    /// <summary>The name typed.</summary>
    public string Name => _name.Trim();

    /// <summary>Who the token signs in as, once an instance has said — <c>username@instance</c>.</summary>
    public string? Account => _account;

    /// <inheritdoc />
    public override string Crumb => SignsInAgain ? "Sign in again" : "Add a profile";

    /// <inheritdoc />
    /// <remarks>Not the instance where it is fixed, which leaves every letter nothing to go into.</remarks>
    public override bool IsTyping => At switch
    {
        Step.Instance => !SignsInAgain,
        Step.Token or Step.Name => true,
        _ => false,
    };

    /// <inheritdoc />
    /// <remarks>
    ///     Only what does something on the step the reader is on. While a field is taking letters every letter is typed
    ///     into it, so <c>t</c> is offered only while the browser is out, and <c>⏎</c> never while something is being
    ///     waited on. Standing alone, <c>esc</c> goes back to the first step, so it is not offered on it, and
    ///     <c>ctrl-q</c> is offered throughout in its place: this is the one screen a reader who wants out has.
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys => At switch
    {
        Step.Instance when Alone => [new("⏎", "sign in"), Quit],
        Step.Instance => [new("⏎", "sign in"), Back],
        Step.Registering or Step.Checking => [Back, .. Quitting],
        Step.Browser when Waiting => [new("t", "paste a token"), Back, .. Quitting],
        Step.Browser => [new("⏎", "try again"), new("t", "paste a token"), Back, .. Quitting],
        Step.Token => [new("⏎", "check"), Back, .. Quitting],
        _ => [new("⏎", "save"), Back, .. Quitting],
    };

    /// <summary><c>ctrl-q</c> where the screen stands alone, and nothing where there is somewhere to go back to.</summary>
    private IReadOnlyList<KeyHint> Quitting => Alone ? [Quit] : [];

    /// <inheritdoc />
    public override void Type(char letter)
    {
        switch (At)
        {
            case Step.Instance when !SignsInAgain:
                _instance += letter;

                break;

            case Step.Token:
                _token += letter;

                break;

            case Step.Name:
                _name += letter;

                break;
        }
    }

    /// <inheritdoc />
    public override void Backspace()
    {
        switch (At)
        {
            case Step.Instance when !SignsInAgain:
                _instance = Backspaced(_instance);

                break;

            case Step.Token:
                _token = Backspaced(_token);

                break;

            case Step.Name:
                _name = Backspaced(_name);

                break;
        }
    }

    /// <summary>
    ///     Says why the step the reader is on did not go through, on the screen, where they can do something about it.
    /// </summary>
    public void Refuse(string why) => _refusal = why;

    /// <summary>
    ///     Back to the first step, with whatever was in flight given up and the loopback port given back — what
    ///     <c>esc</c> does where this screen stands alone and there is nothing under it to go back to (#247). The
    ///     instance typed is kept, being the one thing the reader definitely meant.
    /// </summary>
    public void StartOver()
    {
        StopSigningIn();

        _token = string.Empty;
        _refusal = null;
        _sentTo = null;
        _account = _again?.Account;
        _name = _again?.Name ?? string.Empty;
        At = Step.Instance;
    }

    /// <summary>
    ///     A sign-in through the browser is starting: a fresh cancellation for it, called off whenever the reader stops
    ///     waiting (<see cref="StopSigningIn" />).
    /// </summary>
    /// <returns>What the sign-in's calls are cancelled through.</returns>
    public CancellationToken Registering()
    {
        StopSigningIn();

        _signingIn = new CancellationTokenSource();
        _refusal = null;
        At = Step.Registering;

        return _signingIn.Token;
    }

    /// <summary>The instance would not register this client, which is most often the instance mistyped.</summary>
    public void NotRegistered(string why)
    {
        StopSigningIn();

        _refusal = why;
        At = Step.Instance;
    }

    /// <summary>
    ///     The browser has been sent to <paramref name="address" />, and the redirect is being waited on.
    /// </summary>
    /// <param name="authorization">The sign-in, held until it ends or is given up.</param>
    /// <param name="address">The authorization page, drawn whether or not a browser opened at it.</param>
    /// <param name="opened">Whether a browser did.</param>
    public void Sent(IBrowserAuthorization authorization, Uri address, bool opened)
    {
        _authorization = authorization;
        _sentTo = (address, opened);
        At = Step.Browser;
    }

    /// <summary>
    ///     The browser came back without a token — turned down, or never back at all — and it is said why.
    /// </summary>
    public void NotAuthorized(string why)
    {
        StopSigningIn();

        _refusal = why;
        At = Step.Browser;
    }

    /// <summary>The browser came back with a token, and the loopback port is given back.</summary>
    public void Authorized() => StopSigningIn();

    /// <summary>
    ///     Swaps the browser for a pasted token: the wait is given up, and the field is empty and taking letters.
    /// </summary>
    public void Pasting()
    {
        StopSigningIn();

        _token = string.Empty;
        _refusal = null;
        At = Step.Token;
    }

    /// <summary>The token is being checked against the instance.</summary>
    public void Checking()
    {
        _refusal = null;
        At = Step.Checking;
    }

    /// <summary>
    ///     The instance turned the token down. Said here, and the reader is put back at the token rather than at the
    ///     start — with the field emptied, since what goes in it next is another paste.
    /// </summary>
    public void Refused(string why)
    {
        _token = string.Empty;
        _refusal = why;
        At = Step.Token;
    }

    /// <summary>
    ///     The token signs in as <paramref name="account" />, so the name is asked for — beginning as the handle, which
    ///     is who the profile names. Signing a profile in again, the name is that profile's and stays so.
    /// </summary>
    /// <param name="token">The token that was checked, kept for the write.</param>
    /// <param name="account">Who it signs in as, <c>username@instance</c>.</param>
    public void Verified(string token, string account)
    {
        _token = token;
        _account = account;
        _name = _again?.Name ?? account.Split('@')[0];
        _refusal = null;
        At = Step.Name;
    }

    /// <summary>
    ///     Stops waiting on the browser, and gives its loopback port back. Nothing where nothing is being waited on.
    /// </summary>
    public void StopSigningIn()
    {
        _signingIn?.Cancel();
        _signingIn?.Dispose();
        _signingIn = null;

        _authorization?.Dispose();
        _authorization = null;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Whatever takes this screen off the stack gives up the sign-in with it, so no listener outlives it.
    /// </remarks>
    public override void Left() => StopSigningIn();

    /// <inheritdoc />
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var width = drawing.Width;
        var lines = new List<Line> { Field("Instance: ", _instance, At == Step.Instance && IsTyping, width) };

        if (_again is { } again)
        {
            lines.Add(Field("Profile:  ", again.Name, typing: false, width));
        }

        if (_account is { } account)
        {
            lines.Add(Line.Of(TextWrap.Clip($"Account:  @{account}", width), Role.BylineHandle));
        }

        lines.Add(Line.Blank);

        switch (At)
        {
            case Step.Instance:
                Refusal(lines, width);
                Said(
                    lines,
                    SignsInAgain
                        ? "Sign in again to replace this profile's access token."
                        : "The instance the account is on, e.g. mastodon.social.",
                    width,
                    Role.Muted);

                break;

            case Step.Registering:
                Said(lines, $"Asking {Instance} to let {WoolyClient.Name} sign in...", width, Role.Muted);

                break;

            case Step.Browser:
                Browser(lines, width);

                break;

            case Step.Token:
                lines.Add(Field("Access token: ", new string(Mask, _token.Length), typing: true, width));
                lines.Add(Line.Blank);
                Refusal(lines, width);
                Said(lines, $"Paste an access token made on {Instance}.", width, Role.Muted);

                break;

            case Step.Checking:
                Said(lines, $"Checking the token with {Instance}...", width, Role.Muted);

                break;

            case Step.Name:
                lines.Add(Field("Name: ", _name, typing: true, width));
                lines.Add(Line.Blank);
                Refusal(lines, width);
                Said(lines, "What to call this profile on this machine.", width, Role.Muted);

                break;
        }

        return lines;
    }

    /// <summary>
    ///     Where the browser was sent, in <c>profile add</c>'s words, and the address itself — wrapped rather than
    ///     clipped, because it is there to be copied whole into a browser that is signed in as the right person.
    /// </summary>
    private void Browser(List<Line> lines, int width)
    {
        if (_sentTo is { } sent)
        {
            Said(
                lines,
                sent.Opened ? BrowserSignIn.Opened(Instance) : BrowserSignIn.NotOpened(Instance),
                width,
                Role.Body);

            lines.Add(Line.Blank);
            lines.AddRange(Pieces(WebAddress.Of(sent.Address), width).Select(piece => Line.Of(piece, Role.Link)));
            lines.Add(Line.Blank);
        }

        if (Waiting)
        {
            Said(lines, BrowserSignIn.Waiting, width, Role.Loading);
        }
        else
        {
            Refusal(lines, width);
        }
    }

    /// <summary>
    ///     What the last step was told, in words — so a refusal reads with no colour as well as in <c>error</c>.
    /// </summary>
    private void Refusal(List<Line> lines, int width)
    {
        if (_refusal is not { } refusal)
        {
            return;
        }

        Said(lines, refusal, width, Role.Error);
        lines.Add(Line.Blank);
    }

    private static void Said(List<Line> lines, string text, int width, Role role) =>
        lines.AddRange(TextWrap.Wrap(text, width).Select(row => Line.Of(row, role)));

    /// <summary>
    ///     A labelled field, with the caret where the next letter lands while it is taking them. Too long for the row,
    ///     it shows its end rather than its start, which is where the typing is.
    /// </summary>
    private static Line Field(string label, string typed, bool typing, int width)
    {
        var caret = typing ? Caret : string.Empty;
        var room = Math.Max(0, width - Glyphs.Columns(label) - Glyphs.Columns(caret));
        var shown = Tail(typed, room);

        return typing
            ? Line.Of(new Span(label, Role.Muted), new Span(shown, Role.Body), new Span(caret, Role.Selection))
            : Line.Of(new Span(label, Role.Muted), new Span(shown, Role.Body));
    }

    /// <summary>
    ///     The end of <paramref name="text" /> in <paramref name="room" /> columns, marked where it was cut.
    /// </summary>
    private static string Tail(string text, int room)
    {
        if (Glyphs.Columns(text) <= room)
        {
            return text;
        }

        if (room <= 1)
        {
            return room == 1 ? "…" : string.Empty;
        }

        // Stepped a grapheme at a time, so the cut never parts a base from its mark or halves a pair (#207).
        var from = StringInfo.ParseCombiningCharacters(text)
            .Append(text.Length)
            .First(at => Glyphs.Columns(text[at..]) <= room - 1);

        return "…" + text[from..];
    }

    /// <summary>
    ///     <paramref name="text" /> cut into rows of <paramref name="width" /> columns wherever it reaches the edge —
    ///     an address has no spaces to break at, and every piece of it has to be on screen.
    /// </summary>
    private static IEnumerable<string> Pieces(string text, int width)
    {
        var rest = text;

        while (rest.Length > 0 && width > 0)
        {
            var piece = Glyphs.Cut(rest, width);

            if (piece.Length == 0)
            {
                yield break;
            }

            yield return piece;

            rest = rest[piece.Length..];
        }
    }
}
