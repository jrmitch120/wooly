using System.Drawing;
using System.Globalization;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Rendering;
using Wooly.Tui.Shell;
using Wooly.Tui.Theme;

namespace Wooly.Tui.Screens;

/// <summary>What a compose screen was opened to do, which is the only thing that differs between the three.</summary>
public enum ComposeFor
{
    /// <summary>A post answering nothing.</summary>
    Post,

    /// <summary>A reply to the post the screen was opened from.</summary>
    Reply,

    /// <summary>A change to one of the profile's own posts.</summary>
    Edit,
}

/// <summary>
///     Where on a compose screen the typing is going: one of the headers that takes typing, or the post itself
///     (ADR-0024, #337).
/// </summary>
public enum ComposeField
{
    /// <summary>Who the post goes to: its visibility, chosen on a row of radio buttons (#338).</summary>
    To,

    /// <summary>What language the post is in: a one-line field with a list of languages under it (#340).</summary>
    Lang,

    /// <summary>The content warning over the post (#123, #320).</summary>
    Warning,

    /// <summary>
    ///     The Media header, which types nothing: walked to so that its keys have somewhere to land — <c>s</c> for the
    ///     sensitive toggle at the end of its line (#379).
    /// </summary>
    Media,

    /// <summary>One of the rows under the Media header: the pending attachment <see cref="ComposeScreen.PickedAttachment" /> names.</summary>
    Attachment,

    /// <summary>The post being written.</summary>
    Post,
}

/// <summary>
///     What an edit made on a compose screen changed, which is all the shell needs to settle what follows from it
///     (#364): whether a notice over the draft is spent, and whether the screen is drawn again.
/// </summary>
public enum ComposeChange
{
    /// <summary>Nothing: the edit found the screen already as it would have left it.</summary>
    None,

    /// <summary>
    ///     The typing or the choosing moved, or a list opened or closed — focus, walking the fields, choosing on To —
    ///     with the post, its warning and its language as they were.
    /// </summary>
    Moved,

    /// <summary>The post's text, its warning or its language changed.</summary>
    Edited,
}

/// <summary>Who a compose screen's post goes out as, for its From header (#317).</summary>
/// <param name="Handle">The profile's account, as a byline's handle: <c>@jeff</c>.</param>
/// <param name="Instance">The instance it is on, which tells two profiles with the same username apart.</param>
public sealed record ComposeFrom(string Handle, string Instance)
{
    /// <summary>
    ///     Who <paramref name="profile" /> posts as: its account's username, or the profile's own name where the
    ///     account has not been verified, on the instance the profile signs in to.
    /// </summary>
    public static ComposeFrom Of(ActiveProfile profile) => new(profile.Handle ?? profile.Name, profile.Instance);
}

/// <summary>
///     Writing a post: a new one, a reply, or a change to one already published. A screen pushed onto the stack like
///     any other, which is what <c>docs/tui-shell.md</c> left open and ADR-0015 settles.
/// </summary>
/// <remarks>
///     The text itself lives here rather than in the editor widget, so that what is being written is a fact about the
///     shell — something a test can set and read — rather than something only a terminal knows.
/// </remarks>
public sealed class ComposeScreen : Screen
{
    /// <summary>What To says while it is the account's own default on the instance, which this screen cannot see.</summary>
    private const string AccountDefault = "account default";

    /// <summary>The arrows either side of To's one value where the whole row does not fit (#338).</summary>
    private const string StepBack = "◂";

    private const string StepOn = "▸";

    /// <summary>What Lang says while it is empty, which sends no language and leaves it to the instance (#340).</summary>
    private const string NoLanguage = "none · the instance decides";

    /// <summary>What Lang says while it is empty and is being typed in.</summary>
    private const string LanguageBeingTyped = "type a code or a name";

    /// <summary>What the warning row says while it is empty and nobody is writing in it.</summary>
    private const string NoWarningWritten = "none · ctrl-w to add";

    /// <summary>What the warning row says while it is empty and is being written in.</summary>
    private const string WarningBeingWritten = "say what it's about";

    /// <summary>The warning header's label: a word, like the other headers', in place of the feed's mark (#375).</summary>
    private const string WarningLabel = "Warn";

    /// <summary>The label of the header the pending attachments sit under (#375).</summary>
    private const string MediaLabel = "Media";

    /// <summary>What the Media header says it holds while nothing is attached.</summary>
    private const string NothingAttached = "none";

    /// <summary>The key that adds to the Media header, which it names after what it holds while there is room.</summary>
    private const string AttachKey = "ctrl-o to add";

    /// <summary>The sensitive toggle's word, after its box (#379).</summary>
    private const string SensitiveWord = "sensitive";

    /// <summary>What the sensitive toggle adds while a warning holds it on (#379).</summary>
    private const string SensitiveByWarning = " (warning)";

    /// <summary>What an edit's Media header says of the attachments the post carries, which it cannot change.</summary>
    private const string KeptAsTheyAre = "kept as they are";

    /// <summary>The quiet mark on a pending attachment with no description (#375, #377).</summary>
    private const string NoDescription = "no alt text";

    /// <summary>What a pending attachment's row says while the instance processes it.</summary>
    private const string BeingProcessed = "processing";

    /// <summary>The grip a pending attachment's row is dragged by, muted, ahead of everything else on it.</summary>
    private const string Grip = "⠶";

    /// <summary>The bar against a picked row's grip, as a picked post's is against its edge (#378).</summary>
    private const string SelectionBar = "▌";

    /// <summary>What takes a pending attachment off the post, in plain ASCII (#375, #378).</summary>
    private const string RemoveMark = "x";

    /// <summary>What a refusal a retry could mend offers, the key named in it (#378).</summary>
    private const string RetryOffer = "retry (r)";

    /// <summary>The offer cut to its key, where the status column has not room for the reason and the word both.</summary>
    private const string ShortRetryOffer = "(r)";

    /// <summary>The column a pending attachment's grip sits at, a column in from its selection bar (#375).</summary>
    private const int GripAt = Pad + 3;

    /// <summary>How wide a pending attachment's name is cut to on a terminal with room for it.</summary>
    private const int MostName = 32;

    /// <summary>How narrow a name is cut to before anything else gives way.</summary>
    private const int LeastName = 12;

    /// <summary>The kind column's width, the kind and the gap after it, which goes first on a narrow terminal.</summary>
    private const int KindColumn = 9;

    /// <summary>The size column's width, right-aligned.</summary>
    private const int SizeColumn = 8;

    /// <summary>The fewest columns the status column is left with before the kind and then the name give way.</summary>
    private const int LeastStatus = 16;

    /// <summary>The most the status column is drawn across, so <c>x</c> stays near the row it takes off.</summary>
    private const int MostStatus = 40;

    /// <summary>How many cells an upload's gauge takes.</summary>
    private const int GaugeCells = 8;

    /// <summary>The columns left blank either side of everything on the screen (#317).</summary>
    private const int Pad = 2;

    /// <summary>
    ///     How wide the headers' right-aligned label column is: wide enough for <c>Media</c>, the widest of the words
    ///     every header is labelled with (#375).
    /// </summary>
    private const int LabelWidth = 5;

    /// <summary>The columns between a header's label and its value.</summary>
    private const int LabelGap = 2;

    /// <summary>What an empty editor says, dimly, where the first letter will go (#316).</summary>
    public const string EmptyPostHint = "What's on your mind?";

    /// <summary>
    ///     The fewest rows the editor is ever left with, however much of what is being answered wants to sit above it
    ///     (<see cref="EditorAt" />). Three: a line being written, and one either side of it to see.
    /// </summary>
    private const int LeastEditorRows = 3;

    private readonly bool _aboutIsMine;

    private readonly ComposeFrom? _from;

    /// <summary>What To opened on, which a draft sends as not chosen for as long as To still shows it (#338).</summary>
    private readonly PostVisibility? _startingVisibility;

    /// <summary>What the warning field opened on, which <see cref="Touched" /> measures it against.</summary>
    private readonly string _startingWarning;

    /// <summary>What Lang opened holding, which <see cref="Touched" /> measures it against.</summary>
    private readonly string _startingLanguage;

    /// <summary>
    ///     What To's row offers, left to right: "account default" first where To opened on it — so that an author who
    ///     stepped off it can step back and send nothing again — then every visibility, widest first.
    /// </summary>
    private readonly PostVisibility?[] _choices;

    /// <summary>
    ///     The fields the screen was last drawn with, which the walk keeps to: a short terminal gives Lang's row up
    ///     (<see cref="Laid" />), and a field that is not on screen is not one to move the typing into.
    /// </summary>
    private ComposeField[] _drawn = Fields;

    /// <summary>What is attached, in the order the post will carry it (#375).</summary>
    private readonly List<ComposeAttachment> _attachments = [];

    /// <summary>
    ///     Whether the screen was last drawn with a row for each attachment rather than folded into the Media header's
    ///     line, which settles whether the walk stops on them (#378).
    /// </summary>
    private bool _rowsDrawn;

    /// <summary>The attachment last taken off and the place it was taken from, for <c>ctrl-z</c> (#378); one deep.</summary>
    private (ComposeAttachment Attachment, int At)? _removed;

    /// <summary>
    ///     Whether the author has put what is attached behind a click (#379) — their own setting, kept while a warning
    ///     holds the toggle on, so that clearing the warning gives it back as they left it.
    /// </summary>
    private bool _sensitive;

    /// <param name="purpose">What this screen was opened to do.</param>
    /// <param name="about">The post being replied to or edited.</param>
    /// <param name="addressing">
    ///     Who this is being written to, as the mentions that reach them, or <see langword="null" /> for a post that
    ///     addresses nobody — with a space after it, because their own words go after the recipient rather than into
    ///     their name. Two things ask for one: a reply, which a direct post is delivered by (ADR-0013) and any other is
    ///     notified by (#130) — either way the account being answered is reached by being named, so the mention is put
    ///     where the reader can see and edit it rather than added silently on the way out; and a fresh post opened on a
    ///     picked mention, which is a reader saying who they mean to write to before they have written anything (#85).
    /// </param>
    /// <param name="aboutIsMine">
    ///     Whether <paramref name="about" /> is the profile's own post — settled by <c>Shell.IsMine</c> where this
    ///     screen is pushed, so the reply label below costs no lookup of its own.
    /// </param>
    /// <param name="from">
    ///     Who the post goes out as, for the From header — or <see langword="null" /> for a screen built with no
    ///     profile behind it, whose header is the label alone.
    /// </param>
    /// <param name="starting">
    ///     What To and Lang start on, which is what would go out if nobody touched them — worked out by the shell, where
    ///     the preferences, the account's own defaults and the post being answered are all in reach. An unknown
    ///     visibility reads "account default" on To and sends nothing (#338); no language leaves Lang empty, which sends
    ///     none (#340) (ADR-0024).
    /// </param>
    public ComposeScreen(
        ComposeFor purpose,
        Post? about = null,
        string? addressing = null,
        bool aboutIsMine = false,
        ComposeFrom? from = null,
        PostDefaults? starting = null)
    {
        Purpose = purpose;
        About = about;
        _aboutIsMine = aboutIsMine;
        _from = from;

        // An edit opens on the post's own visibility and language whatever it was handed, the one being what Mastodon
        // cannot change and the other what the author already said. Here, and nowhere else.
        var opening = purpose == ComposeFor.Edit && about is not null
            ? new PostDefaults(about.Visibility, about.Language)
            : starting ?? PostDefaults.Unknown;

        Visibility = opening.Visibility;
        _startingVisibility = Visibility;
        _choices =
        [
            .. Visibility is null ? [null] : Array.Empty<PostVisibility?>(),
            .. Enum.GetValues<PostVisibility>().Select(visibility => (PostVisibility?)visibility),
        ];

        Lang = ComposeLang.Opening(opening.Language);
        _startingLanguage = Lang.Held;

        Opening = purpose switch
        {
            ComposeFor.Edit => about?.Content ?? string.Empty,
            _ => string.IsNullOrEmpty(addressing) ? string.Empty : $"{addressing} ",
        };

        Text = Opening;

        // Pre-filled from whatever post this one is about: what a reply answers, since a reply to a warned post is
        // usually about the warned thing and an author who has to remember to re-type the warning is one who sometimes
        // will not (#123); and what an edit is changing, which is the same warning coming back to the author who wrote
        // it (#140). A post answering nothing has no post to have been filled from and opens on an empty field rather
        // than no field at all (#139). Only the words the author wrote carry across: the instance's sensitive flag is
        // a mark over the attachments and says nothing a field could hold.
        Warning = about?.ContentWarning ?? string.Empty;
        _startingWarning = Warning;
    }

    /// <summary>What this screen was opened to do.</summary>
    public ComposeFor Purpose { get; }

    /// <summary>The post being replied to or edited, or <see langword="null" /> for one answering nothing.</summary>
    public Post? About { get; }

    /// <summary>
    ///     What was in the editor before anybody typed: the post itself for an edit, the mention for a reply this
    ///     client had to address, and nothing for anything else.
    /// </summary>
    public string Opening { get; }

    /// <summary>
    ///     How long the instance lets the post be, which the count is out of: Mastodon's own until the instance has
    ///     said otherwise, which the shell asks it once a session and hands on as soon as it hears (#319).
    /// </summary>
    public PostLimits Limits { get; set; } = PostLimits.Default;

    /// <summary>What has been written so far, kept in step with the editor on every edit.</summary>
    public string Text { get; set; }

    /// <summary>The editor changed: what the post says now, learned on every edit.</summary>
    /// <returns>An edit, or nothing where the editor says back what this already holds.</returns>
    public ComposeChange Rewrite(string text)
    {
        if (Text == text)
        {
            return ComposeChange.None;
        }

        Text = text;

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     What the field holds, letter for letter — pre-filled on a reply from the post being answered and on an edit
    ///     from the post being changed, and from there the author's to keep, edit or clear. It is their post.
    /// </summary>
    /// <remarks>
    ///     The field's text, learned through <see cref="RewriteWarning" /> as the field changes, and nothing else's to
    ///     set: what it <em>means</em> on the way out is <see cref="Outgoing" />'s, which is the same field read two ways
    ///     and the reason nobody outside gets to choose between them (#146).
    /// </remarks>
    public string Warning { get; private set; }

    /// <summary>
    ///     Who the post goes to, as To shows it (ADR-0024, #338): what goes out, or <see langword="null" /> while it is
    ///     the account's own default on the instance, which this screen cannot see and so sends nothing for.
    /// </summary>
    public PostVisibility? Visibility { get; private set; }

    /// <summary>What Lang holds, and what that stands for (ADR-0024, #340).</summary>
    public ComposeLang Lang { get; private set; }

    /// <summary>
    ///     Whether the list of languages under Lang is open, which the window says as it opens and closes it — so that
    ///     the status row can offer the list's keys while it is (#340).
    /// </summary>
    public bool OfferingLanguages { get; private set; }

    /// <summary>
    ///     The fields the typing walks, in the order the arrows walk them (ADR-0024, #337): the headers that take typing,
    ///     top to bottom as drawn, then the post under them. A header that comes to take typing is put here and joins
    ///     the walk, with no key rule of its own.
    /// </summary>
    private static readonly ComposeField[] Fields =
        [ComposeField.To, ComposeField.Lang, ComposeField.Warning, ComposeField.Media, ComposeField.Post];

    /// <summary>
    ///     Which field what is typed is going into. Every field is on screen at once and the typing is in one of them,
    ///     since a terminal takes the keys of whichever field has them: the arrows walk it (<see cref="Walk" />),
    ///     <c>ctrl-w</c> jumps it into the warning and back, and a click into a field moves it there too, which keeps
    ///     this in step (#320).
    /// </summary>
    public ComposeField Typing { get; private set; } = ComposeField.Post;

    /// <summary>Whether what is typed is going into the warning rather than into the post.</summary>
    public bool WritingTheWarning => Typing == ComposeField.Warning;

    /// <summary>Whether the typing is in one of the headers rather than in the post.</summary>
    public bool OnAHeader => Typing != ComposeField.Post;

    /// <summary>
    ///     What the warning field says, dimly, while it is empty: how to reach it while the typing is in the post, and
    ///     what goes there while it is in the field.
    /// </summary>
    public string WarningHint => WritingTheWarning ? WarningBeingWritten : NoWarningWritten;

    /// <summary>
    ///     What Lang says, dimly, while it is empty: that the instance decides while the typing is elsewhere, and what
    ///     goes there while it is in Lang (#340).
    /// </summary>
    public string LanguageHint => Typing == ComposeField.Lang ? LanguageBeingTyped : NoLanguage;

    /// <summary>
    ///     Whether anything here differs from what the screen opened with — the post, its warning, To, Lang or what is
    ///     attached — which is what makes throwing it away worth a question (#373, #375). What it holds rather than
    ///     whether a key was pressed: a reply still holding only the mention and the warning it opened on is untouched,
    ///     and so is a letter typed and rubbed out again.
    /// </summary>
    /// <remarks>
    ///     The post is compared line ending for line ending alike, so that an editor writing its lines back its own way
    ///     does not count as somebody having written something. A screen opens with nothing attached, so anything
    ///     attached is a change.
    /// </remarks>
    public bool Touched =>
        !string.Equals(Text.ReplaceLineEndings(), Opening.ReplaceLineEndings(), StringComparison.Ordinal)
        || !string.Equals(Warning, _startingWarning, StringComparison.Ordinal)
        || Visibility != _startingVisibility
        || !string.Equals(Lang.Held, _startingLanguage, StringComparison.Ordinal)
        || _attachments.Count > 0;

    /// <summary>
    ///     Whether this screen takes attachments: a fresh post and a reply do, under their Media header (#375); an edit
    ///     carries the post's own through unchanged (ADR-0008), and its header only lists them (#381).
    /// </summary>
    public bool TakesAttachments => Purpose != ComposeFor.Edit;

    /// <summary>What is attached, in the order the post will carry it, each with where it has got to (#375).</summary>
    public IReadOnlyList<ComposeAttachment> Attachments => _attachments;

    /// <summary>
    ///     What the post being edited already carries, which an edit lists under its Media header read-only so that the
    ///     author can see it stays (#381) — and which the edit leaves as it was, never naming it (ADR-0008). Nothing on
    ///     a fresh post or a reply.
    /// </summary>
    private IReadOnlyList<PostMedia> Kept => Purpose == ComposeFor.Edit && About is { } edited ? edited.Media : [];

    /// <summary>How many rows the Media header has under it: one for each attachment, pending or kept.</summary>
    private int MediaRows => _attachments.Count + Kept.Count;

    /// <summary>How many more attachments the post has room for, by the instance's limit.</summary>
    public int AttachmentRoom => TakesAttachments ? Math.Max(0, Limits.Attachments - _attachments.Count) : 0;

    /// <summary>Whether anything attached is still going up or being processed, which a send waits on (ADR-0026).</summary>
    public bool Unfinished => _attachments.Any(attachment => attachment.Unfinished);

    /// <summary>How many attachments the instance refused, which a send will not go out with (ADR-0026).</summary>
    public int Refused => _attachments.Count(attachment => attachment.State is AttachmentState.Refused);

    /// <summary>
    ///     Whether what is attached goes behind a click (#379): as the author set it, or on whatever they set while the
    ///     warning has text, since a warning puts the whole post behind one (ADR-0008).
    /// </summary>
    public bool Sensitive => _sensitive || SensitiveByAWarning;

    /// <summary>Whether a warning is what holds the sensitive toggle on, which locks it there (#379).</summary>
    public bool SensitiveByAWarning => ContentWarnings.Written(Warning) is not null;

    /// <summary>
    ///     <c>s</c> on the Media header, or a click on the toggle (#379): puts what is attached behind a click, or takes
    ///     it out from behind one. Nothing while nothing is attached, there being no toggle, nor while a warning holds
    ///     it on.
    /// </summary>
    /// <returns>An edit, the toggle being part of what is sent, or nothing where it could not change.</returns>
    public ComposeChange ToggleSensitive()
    {
        if (_attachments.Count == 0 || SensitiveByAWarning)
        {
            return ComposeChange.None;
        }

        _sensitive = !_sensitive;

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     Attaches <paramref name="files" />, in order, after whatever is attached already — as many as the post has
    ///     room for (#375). Each starts out going up, which is the shell's to start and to feed back
    ///     (<see cref="Progressed" />).
    /// </summary>
    /// <returns>The ones attached, which are the ones the shell sends up.</returns>
    public IReadOnlyList<ComposeAttachment> Attach(IEnumerable<ComposeAttachment> files)
    {
        var attached = files.Take(AttachmentRoom).ToList();

        _attachments.AddRange(attached);

        return attached;
    }

    /// <summary>
    ///     Picks <paramref name="attachment" />'s row, as a click or a press on it does (#378): the typing goes to it, as
    ///     the walk would have taken it.
    /// </summary>
    /// <returns>A move, or nothing where it was already picked or is not attached.</returns>
    public ComposeChange Pick(ComposeAttachment attachment)
    {
        if (!_attachments.Contains(attachment)
            || (Typing == ComposeField.Attachment && ReferenceEquals(PickedAttachment, attachment)))
        {
            return ComposeChange.None;
        }

        Typing = ComposeField.Attachment;
        PickedAttachment = attachment;

        return ComposeChange.Moved;
    }

    /// <summary>
    ///     <c>del</c> or <c>backspace</c> on a row: takes the picked attachment off the post (#378). The walk stays where
    ///     it was — on the row that took its place, else the one above — and with nothing left attached goes back to
    ///     the post, the header having nothing on it to walk to (#379); what went is remembered, one deep, for
    ///     <see cref="BringBack" />.
    /// </summary>
    /// <returns>An edit, or nothing off the rows.</returns>
    public ComposeChange Remove() =>
        Typing == ComposeField.Attachment && PickedAttachment is { } picked ? Remove(picked) : ComposeChange.None;

    /// <summary>A click on <paramref name="attachment" />'s <c>x</c>: takes it off the post, as <see cref="Remove()" /> does.</summary>
    /// <returns>An edit, or nothing where it is not attached.</returns>
    public ComposeChange Remove(ComposeAttachment attachment)
    {
        var at = _attachments.IndexOf(attachment);

        if (at < 0)
        {
            return ComposeChange.None;
        }

        _attachments.RemoveAt(at);
        _removed = (attachment, at);

        if (ReferenceEquals(PickedAttachment, attachment))
        {
            PickedAttachment = _attachments.Count > 0 ? _attachments[Math.Min(at, _attachments.Count - 1)] : null;
            Typing = PickedAttachment is null ? ComposeField.Post : ComposeField.Attachment;
        }

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     <c>ctrl-z</c> on the Media header or its rows: the attachment last taken off, back where it was with its
    ///     description and wherever it had got to, and picked (#378). One deep, and only here — the editor and Warn keep
    ///     <c>ctrl-z</c> for their own text — and nothing while the post has no room for it.
    /// </summary>
    /// <returns>An edit, or nothing where there is nothing to bring back or nowhere to put it.</returns>
    public ComposeChange BringBack()
    {
        if (Typing is not (ComposeField.Media or ComposeField.Attachment)
            || _removed is not var (attachment, at)
            || AttachmentRoom == 0)
        {
            return ComposeChange.None;
        }

        _attachments.Insert(Math.Min(at, _attachments.Count), attachment);
        _removed = null;
        Typing = ComposeField.Attachment;
        PickedAttachment = attachment;

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     <c>shift-↑</c> or <c>shift-↓</c> on a row (<paramref name="by" /> −1 or 1): the picked attachment one place
    ///     earlier or later on the post, still picked (#378).
    /// </summary>
    /// <returns>An edit, or nothing off the rows or off either end.</returns>
    public ComposeChange Reorder(int by) =>
        Typing == ComposeField.Attachment && PickedAttachment is { } picked
            ? ReorderTo(picked, _attachments.IndexOf(picked) + by)
            : ComposeChange.None;

    /// <summary>
    ///     <paramref name="attachment" /> dragged to the <paramref name="place" />th row, the rows between making way
    ///     (#378) — a place past either end is that end.
    /// </summary>
    /// <returns>An edit, or nothing where it is already there or is not attached.</returns>
    public ComposeChange ReorderTo(ComposeAttachment attachment, int place)
    {
        var from = _attachments.IndexOf(attachment);
        var to = Math.Clamp(place, 0, _attachments.Count - 1);

        if (from < 0 || from == to)
        {
            return ComposeChange.None;
        }

        _attachments.RemoveAt(from);
        _attachments.Insert(to, attachment);

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     <c>r</c> on a row: the picked attachment, where a retry could mend its refusal, to be sent up again (#378).
    /// </summary>
    /// <returns>The attachment, going up again from nothing, for the shell to send; or nothing.</returns>
    public ComposeAttachment? Retry() =>
        Typing == ComposeField.Attachment && PickedAttachment is { } picked ? Retry(picked) : null;

    /// <summary>
    ///     A click on <paramref name="attachment" />'s <c>retry (r)</c>: it starts going up again from nothing, where a
    ///     retry could mend its refusal and nowhere else (ADR-0006: the author's to ask for, never done by itself).
    /// </summary>
    /// <returns>The attachment, for the shell to send up; or nothing.</returns>
    public ComposeAttachment? Retry(ComposeAttachment attachment)
    {
        if (!_attachments.Contains(attachment) || attachment.State is not AttachmentState.Refused { Retryable: true })
        {
            return null;
        }

        attachment.State = new AttachmentState.Sending(0);

        return attachment;
    }

    /// <summary>
    ///     Where <paramref name="attachment" /> has got to, as the shell heard it: its row says so from now on. Nothing
    ///     where it is no longer attached — kept, but unsaid, on the one <c>ctrl-z</c> could still bring back.
    /// </summary>
    /// <returns>Whether that changed anything on the screen.</returns>
    public bool Progressed(ComposeAttachment attachment, AttachmentState state)
    {
        // One taken off but still to be brought back hears on too, so that it comes back as far as it has got (#378).
        var attached = _attachments.Contains(attachment);

        if ((!attached && !ReferenceEquals(_removed?.Attachment, attachment)) || attachment.State == state)
        {
            return false;
        }

        attachment.State = state;

        return attached;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Whoever wrote the post this one is about and everyone it names, so that the person being answered is the
    ///     most recently seen — and one keystroke away — once the screen is pushed.
    /// </remarks>
    public override IEnumerable<Mentionable> Seen => About is { } about ? Mentionable.In(about) : [];

    /// <summary>
    ///     What goes out when <c>ctrl-s</c> is pressed, whole: the post this screen publishes, or the change it saves
    ///     to one already published. Said here because this is where the fields are — what is left to the shell is the
    ///     port, the stack and the notice, which are the three things a screen has none of (#146).
    /// </summary>
    /// <remarks>
    ///     Asked for at the moment of sending rather than kept in step with the fields, since the key that asks is the
    ///     key that ends the screen.
    /// </remarks>
    public Outgoing Outgoing => Purpose switch
    {
        // About is the post e was pressed on, which the shell refuses to open an edit without.
        ComposeFor.Edit => new Outgoing.Saving(About!.Id, Changed()),
        _ => new Outgoing.Publishing(Drafted()),
    };

    /// <summary>
    ///     Whether there is anything here worth sending — which for a reply this client addressed means anything
    ///     beyond the mention it opened with, since a message that is nothing but its recipient's name says nothing.
    ///     Anything attached is worth sending with no words at all, as Mastodon allows: a picture can be the whole post
    ///     (#375).
    /// </summary>
    public bool IsEmpty =>
        _attachments.Count == 0
        && (string.IsNullOrWhiteSpace(Text)
            || (Purpose == ComposeFor.Reply && string.Equals(Text.Trim(), Opening.Trim(), StringComparison.Ordinal)));

    /// <inheritdoc />
    public override string Crumb => Purpose switch
    {
        ComposeFor.Post => "Compose",
        ComposeFor.Reply => $"Reply to @{About?.Account}",
        ComposeFor.Edit => "Edit",
        _ => "Compose",
    };

    /// <inheritdoc />
    /// <remarks>
    ///     No keymap key, in either field: <c>?</c> is a letter in the post and in the warning alike, each field taking
    ///     its own keys before the shell sees them (#320) — so naming it would be offering a press that types a
    ///     question mark.
    ///     <para>
    ///         The walk between the fields comes first (<c>docs/tui-shell.md</c>, #337), offered only the ways it goes:
    ///         it stops at either end, so To, the top header, offers <c>↓</c> and the post <c>↑</c> — which in the post
    ///         walks only off its first line, the caret having it below that. On To the choosing comes ahead of that,
    ///         being what the row is for (#338).
    ///     </para>
    /// </remarks>
    protected override IReadOnlyList<KeyHint> OwnKeys =>
    [
        .. Typing == ComposeField.Lang && OfferingLanguages
            ? [new KeyHint("↑↓", "pick"), new KeyHint("tab", "choose"), new KeyHint("esc", "close")]
            : Array.Empty<KeyHint>(),
        .. Typing == ComposeField.To ? [new KeyHint("←→", "choose")] : Array.Empty<KeyHint>(),
        .. RowKeys,
        .. Typing is (ComposeField.Media or ComposeField.Attachment) && _attachments.Count > 0
            ? [new KeyHint("s", "sensitive")]
            : Array.Empty<KeyHint>(),
        .. Walks switch
        {
            (true, true) => [new KeyHint("↑↓", "field")],
            (true, false) => [new KeyHint("↑", "field")],
            (false, true) => [new KeyHint("↓", "field")],
            _ => Array.Empty<KeyHint>(),
        },
        new("ctrl-s", Purpose == ComposeFor.Edit ? "save" : "send"),
        new("ctrl-w", WritingTheWarning ? "back to the post" : "content warning"),
        new("esc", "throw it away"),
    ];

    /// <summary>
    ///     What a row under the Media header offers while the walk is on it (#378): a retry where one could mend its
    ///     refusal, moving it and taking it off — and, on the header or a row, bringing back what was last taken off
    ///     while there is something to and room for it.
    /// </summary>
    private KeyHint[] RowKeys =>
    [
        .. Typing == ComposeField.Attachment && PickedAttachment is { State: AttachmentState.Refused { Retryable: true } }
            ? [new KeyHint("r", "retry")]
            : Array.Empty<KeyHint>(),
        .. Typing == ComposeField.Attachment
            ? [new KeyHint("shift-↑↓", "move"), new KeyHint("del", "remove")]
            : Array.Empty<KeyHint>(),
        .. Typing is (ComposeField.Media or ComposeField.Attachment) && _removed is not null && AttachmentRoom > 0
            ? [new KeyHint("ctrl-z", "bring back")]
            : Array.Empty<KeyHint>(),
    ];

    /// <inheritdoc />
    /// <remarks>
    ///     The whole of the content region, laid out to its height (<see cref="Drawing.Height" />) rather than
    ///     scrolled: the headers, the hairline, blank rows where the editor is laid over them, and the foot.
    /// </remarks>
    public override IReadOnlyList<Line> Lines(Drawing drawing)
    {
        var laid = Laid(drawing.Width, drawing.Height);

        _drawn =
        [
            .. Fields.Where(field => field switch
            {
                ComposeField.Lang => laid.Lang != Rectangle.Empty,
                ComposeField.Media => laid.Media != Rectangle.Empty,
                _ => true,
            }),
        ];
        _rowsDrawn = laid.Media.Height > 1;

        return laid.Rows;
    }

    /// <summary>
    ///     The warning field changed: what it holds now, learned the way the post's text is learned from the editor
    ///     (#320). The field is the author's to type into; what goes out is still this screen's to say.
    /// </summary>
    /// <returns>An edit, or nothing where the field says back what this holds, as it does when filled on opening.</returns>
    public ComposeChange RewriteWarning(string written)
    {
        if (Warning == written)
        {
            return ComposeChange.None;
        }

        Warning = written;

        return ComposeChange.Edited;
    }

    /// <summary>
    ///     Lang's field changed: what it holds now, learned as the warning's is (#340). Whether that is a language is
    ///     asked at send.
    /// </summary>
    /// <returns>An edit, or nothing where the field says back what Lang already holds.</returns>
    public ComposeChange RewriteLanguage(string written)
    {
        if (Lang.Held == written)
        {
            return ComposeChange.None;
        }

        Lang = Lang with { Held = written };

        return ComposeChange.Edited;
    }

    /// <summary>A language picked off Lang's list: the field says it as <see cref="ComposeLang.Spoken" /> writes it.</summary>
    /// <returns>An edit, or nothing where Lang already held it so.</returns>
    public ComposeChange PickLanguage(PostLanguage language) => RewriteLanguage(ComposeLang.Spoken(language));

    /// <summary>The list of languages under Lang opened or closed, which the status row follows.</summary>
    /// <returns>A move, or nothing where the list was already that way.</returns>
    public ComposeChange OfferLanguages(bool open)
    {
        if (OfferingLanguages == open)
        {
            return ComposeChange.None;
        }

        OfferingLanguages = open;

        return ComposeChange.Moved;
    }

    /// <summary><c>ctrl-w</c>: hands the typing to the warning, or hands it back to the post.</summary>
    /// <returns>A move, always.</returns>
    public ComposeChange WriteTheWarning()
    {
        Typing = WritingTheWarning ? ComposeField.Post : ComposeField.Warning;
        PickedAttachment = null;

        return ComposeChange.Moved;
    }

    /// <summary>
    ///     <c>↑</c> or <c>↓</c> (<paramref name="by" /> −1 or 1): moves the typing to the field above or below in
    ///     <see cref="Fields" /> — out of the post into the last header, between the headers, and off the last header
    ///     back into the post (ADR-0024, #337).
    /// </summary>
    /// <returns>
    ///     A move, or nothing off either end: there is nothing above the top header, and below the post is the post's
    ///     own business.
    /// </returns>
    /// <remarks>
    ///     A field that takes nothing is not walked into: To on an edit, which Mastodon cannot change. Nor is one the
    ///     screen was last drawn without: Lang, where a short terminal gave its row up.
    /// </remarks>
    public ComposeChange Walk(int by)
    {
        if (WalkedTo(by) is not { } stop)
        {
            return ComposeChange.None;
        }

        (Typing, PickedAttachment) = stop;

        return ComposeChange.Moved;
    }

    /// <summary>
    ///     Which ways the arrows can walk from where the typing is, up and down, for the status row to offer: the walk
    ///     stops at either end rather than coming round, so To offers only <c>↓</c> and the post only <c>↑</c>.
    /// </summary>
    private (bool Up, bool Down) Walks => (WalkedTo(-1) is not null, WalkedTo(1) is not null);

    /// <summary>
    ///     Where <see cref="Walk" /> would move the typing to, or null off either end: a field, or one of the rows under
    ///     the Media header (#378).
    /// </summary>
    private (ComposeField Field, ComposeAttachment? PickedAttachment)? WalkedTo(int by)
    {
        var stops = Stops().ToList();
        var at = stops.IndexOf((Typing, PickedAttachment));

        // A row no longer drawn — folded away on a terminal grown short — is walked from as the header it folded into.
        if (at < 0 && Typing == ComposeField.Attachment)
        {
            at = stops.IndexOf((ComposeField.Media, null));
        }

        var to = at + by;

        return to < 0 || to >= stops.Count ? null : stops[to];
    }

    /// <summary>
    ///     Everywhere the walk stops, top to bottom: each field drawn that takes the typing and, under the Media header,
    ///     each of its rows while they are drawn as rows rather than folded into the header's line (#378).
    /// </summary>
    private IEnumerable<(ComposeField Field, ComposeAttachment? PickedAttachment)> Stops()
    {
        foreach (var field in _drawn.Where(Takes))
        {
            yield return (field, null);

            if (field == ComposeField.Media && _rowsDrawn)
            {
                foreach (var attachment in _attachments)
                {
                    yield return (ComposeField.Attachment, attachment);
                }
            }
        }
    }

    /// <summary>
    ///     Whether <paramref name="field" /> can have the typing at all, which To cannot where it allows nothing, nor
    ///     Media until something is attached (#379): the toggle is all there is to do on it so far, and it shows only
    ///     then — so the walk from the post still goes straight to Warn on a screen with nothing attached, and on an
    ///     edit, whose Media header is read-only (#381).
    /// </summary>
    public bool Takes(ComposeField field) => field switch
    {
        ComposeField.To => _choices.Any(Offers),
        ComposeField.Media or ComposeField.Attachment => TakesAttachments && _attachments.Count > 0,
        _ => true,
    };

    /// <summary>
    ///     The pending attachment whose row the typing is on, while it is on one (<see cref="ComposeField.Attachment" />),
    ///     and nothing otherwise (#378).
    /// </summary>
    public ComposeAttachment? PickedAttachment { get; private set; }

    /// <summary>
    ///     The typing went into <paramref name="field" /> by a click rather than by this screen's keys, and the screen
    ///     follows it there (#320, #338).
    /// </summary>
    /// <returns>A move, or nothing where it was already there or the field takes nothing.</returns>
    public ComposeChange TypeInto(ComposeField field)
    {
        // The rows are under the header, and a row picked has the Media header's focus as much as the header has.
        if (Typing == field || !Takes(field) || field == ComposeField.Attachment
            || (field == ComposeField.Media && Typing == ComposeField.Attachment))
        {
            return ComposeChange.None;
        }

        Typing = field;
        PickedAttachment = null;

        return ComposeChange.Moved;
    }

    /// <summary>
    ///     <c>←</c> or <c>→</c> (<paramref name="by" /> −1 or 1) on To: the next choice that way which To offers,
    ///     skipping any it does not (ADR-0024, #338).
    /// </summary>
    /// <returns>
    ///     A move — choosing is moving the choice, as walking is moving the typing (#364) — or nothing off To or off
    ///     either end of what it offers.
    /// </returns>
    public ComposeChange Choose(int by)
    {
        if (Typing != ComposeField.To || !Stepped(by, out var next))
        {
            return ComposeChange.None;
        }

        Visibility = next;

        return ComposeChange.Moved;
    }

    /// <summary>
    ///     A click <paramref name="column" /> columns into To's value as it is drawn <paramref name="room" /> wide: on a
    ///     choice To offers it chooses it, on an arrow it steps, and anywhere on the row it gives To the typing — but a
    ///     value To does not allow ignores it, and so does all of To on an edit (ADR-0024, #338).
    /// </summary>
    /// <returns>A move where anything changed, as for <see cref="Choose" />, and nothing otherwise.</returns>
    public ComposeChange ClickTo(int column, int room)
    {
        if (!Takes(ComposeField.To))
        {
            return ComposeChange.None;
        }

        var was = (Visibility, Typing);

        switch (ToPartAt(ToRow(room), column))
        {
            case ToPart.Choice { Visibility: var choice } when !Offers(choice):
                return ComposeChange.None;
            case ToPart.Choice { Visibility: var choice }:
                Visibility = choice;

                break;
            case ToPart.Step { By: var by } when Stepped(by, out var next):
                Visibility = next;

                break;
        }

        Typing = ComposeField.To;

        return (Visibility, Typing) != was ? ComposeChange.Moved : ComposeChange.None;
    }

    /// <summary>What the run <paramref name="column" /> columns into <paramref name="row" /> stands for, if anything.</summary>
    private static ToPart? ToPartAt(IEnumerable<(Span Span, ToPart? Part)> row, int column)
    {
        foreach (var (span, part) in row)
        {
            if (column < span.Width)
            {
                return part;
            }

            column -= span.Width;
        }

        return null;
    }

    /// <summary>The next choice To offers <paramref name="by" /> along the row from the one chosen, if there is one.</summary>
    private bool Stepped(int by, out PostVisibility? next)
    {
        for (var at = Array.IndexOf(_choices, Visibility) + by; at >= 0 && at < _choices.Length; at += by)
        {
            if (Offers(_choices[at]))
            {
                next = _choices[at];

                return true;
            }
        }

        next = Visibility;

        return false;
    }

    /// <summary>
    ///     Whether To's row offers <paramref name="choice" />: a visibility where To <see cref="Allows" /> it, and
    ///     "account default" anywhere but an edit — it is only on the row where To opened on it.
    /// </summary>
    private bool Offers(PostVisibility? choice) => choice is { } visibility ? Allows(visibility) : Purpose != ComposeFor.Edit;

    /// <summary>
    ///     Where the editor goes inside the content panel's viewport of <paramref name="viewport" />: under the
    ///     headers and the hairline below them, inside the padding either side, and down to the hairline at the foot.
    ///     Said here rather than worked out by the window from the rows above, because this is what paints those rows
    ///     (#315) — and both come from the one layout, so they cannot come to disagree.
    /// </summary>
    public Rectangle EditorAt(Size viewport) => Laid(viewport.Width, viewport.Height).Editor;

    /// <summary>
    ///     Where the warning field goes inside the same viewport: the warning header's value column, on whichever row
    ///     the headers above it leave it (#320). From the one layout that paints the header, as for the editor.
    /// </summary>
    public Rectangle WarningAt(Size viewport) => Laid(viewport.Width, viewport.Height).Warning;

    /// <summary>Where To's value goes inside the same viewport, for the row of buttons laid over it (#338).</summary>
    public Rectangle ToAt(Size viewport) => Laid(viewport.Width, viewport.Height).To;

    /// <summary>
    ///     Where Lang's value goes inside the same viewport, for the field laid over it (#340) — nowhere where a short
    ///     terminal has given its row up.
    /// </summary>
    public Rectangle LangAt(Size viewport) => Laid(viewport.Width, viewport.Height).Lang;

    /// <summary>
    ///     Where the Media header and the rows under it go inside the same viewport, from the left edge across: its
    ///     line, for the view laid over it that takes its keys and its clicks (#379), and one under it for each
    ///     pending attachment's row drawn (#378) — nowhere on an edit, or where a short terminal gave it up.
    /// </summary>
    public Rectangle MediaAt(Size viewport) => Laid(viewport.Width, viewport.Height).Media;

    /// <summary>
    ///     The pending attachment whose row is at <paramref name="at" /> in the same viewport, and what a click there
    ///     means (#378) — nothing on the header's own line, or off the rows.
    /// </summary>
    public (ComposeAttachment Attachment, AttachmentPart Part)? AttachmentAt(Size viewport, Point at)
    {
        var media = MediaAt(viewport);
        var row = at.Y - media.Y - 1;

        if (row < 0 || row >= media.Height - 1 || row >= _attachments.Count)
        {
            return null;
        }

        var attached = _attachments[row];
        var column = at.X;

        foreach (var (span, part) in AttachmentRuns(attached, viewport.Width))
        {
            if (column < span.Width)
            {
                return (attached, part);
            }

            column -= span.Width;
        }

        return (attached, AttachmentPart.Row);
    }

    /// <summary>
    ///     Whether a click <paramref name="column" /> columns into the Media header's line, laid in
    ///     <paramref name="viewport" />, lands on the sensitive toggle at the end of it (#379) — read off the very row
    ///     that is drawn, folded or not.
    /// </summary>
    public bool OnTheSensitiveToggle(int column, Size viewport)
    {
        var laid = Laid(viewport.Width, viewport.Height);

        if (laid.Media == Rectangle.Empty || _attachments.Count == 0)
        {
            return false;
        }

        var toggle = SensitiveToggle().Text;
        var at = -laid.Media.X;

        foreach (var span in laid.Rows[laid.Media.Y].Spans)
        {
            if (span.Text == toggle)
            {
                return column >= at && column < at + span.Width;
            }

            at += span.Width;
        }

        return false;
    }

    /// <summary>
    ///     To's value as it is drawn <paramref name="room" /> columns wide — what the row laid over it paints, so that
    ///     row and the one a test reads off <see cref="Lines" /> are the same spans.
    /// </summary>
    public IReadOnlyList<Span> ToSpans(int room) => [.. ToRow(room).Select(run => run.Span)];

    /// <summary>
    ///     The screen laid out at <paramref name="width" /> by <paramref name="height" />: every row, top to bottom,
    ///     and where the editor sits among them (#317, variant A of the prototype).
    /// </summary>
    /// <remarks>
    ///     A blank; the headers — From, To, Lang, the reply header and its quote on a reply, the warning, and Media with
    ///     a row for each pending attachment, or on an edit for each the post carries (#375, #381); a hairline; a
    ///     blank; the editor; a hairline; the row the count sits on. Two columns of padding either side of all of it.
    ///     Lang gives way with From, being lower on the screen just before it (#340). The attachments' rows go before
    ///     anything else does, by folding into the Media header's line, and the header itself after From.
    ///     <para>
    ///         Never so far down that there is no editor left. ADR-0015 priced the editor's share of a 24-row
    ///         terminal at more than what sits above it, but a terminal can be any size, and an editor that starts
    ///         below the foot is one nobody can type in. Where there is not room for everything, rows give way in the
    ///         order <see cref="Row.Keep" /> ranks them: the tail of what is being answered first, To and the warning
    ///         never — the warning is a row the reader types into, and one they cannot see is worse than a quote that
    ///         stops early, and To says who the post reaches (#338).
    ///         Where nobody says how tall the room is, it is as tall as the rows and the editor's least want.
    ///     </para>
    /// </remarks>
    private (IReadOnlyList<Line> Rows, Rectangle Editor, Rectangle Warning, Rectangle To, Rectangle Lang, Rectangle Media)
        Laid(int width, int? height)
    {
        var inner = Math.Max(0, width - (Pad * 2));
        var valueWidth = Math.Max(0, inner - LabelWidth - LabelGap);
        var hairline = Line.Of(Gap(Pad), new Span(new string('─', inner), Role.PanelBorder));

        var to = new Row(Header("To", Role.Muted, [.. ToSpans(valueWidth)]), Keep.Always);
        var lang = new Row(Header("Lang", Role.Muted, LangValue(valueWidth)), Keep.From);

        var above = new List<Row>
        {
            new(Line.Blank, Keep.TopBlank),
            new(Header("From", Role.Muted, From(valueWidth)), Keep.From),
            to,
            lang,
        };

        if (Purpose == ComposeFor.Reply && About is { } answered)
        {
            var said = PostReplyName.Answered(answered.Account, _aboutIsMine);

            above.Add(new Row(
                Header(PostReplyName.Mark, Role.Muted, new Span(TextWrap.Clip(said, valueWidth), Role.Muted)),
                Keep.ReplyHeader));

            // Three rows of what was said, and blank ones are not among them (#141): a post's paragraphs arrive as
            // blank lines, so a quote that took its three rows in order spent one of them on a gap.
            const string gutter = "│ ";
            var quoted = TextWrap.Wrap(answered.Content, Math.Max(1, valueWidth - Glyphs.Columns(gutter)))
                                 .Where(row => row.Length > 0)
                                 .Take(3);

            above.AddRange(quoted.Select(row => new Row(
                Line.Of(
                    Gap(Pad + LabelWidth + LabelGap),
                    new Span(gutter, Role.PanelBorder),
                    new Span(row, Role.Muted)),
                Keep.Quote)));
        }

        var warning = new Row(WarningHeader(valueWidth), Keep.Always);

        above.Add(warning);

        // Every compose screen has the header, an edit's listing what the post carries, so that with the same headers
        // the editor starts on the same row on all three (ADR-0015, #381).
        var media = new Row(Line.Blank, Keep.Media);

        above.Add(media);
        above.Add(new Row(hairline, Keep.HeaderHairline));
        above.Add(new Row(Line.Blank, Keep.BlankUnderHairline));

        var foot = new List<Row> { new(hairline, Keep.FootHairline), new(Count(width), Keep.CountRow) };
        var room = height ?? (above.Count + MediaRows + LeastEditorRows + foot.Count);

        // A row for each attachment where that still leaves the editor its three, and otherwise one line for all of
        // them on the header's own — before any other row gives way, since what is attached can still be counted on
        // one line and a header given up says nothing at all (#375).
        var folded = above.Count + MediaRows + foot.Count + LeastEditorRows > room;
        var attachmentRows = folded ? 0 : _attachments.Count;
        var at = above.IndexOf(media);

        above[at] = media with { Line = Header(MediaLabel, Role.Muted, MediaValue(valueWidth, folded)) };

        if (!folded)
        {
            above.InsertRange(
                at + 1,
                [
                    .. _attachments.Select(attached => new Row(AttachmentRow(attached, width), Keep.Always)),
                    .. Kept.Select(kept => new Row(KeptRow(kept, valueWidth), Keep.Always)),
                ]);
        }

        // Whatever ranks lowest goes first and, among equals, whichever is lowest on the screen: the quote gives up
        // its tail rather than its start.
        while (above.Count + foot.Count + LeastEditorRows > room
               && above.Concat(foot).Where(row => row.Keep != Keep.Always).MinBy(row => row.Keep) is { } least)
        {
            var holding = foot.Contains(least) ? foot : above;

            holding.RemoveAt(holding.FindLastIndex(row => row.Keep == least.Keep));
        }

        var top = above.Count;
        var editor = Math.Max(0, room - top - foot.Count);

        IReadOnlyList<Line> rows =
        [
            .. above.Select(row => row.Line),
            .. Enumerable.Repeat(Line.Blank, editor),
            .. foot.Select(row => row.Line),
        ];

        return (
            rows,
            new Rectangle(Math.Min(Pad, width), top, inner, editor),
            Value(above.IndexOf(warning)),
            Value(above.IndexOf(to)),
            above.Contains(lang) ? Value(above.IndexOf(lang)) : Rectangle.Empty,
            above.FindIndex(row => row.Keep == Keep.Media) is var header and >= 0
                ? new Rectangle(0, header, width, 1 + attachmentRows)
                : Rectangle.Empty);

        Rectangle Value(int row) => new(Math.Min(Pad + LabelWidth + LabelGap, width), row, valueWidth, 1);
    }

    /// <summary>
    ///     A header: <paramref name="label" /> right-aligned in the label column, in <paramref name="role" />, then
    ///     <paramref name="value" />.
    /// </summary>
    private static Line Header(string label, Role role, params Span[] value) =>
        Line.Of([Gap(Pad + LabelWidth - Glyphs.Columns(label)), new Span(label, role), Gap(LabelGap), .. value]);

    /// <summary>
    ///     The From header's value in <paramref name="room" /> columns: the handle as a byline's, then the instance,
    ///     muted — the instance cut first, since the handle is the half that says whose post this is.
    /// </summary>
    private Span[] From(int room)
    {
        if (_from is not { } from)
        {
            return [];
        }

        var handle = TextWrap.Clip(from.Handle, room);
        var instance = TextWrap.Clip($" · {from.Instance}", room - Glyphs.Columns(handle));

        return [new Span(handle, Role.BylineHandle), new Span(instance, Role.Muted)];
    }

    /// <summary>
    ///     To's value in <paramref name="room" /> columns (ADR-0024, #338): every choice it offers as a radio button, the
    ///     chosen one filled — or, where that row does not fit, the one chosen with an arrow either side. Values To does
    ///     not allow are muted, and so is all of it on an edit.
    /// </summary>
    /// <remarks>
    ///     Each run comes with what a click on it means — a choice, or a step along the row — so that a click is
    ///     answered from the very runs that were drawn.
    /// </remarks>
    private List<(Span Span, ToPart? Part)> ToRow(int room)
    {
        var row = new List<(Span Span, ToPart? Part)>();

        foreach (var choice in _choices)
        {
            if (row.Count > 0)
            {
                row.Add((Gap(2), null));
            }

            row.Add((Choice(choice), new ToPart.Choice(choice)));
        }

        if (row.Sum(run => run.Span.Width) <= room)
        {
            return row;
        }

        var shown = Choice(Visibility);

        List<(Span Span, ToPart? Part)> cycle =
        [
            (new Span(StepBack, Role.Muted), new ToPart.Step(-1)),
            (Gap(1), null),
            (shown, new ToPart.Choice(Visibility)),
            (Gap(1), null),
            (new Span(StepOn, Role.Muted), new ToPart.Step(1)),
        ];

        return cycle.Sum(run => run.Span.Width) <= room
            ? cycle
            : [(shown with { Text = TextWrap.Clip(shown.Text, room) }, new ToPart.Choice(Visibility))];
    }

    /// <summary>One of To's choices as a radio button and its name: <c>● followers</c>, <c>○ account default</c>.</summary>
    private Span Choice(PostVisibility? choice)
    {
        var bubble = choice == Visibility ? "●" : "○";
        var name = choice is { } visibility ? PostVisibilityName.Of(visibility) : AccountDefault;

        return new Span($"{bubble} {name}", ValueRole(choice));
    }

    /// <summary>
    ///     Lang's value in <paramref name="room" /> columns (#340), painted as the field laid over it shows it so the row
    ///     reads the same with no terminal behind it: a language's code and, muted, its own name; anything else being
    ///     typed as it stands; and, empty, a muted hint.
    /// </summary>
    private Span[] LangValue(int room)
    {
        if (Lang.Held.Length == 0)
        {
            return [new Span(TextWrap.Clip(LanguageHint, room), Role.Muted)];
        }

        if (Lang.HoldsALanguageWhole && Lang.Language is { } language)
        {
            var code = TextWrap.Clip(language.Code, room);

            return
            [
                new Span(code, Role.Body),
                new Span(
                    TextWrap.Clip($"{ComposeLang.CodeGap}{language.OwnName}", room - Glyphs.Columns(code)),
                    Role.Muted),
            ];
        }

        return [new Span(TextWrap.Clip(Lang.Held, room), Role.Body)];
    }

    /// <summary>
    ///     The role a choice on To is drawn in: muted where it cannot be chosen, the selection's where it is chosen and
    ///     To has the typing — which is how a reader sees where the typing went, a row of buttons having no caret — and
    ///     the body's otherwise.
    /// </summary>
    private Role ValueRole(PostVisibility? choice) =>
        !Offers(choice) ? Role.Muted
        : choice == Visibility && Typing == ComposeField.To ? Role.SelectedText
        : Role.Body;

    /// <summary>
    ///     Whether To may be set to <paramref name="visibility" />: any on a fresh post, none on an edit — Mastodon cannot
    ///     change a published post's visibility — and on a reply only as narrow as the post being answered or narrower,
    ///     so the screen never offers what ADR-0013 would refuse. The keys, the clicks and the dimming all read this.
    /// </summary>
    public bool Allows(PostVisibility visibility) => Purpose switch
    {
        ComposeFor.Edit => false,
        ComposeFor.Reply when About is { } answered => !PostAudience.IsWiderThan(visibility, answered.Visibility),
        _ => true,
    };

    /// <summary>
    ///     The count at the foot, right-aligned inside the padding: how much of the post's limit has been used, as the
    ///     instance will count it (#319) — muted while there is room, in the quota's low colour within the last tenth,
    ///     and an error's past the limit, so that a reader notices before the instance refuses the post.
    /// </summary>
    private Line Count(int width)
    {
        var used = PostLength.Of(Text, Warning, Limits);
        var count = $"{used} / {Limits.Characters}";

        // The most the count can reach and stay muted: past it is the last tenth of the limit.
        var nearlyFull = Limits.Characters * 9 / 10;

        var role = used > Limits.Characters ? Role.Error
            : used > nearlyFull ? Role.QuotaLow
            : Role.Muted;

        return Line.Of(Gap(width - Pad - Glyphs.Columns(count)), new Span(count, role));
    }

    private static Span Gap(int columns) => new(new string(' ', Math.Max(0, columns)), Role.Body);

    /// <summary>
    ///     The post this screen publishes: what was written, whatever warning is over it, and the post it answers
    ///     where it answers one.
    /// </summary>
    /// <remarks>
    ///     A cleared field sends no warning at all rather than putting the post behind a blank, an instance reading an
    ///     empty warning as none (<see cref="ContentWarnings.Written" />, <see cref="PostDraft.ContentWarning" />).
    ///     That is the one thing this reads differently from <see cref="Changed" />, and the whole reason both are
    ///     assembled here: they are one field meaning two things, and the screen holding the field is the only place
    ///     that can be expected to know which is which.
    ///     <para>
    ///         The visibility To shows, chosen only where the author moved it off what it opened on (#338): a
    ///         preference To started on is still a preference, and <c>PostAuthor</c> narrows one too wide for the post
    ///         being answered exactly as it does on the CLI, where a choice that wide is refused. Unknown sends nothing,
    ///         which leaves it to the account's own default — or, on a reply, to the post being answered.
    ///     </para>
    /// </remarks>
    /// <remarks>
    ///     What is attached goes as the pending attachments the instance handed back, in the order shown (#375) — so
    ///     this is asked only once every one of them is ready, which is what the shell waits for before it sends.
    ///     With them goes the author's own sensitive toggle (#379), and only theirs: the warning that may hold it on goes
    ///     as the warning, which marks the post sensitive by itself (ADR-0008).
    /// </remarks>
    private PostDraft Drafted() => new()
    {
        Text = Text,
        ContentWarning = ContentWarnings.Written(Warning),
        InReplyTo = Purpose == ComposeFor.Reply ? About?.Id : null,
        Visibility = Visibility,
        VisibilityChosen = Visibility != _startingVisibility,
        Language = Lang.Resolves(out var code) ? code : null,
        Sensitive = _sensitive && _attachments.Count > 0,
        Attached =
        [
            .. _attachments.Select(attachment => attachment.State is AttachmentState.Ready(var pending)
                ? pending
                : throw new InvalidOperationException("A draft is sent only once everything attached is ready.")),
        ],
    };

    /// <summary>The change this screen saves to the post it was opened on: its text, and the warning over it.</summary>
    /// <remarks>
    ///     Always said, never left silent: the field opened on the post's own warning, so whatever it holds now is what
    ///     the author wants (#140). The field as it stands rather than as <see cref="Drafted" /> reads it, since an
    ///     empty one here means "take the warning away" rather than "say nothing about it" — the third state
    ///     <see cref="PostEdit.ContentWarning" /> keeps for the CLI, where <c>--cw</c> can be absent from the command
    ///     line. Whitespace amounts to no warning here too, which
    ///     <see cref="PostEdit.ContentWarningWanted" /> reads off the same rule rather than a second one.
    ///     <para>
    ///         What this re-sends is the warning as the timeline last read it, which may be stale — the same exposure
    ///         the body already carries, the editor being pre-filled from the same post.
    ///     </para>
    /// </remarks>
    /// <remarks>
    ///     The language the same way (#340): whatever Lang holds, always — an empty one as an empty code, which is
    ///     <see cref="PostEdit.Language" />'s "clear it".
    /// </remarks>
    private PostEdit Changed() => new()
    {
        Text = Text,
        ContentWarning = Warning,
        Language = Lang.Resolves(out var code) ? code ?? string.Empty : null,
    };

    /// <summary>
    ///     The warning header: what this post is going behind, under the mark a warned post wears in the feed
    ///     (<see cref="PostLines.WarningMark" />) and in the role its warning is drawn in, so that a warning being
    ///     written looks like the warning it will become. The mark is lit while there is a warning or one is being
    ///     written, and dim while there is none — so a reader sees at a glance whether the post is going out behind one.
    /// </summary>
    /// <remarks>
    ///     Empty, it says so rather than going blank: a row a reader can type into is a row they have to be able to
    ///     find, and while it is being written it hints at what goes there instead. The value is painted as the field
    ///     laid over it shows it (#320), so the row reads the same with no terminal behind it; the caret is the
    ///     field's, and the terminal's own.
    /// </remarks>
    private Line WarningHeader(int room)
    {
        var role = WritingTheWarning || Warning.Length > 0 ? Role.ContentWarning : Role.Muted;

        return Warning.Length > 0
            ? Header(WarningLabel, role, new Span(TextWrap.Clip(Warning, room), Role.ContentWarning))
            : Header(WarningLabel, role, new Span(TextWrap.Clip(WarningHint, room), Role.Muted));
    }

    /// <summary>
    ///     The Media header's value in <paramref name="room" /> columns (#375), in the warning header's format and all
    ///     muted: what it holds — <c>none</c>, or how many of the instance's limit — then the key that adds to it, which
    ///     goes once the post is full. <paramref name="folded" />, it counts its rows too, on a terminal too short for
    ///     them: how many have no description and how many were refused, the refusals in the error's colour.
    ///     <para>
    ///         An edit's has no key, there being no way to add to it (#381): it says <c>none</c>, or how many the post
    ///         carries and that they are kept as they are — no limit, since nothing more can be put against it.
    ///     </para>
    /// </summary>
    private Span[] MediaValue(int room, bool folded)
    {
        var count = MediaRows;
        var held = count == 0 ? NothingAttached
            : TakesAttachments ? $"{count} of {Limits.Attachments}"
            : count.ToString(CultureInfo.InvariantCulture);

        // The header takes no selection bar with the typing on it: its own words light up, as To's choice does.
        var spans = new List<Span> { new(held, Typing == ComposeField.Media ? Role.SelectedText : Role.Muted) };
        var undescribed = _attachments.Count(attached => !attached.Described)
                          + Kept.Count(kept => string.IsNullOrWhiteSpace(kept.Description));

        if (folded && undescribed > 0)
        {
            spans.Add(new Span($" · {undescribed} {NoDescription}", Role.Muted));
        }

        if (folded && Refused > 0)
        {
            spans.Add(new Span(" · ", Role.Muted));
            spans.Add(new Span($"{Refused} failed", Role.Error));
        }

        if (AttachmentRoom > 0)
        {
            spans.Add(new Span($" · {AttachKey}", Role.Muted));
        }

        if (Kept.Count > 0)
        {
            spans.Add(new Span($" · {KeptAsTheyAre}", Role.Muted));
        }

        if (_attachments.Count > 0)
        {
            spans.Add(new Span(" · ", Role.Muted));
            spans.Add(SensitiveToggle());
        }

        return Fitted(spans, room);
    }

    /// <summary>
    ///     The sensitive toggle at the end of the Media header's line (#379): <c>□ sensitive</c>, muted, while it is off;
    ///     <c>■ sensitive</c> in the warning's colour while it is on, since it hides what is attached the way a warning
    ///     hides the post; and <c>■ sensitive (warning)</c>, the same, while a warning holds it on.
    /// </summary>
    private Span SensitiveToggle() =>
        SensitiveByAWarning ? new Span($"■ {SensitiveWord}{SensitiveByWarning}", Role.ContentWarning)
        : Sensitive ? new Span($"■ {SensitiveWord}", Role.ContentWarning)
        : new Span($"□ {SensitiveWord}", Role.Muted);

    /// <summary>
    ///     A row for an attachment the post being edited already carries, in <paramref name="room" /> columns from the
    ///     header's value column (#381): its kind, then its description in quotes or the quiet mark. Nothing a pending
    ///     attachment's row has for changing it — no grip, no <c>x</c> — and no name or size, which the instance does not
    ///     hand back.
    /// </summary>
    private static Line KeptRow(PostMedia kept, int room)
    {
        var description = kept.Description?.ReplaceLineEndings(" ").Trim() ?? string.Empty;
        Span[] status = description.Length > 0
            ? [new Span($"“{description}”", Role.Body)]
            : [new Span(NoDescription, Role.Muted)];

        return Line.Of(
        [
            Gap(Pad + LabelWidth + LabelGap),
            .. Fitted(
                [new Span(Glyphs.Padded(ComposeAttachment.KindWordOf(kept.Kind), KindColumn + 2), Role.Muted), .. status],
                room),
        ]);
    }

    /// <summary>
    ///     A pending attachment's row, <paramref name="width" /> columns wide (#375): the selection bar where the row is
    ///     picked (#378), its grip, the column its picture goes in, its name, its kind, its size, the <c>x</c> that
    ///     takes it off, and one status column saying where it has got to. Every column but the status sits where the
    ///     terminal's width puts it, never where a name does, so that nothing on a row moves as it goes up, as more are
    ///     attached, or as the walk passes over it.
    /// </summary>
    /// <remarks>
    ///     The picture's column is held blank until pictures are drawn on it (#382), where it will be, so that it moves
    ///     nothing when it comes.
    /// </remarks>
    private Line AttachmentRow(ComposeAttachment attached, int width) =>
        Line.Of([.. AttachmentRuns(attached, width).Select(run => run.Span)]);

    /// <summary>
    ///     A pending attachment's row as <see cref="AttachmentRow" /> draws it, each run with what a click on it means
    ///     (#378): the <c>x</c> takes it off, <c>retry (r)</c> sends it up again, and anywhere else is the row.
    /// </summary>
    private List<(Span Span, AttachmentPart Part)> AttachmentRuns(ComposeAttachment attached, int width)
    {
        var (name, kind, status) = RowColumns(width);
        var picked = Typing == ComposeField.Attachment && ReferenceEquals(PickedAttachment, attached);

        return
        [
            // The bar fills only the left half of its cell, so set right against the grip it reads as one space.
            .. picked
                ? [(Gap(GripAt - 1), AttachmentPart.Row), (new Span(SelectionBar, Role.Selection), AttachmentPart.Row)]
                : new[] { (Gap(GripAt), AttachmentPart.Row) },
            (new Span(Grip, Role.Muted), AttachmentPart.Row),
            (Gap(3), AttachmentPart.Row),
            (new Span(
                Glyphs.Padded(TextWrap.Clip(attached.Name, name), name),
                picked ? Role.SelectedText : Role.Body), AttachmentPart.Row),
            (Gap(2), AttachmentPart.Row),
            .. kind
                ? [(new Span(Glyphs.Padded(attached.KindWord, KindColumn), Role.Muted), AttachmentPart.Row)]
                : Array.Empty<(Span, AttachmentPart)>(),
            (new Span(attached.Size.PadLeft(SizeColumn), Role.Muted), AttachmentPart.Row),
            (Gap(1), AttachmentPart.Row),

            // The x's cell and one either side of it, so that a click a column off still takes it off.
            (Gap(1), AttachmentPart.Remove),
            (new Span(RemoveMark, Role.Destructive), AttachmentPart.Remove),
            (Gap(1), AttachmentPart.Remove),
            (Gap(1), AttachmentPart.Row),
            .. Status(attached, status),
        ];
    }

    /// <summary>
    ///     How a row's columns share <paramref name="width" />: the name 32 wide, the kind, and whatever is left for the
    ///     status up to its most. Where that leaves the status fewer than its least, the kind goes first and then the
    ///     name narrows, as far as its own least (#375).
    /// </summary>
    private static (int Name, bool Kind, int Status) RowColumns(int width)
    {
        // The grip, the picture and the gaps either side of them come to the header's value column, where names start.
        var room = width - Pad - (Pad + LabelWidth + LabelGap);
        var name = MostName;
        var kind = true;

        int Status() => room - name - 2 - (kind ? KindColumn : 0) - SizeColumn - 2 - 1 - 2;

        if (Status() < LeastStatus)
        {
            kind = false;
        }

        if (Status() < LeastStatus)
        {
            name = Math.Max(LeastName, name - (LeastStatus - Status()));
        }

        return (name, kind, Math.Clamp(Status(), 0, MostStatus));
    }

    /// <summary>
    ///     What a row's status column says in <paramref name="room" /> columns, one thing at a time (#375): the upload's
    ///     gauge and how far it has got, that the instance is processing it, why it was refused, or — ready — its
    ///     description in quotes, or the quiet mark where it has none. No mark for ready: a row saying nothing else is
    ///     one that is done.
    /// </summary>
    private static IEnumerable<(Span Span, AttachmentPart Part)> Status(ComposeAttachment attached, int room) =>
        attached.State is AttachmentState.Refused refused
            ? Failure(refused, room)
            : Fitted(attached.State switch
                {
                    AttachmentState.Sending(var done) => Gauge(done),
                    AttachmentState.Processing => [new Span(BeingProcessed, Role.Muted)],
                    _ when attached.Described =>
                        [new Span($"“{attached.Description.ReplaceLineEndings(" ").Trim()}”", Role.Body)],
                    _ => [new Span(NoDescription, Role.Muted)],
                }, room)
                .Select(span => (span, AttachmentPart.Row));

    /// <summary>
    ///     A refusal in <paramref name="room" /> columns (#378): why, in the error's colour and with no mark beside the
    ///     <c>x</c> to double it, then <c>retry (r)</c> where trying again could mend it — a dropped connection, never a
    ///     file the instance will not take. Where both do not fit, the offer shortens to <c>(r)</c> and the reason is
    ///     cut, so that the key is still there to be read.
    /// </summary>
    private static IEnumerable<(Span Span, AttachmentPart Part)> Failure(AttachmentState.Refused refused, int room)
    {
        if (!refused.Retryable)
        {
            return Fitted([new Span(refused.Why, Role.Error)], room).Select(span => (span, AttachmentPart.Row));
        }

        var offer = Glyphs.Columns(refused.Why) + 2 + Glyphs.Columns(RetryOffer) <= room ? RetryOffer : ShortRetryOffer;
        var why = Math.Max(0, room - 2 - Glyphs.Columns(offer));

        return
        [
            (new Span(TextWrap.Clip(refused.Why, why), Role.Error), AttachmentPart.Row),
            (Gap(2), AttachmentPart.Row),
            (new Span(offer, Role.Key), AttachmentPart.Retry),
        ];
    }

    /// <summary>An upload's gauge, <c>████░░░░  54%</c>: eight cells filled as far as it has got, then the share of it.</summary>
    private static Span[] Gauge(double done)
    {
        var share = Math.Clamp(done, 0, 1);
        var filled = (int)Math.Round(share * GaugeCells);

        return
        [
            new Span(new string('█', filled), Role.Gauge),
            new Span(new string('░', GaugeCells - filled), Role.GaugeEmpty),
            new Span(string.Create(CultureInfo.InvariantCulture, $" {(int)Math.Round(share * 100),3}%"), Role.Muted),
        ];
    }

    /// <summary>
    ///     <paramref name="spans" /> cut to <paramref name="room" /> columns, the last that does not fit ending in an
    ///     ellipsis and anything after it left off.
    /// </summary>
    private static Span[] Fitted(IEnumerable<Span> spans, int room)
    {
        var fitted = new List<Span>();

        foreach (var span in spans)
        {
            if (span.Width <= room)
            {
                fitted.Add(span);
                room -= span.Width;

                continue;
            }

            if (room > 0)
            {
                fitted.Add(span with { Text = TextWrap.Clip(span.Text, room) });
            }

            break;
        }

        return [.. fitted];
    }

    /// <summary>One row of the layout, and how long it holds out on a terminal too short for every row.</summary>
    private sealed record Row(Line Line, Keep Keep);

    /// <summary>What a run of To's row means to a click on it.</summary>
    private abstract record ToPart
    {
        /// <summary>One of the choices: a visibility, or <see langword="null" /> for "account default".</summary>
        public sealed record Choice(PostVisibility? Visibility) : ToPart;

        /// <summary>An arrow either side of the one value shown, stepping <paramref name="By" /> along the row.</summary>
        public sealed record Step(int By) : ToPart;
    }

    /// <summary>
    ///     How long a row holds out on a terminal too short for everything, lowest first to go: what is being
    ///     answered gives way first, then the blanks — which say nothing — before the reply header, which says where
    ///     the reply will land.
    /// </summary>
    private enum Keep
    {
        Quote,
        TopBlank,
        BlankUnderHairline,
        ReplyHeader,
        FootHairline,
        CountRow,
        From,

        /// <summary>
        ///     The Media header, which holds out past From and Lang — a draft's attachments are part of what goes out,
        ///     and a header saying how many there are is the last word of them left once their rows have folded into it.
        /// </summary>
        Media,

        HeaderHairline,
        Always,
    }
}
