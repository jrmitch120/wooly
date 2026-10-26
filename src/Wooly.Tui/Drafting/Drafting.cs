using Wooly.Core.Accounts;
using Wooly.Core.Conversations;
using Wooly.Core.Posts;
using Wooly.Core.Profiles;
using Wooly.Tui.Clipboard;
using Wooly.Tui.Screens;
using Wooly.Tui.Shell;

namespace Wooly.Tui.Drafting;

/// <summary>
///     What the session does for the **draft**s it has open, beyond holding what is written (ADR-0029): opening a compose
///     screen to post, reply or edit, with the defaults it starts on and the limits it is measured against; attaching
///     what is pasted, dropped or chosen in the file browser, sending it up and describing it; every change to a draft
///     and to its attachments' rows; and sending, at once or once what it waits on is done. One for the session, beside
///     the shell, rather than one per draft: what it remembers outlives any one draft. The edit public profile form is
///     the other draft, and everything it does but ask before it is thrown away is here too: opening it, walking and
///     flipping it, choosing or pasting its pictures, and saving it (#422).
/// </summary>
/// <remarks>
///     It reaches the shell through two things and no more: the stack (<see cref="IStack" />), and the
///     <see cref="Reach" /> a screen gets for a verb of its own — read afresh on every call, so a switch of profile needs
///     no rewiring. A draft verb comes in through <see cref="Do" />, a view's change to a draft through
///     <see cref="Edit{T}" />, and the shell tells it when a screen leaves (<see cref="Left" />) and when one is
///     uncovered (<see cref="Uncovered" />).
///     <para>
///         Everything here runs on the drawing thread: what an instance answers is brought back onto it through the
///         shell host, as it always was.
///     </para>
/// </remarks>
public sealed class Drafting
{
    private readonly IStack _stack;
    private readonly Func<Reach?> _reach;
    private readonly IShellHost _host;

    /// <summary>
    ///     This machine's clipboard, which <c>ctrl-v</c> on a compose screen attaches a picture or copied files from
    ///     (#380). Not a port: it is not on an instance.
    /// </summary>
    private readonly IClipboard _clipboard;

    /// <summary>
    ///     Where pictures pasted from the clipboard are written before they are attached (#380) — a temporary folder of
    ///     the session's, which whoever made it takes away as the session ends (review of #372).
    /// </summary>
    private readonly PastedPictures _pasted;

    /// <summary>
    ///     Whether the status row has said over a compose that this machine has nothing to read the clipboard with, which
    ///     it says there once a session (#380).
    /// </summary>
    private bool _toldNoClipboard;

    /// <summary>The folder Wooly was launched from, where the file browser opens the first time (#376).</summary>
    private readonly string _launchedFrom;

    /// <summary>
    ///     The folder the author last attached from through the file browser, where it opens from then on — for this
    ///     session only, saved nowhere (#376).
    /// </summary>
    private string? _lastFolder;

    /// <summary>
    ///     What the config file says a post goes out at — <c>default_visibility</c> and <c>default_language</c>, either
    ///     unknown where it says nothing — which a compose screen's To and Lang start on ahead of the account's own
    ///     (ADR-0024, #338, #340).
    /// </summary>
    private readonly PostDefaults _preferred;

    /// <summary>How long each instance lets a post be, asked as posts are first written there (#319).</summary>
    private readonly LimitsByInstance _limits;

    /// <summary>
    ///     What each account acted as posts at by default, asked as the session starts acting as it — the fallback for
    ///     what compose starts on after the config's own preferences (ADR-0024, #339).
    /// </summary>
    private readonly DefaultsByProfile _defaults;

    /// <summary>
    ///     The calls each compose screen's pending attachments make to the instance — sent up as attached, and described
    ///     (ADR-0026, #375, #377) — whose answers this feeds into the screen.
    /// </summary>
    private readonly AttachmentCalls _attachments;

    /// <summary>
    ///     The compose screen whose <c>ctrl-s</c> is waiting on its attachments to finish before it sends (ADR-0026),
    ///     or none.
    /// </summary>
    private ComposeScreen? _waitingToSend;

    /// <param name="stack">The stack of screens, which a compose is pushed onto.</param>
    /// <param name="reach">
    ///     What a screen can reach of the shell, as it is now: read on every call, so that it is always whoever is
    ///     being acted as — or nobody, while the shell has nobody to act as.
    /// </param>
    /// <param name="ports">
    ///     What an instance's limits and an account's defaults are asked through, and what is attached is sent up
    ///     through.
    /// </param>
    /// <param name="host">The drawing thread, which their answers are handed back on.</param>
    /// <param name="clipboard">This machine's clipboard, which a paste attaches a picture or copied files from.</param>
    /// <param name="pasted">Where a picture pasted from the clipboard is written before it is attached.</param>
    /// <param name="preferred">What the config file says a post goes out at.</param>
    /// <param name="launchedFrom">The folder Wooly was launched from, where the file browser first opens.</param>
    public Drafting(
        IStack stack,
        Func<Reach?> reach,
        ShellPorts ports,
        IShellHost host,
        IClipboard clipboard,
        PastedPictures pasted,
        PostDefaults preferred,
        string launchedFrom)
    {
        _launchedFrom = launchedFrom;
        _stack = stack;
        _reach = reach;
        _host = host;
        _clipboard = clipboard;
        _pasted = pasted;
        _preferred = preferred;
        _limits = new LimitsByInstance(ports.Limits, host);
        _limits.Heard += Measured;
        _defaults = new DefaultsByProfile(ports.Defaults, host);
        _attachments = new AttachmentCalls(ports.Author, host);
        _attachments.Heard += Progressed;
        _attachments.Told += Told;
        _attachments.Untold += Untold;
    }

    private Reach Reach => _reach() ?? throw new InvalidOperationException("Nobody is being acted as.");

    /// <summary>
    ///     Carries out <paramref name="verb" /> where it is a draft verb, which is the whole of how the shell's verb route
    ///     knows one: there is no list of them anywhere else.
    /// </summary>
    /// <param name="verb">What was pressed or clicked.</param>
    /// <param name="on">
    ///     The attachment a click acted on, where it was one — taken off, sent up again or described — rather than the one
    ///     the walk is on: a click on a row's part is the verb that part stands for, with the row's attachment (#378).
    /// </param>
    /// <returns>
    ///     <see cref="Drafted.NotMine" /> for any verb that is not a draft's, which the shell carries on with;
    ///     otherwise whether it was used or declined. <c>esc</c> is not among them: it reaches drafting directly
    ///     (<see cref="Back" />), and only once the shell has no question open.
    /// </returns>
    public Drafted Do(Verb verb, ComposeAttachment? on = null) => verb switch
    {
        Verb.Compose => Ran(() => Compose(ComposeFor.Post)),
        Verb.Reply => Ran(() => Compose(ComposeFor.Reply)),
        Verb.Edit => Ran(() => Compose(ComposeFor.Edit)),
        Verb.Describe => Ran(() => Describe(on)),
        Verb.RemoveAttachment => Ran(() => RemoveAttachment(compose => on is null ? compose.Remove() : compose.Remove(on))),
        Verb.RetryAttachment => Ran(() => Retry(compose => on is null ? compose.Retry() : compose.Media.Retry(on))),
        Verb.PasteFromTheClipboard => PasteFromTheClipboard() ? Drafted.Used : Drafted.Declined,
        Verb.WriteWarning => Ran(() => Edit<ComposeScreen>(compose => compose.WriteTheWarning())),

        // Answered whether or not there was anywhere to go, so that an arrow off either end of the walk or of To stops
        // there rather than falling through to Terminal.Gui, which would carry the focus round to the other end.
        Verb.PreviousField => Ran(() => Edit<ComposeScreen>(compose => compose.Walk(-1))),
        Verb.NextField => Ran(() => Edit<ComposeScreen>(compose => compose.Walk(1))),
        Verb.PreviousChoice => Ran(() => Edit<ComposeScreen>(compose => compose.Choose(-1))),
        Verb.NextChoice => Ran(() => Edit<ComposeScreen>(compose => compose.Choose(1))),
        Verb.BringBackAttachment => Ran(() => Edit<ComposeScreen>(compose => compose.BringBack())),
        Verb.EarlierAttachment => Ran(() => Edit<ComposeScreen>(compose => compose.Reorder(-1))),
        Verb.LaterAttachment => Ran(() => Edit<ComposeScreen>(compose => compose.Reorder(1))),
        Verb.ToggleSensitive => Ran(ToggleSensitive),
        Verb.ListAttachments => Ran(ListAttachments),
        Verb.OpenBrowser => Ran(Browse),
        Verb.UseChosen => Ran(UseChosen),
        Verb.IntoFolder => Ran(() => Browse(browser => browser.FolderPicked)),
        Verb.UpFolder => Ran(() => Browse(browser => browser.Above)),

        // The edit public profile form's, the second draft (#422).
        Verb.EditPublicProfile => Ran(() => _ = EditPublicProfile()),
        Verb.FlipToggle => Ran(() => Edit<PublicProfileScreen>(form => form.Flip())),
        Verb.SavePublicProfile => Ran(() => _ = SavePublicProfile()),
        Verb.PreviousPublicProfileField => Ran(() => Edit<PublicProfileScreen>(form => form.Walk(-1))),
        Verb.NextPublicProfileField => Ran(() => Edit<PublicProfileScreen>(form => form.Walk(1))),
        Verb.ChoosePicture => Ran(ChoosePicture),
        Verb.PastePicture => Ran(PastePicture),
        _ => Drafted.NotMine,
    };

    /// <summary>
    ///     Makes <paramref name="change" /> to the draft in front — every change a view makes to one comes in here — and
    ///     settles what follows from it by the one rule (#364): a change that says it <see cref="DraftChange.Edited" />
    ///     spends a notice said over the draft, and anything but no change at all is announced so the screen is drawn
    ///     again.
    /// </summary>
    /// <remarks>
    ///     The draft in front is the screen on top where it is a <typeparamref name="T" />, or the compose under the
    ///     attachments screen listing its rows on a short terminal, whose keys and clicks change the compose's draft as
    ///     the same ones on its rows do (story 58, review of #372).
    ///     <para>
    ///         A notice is spent by an edit of the draft and not by a move (#319). The status row holds a notice or the
    ///         keymap and never both, and while a post is being written the keys go to its fields rather than to
    ///         anything that would otherwise take a notice down — so a refusal of the post would stand, hiding every key
    ///         compose answers to, until <c>esc</c> threw the draft away. Changing the draft is doing what the notice
    ///         asked; walking the fields, choosing on To or opening a list is not, and leaves the notice to be read.
    ///     </para>
    /// </remarks>
    /// <returns>What the change was, or nothing where no such draft is in front and no change was made.</returns>
    public DraftChange Edit<T>(Func<T, DraftChange> change)
        where T : Screen
    {
        var draft = _stack.Top switch
        {
            T top => top,
            AttachmentsScreen { Compose: T listed } => listed,
            _ => null,
        };

        return draft is null ? DraftChange.None : Settled(change(draft));
    }

    /// <summary>
    ///     A paste on a compose screen made of files dropped onto the terminal (<see cref="Dropped" />, #375): they are
    ///     attached, as many as the post has room for, and start going up.
    /// </summary>
    /// <returns>Whether the paste was files it attached; anything else is left to be typed.</returns>
    public bool Paste(string text)
    {
        if (_stack.Top is not ComposeScreen { Media.Takes: true } compose
            || Dropped.Paths(text, compose.Limits) is not { } dropped)
        {
            return false;
        }

        Attach(compose, dropped);

        return true;
    }

    /// <summary>
    ///     <c>ctrl-v</c> or <c>alt-v</c> on a compose screen, or on the attachments screen over one (#380): a picture on
    ///     this machine's clipboard is written to a file of its own and attached from there, and copied files are
    ///     attached, as a drop is (<see cref="Paste" />, ADR-0026).
    /// </summary>
    /// <returns>Whether the paste was taken; one that was not is declined, and left to the field's own paste.</returns>
    private bool PasteFromTheClipboard()
    {
        if (InFront is not { Media.Takes: true } compose)
        {
            return false;
        }

        switch (_clipboard.Read())
        {
            case Clipped.Picture(var png):
                Attach(compose, [_pasted.Keep(png)]);

                return true;

            // Subject to what a drop is: the files there that the instance takes, up to what the post has room for.
            case Clipped.Files(var paths):
                var accepted = paths.Where(path => File.Exists(path) && compose.Limits.Accepts(path)).ToList();

                if (accepted.Count == 0)
                {
                    Reach.Say("None of the copied files is a type this instance takes.", isError: true);
                }
                else
                {
                    Attach(compose, accepted);
                }

                return true;

            // Said once a session: the author who has no wish to install either is still pasting text every time. Said
            // once the field has pasted, since the paste is an edit and an edit spends a notice (#364).
            case Clipped.NoTool(var why) when !_toldNoClipboard:
                _toldNoClipboard = true;
                _host.OnUiThread(() => Reach.Say(why, isError: false));

                return false;

            default:
                return false;
        }
    }

    /// <summary>
    ///     Attaches the files at <paramref name="paths" /> to <paramref name="compose" />, as many as it has room for,
    ///     and starts each going up to the instance there and then (ADR-0026, #375). Attaching changes the draft, which
    ///     spends a notice over it as typing does (#364) — and where some would not fit, the status row says how many
    ///     were left out, so that nobody is unsure what is on the post (#378).
    /// </summary>
    private void Attach(ComposeScreen compose, IReadOnlyCollection<string> paths)
    {
        var attached = compose.Media.Attach(paths.Select(path => ComposeAttachment.Of(path, LocalFiles.Size(path))));

        _ = Settled(attached.Count > 0 ? DraftChange.Edited : DraftChange.None);

        if (paths.Count - attached.Count is > 0 and var left)
        {
            var most = $"{compose.Limits.Attachments} is the most a post can carry.";

            Reach.Say(attached.Count == 0 ? $"Nothing attached — {most}" : $"{left} left out — {most}", isError: true);
        }

        foreach (var attachment in attached)
        {
            _ = _attachments.SendUp(Reach.Profile, compose, attachment);
        }
    }

    /// <summary>
    ///     Where the file browser opens: the folder last attached or chosen from this session or, the first time, the one
    ///     Wooly was launched from (#376).
    /// </summary>
    private string BrowsingFrom => _lastFolder is { } last && Directory.Exists(last) ? last : _launchedFrom;

    /// <summary>
    ///     <c>ctrl-s</c> on a compose screen, which the window carries out: sends what it says, once it says something,
    ///     and puts the screen away on success.
    /// </summary>
    /// <remarks>
    ///     A new post and an edit of one go out the same way here — the screen says which it is
    ///     (<see cref="Outgoing" />), and a stack: the call, the pop, and what the reader is told — and the two arms below
    ///     are the two writes a post author has rather than two ways of putting a post together.
    ///     <para>
    ///         Each arm says the whole of what its own landing means, notice included, rather than matching once here
    ///         and asking <see cref="ComposeScreen.Purpose" /> the same question again afterwards. The one thing left
    ///         that reads the purpose is a different question: a reply written inside a conversation is put at the end
    ///         of it, which is about where the reader is standing rather than about what went out.
    ///     </para>
    /// </remarks>
    public async Task Send()
    {
        if (_stack.Top is not ComposeScreen compose)
        {
            return;
        }

        if (compose.IsEmpty)
        {
            Reach.Say("There is nothing written to send.", isError: true);

            return;
        }

        // A Lang holding something that is not a language is a typo, and a typo is not published as one (#340).
        if (compose.Lang.Refusal is { } refusal)
        {
            Reach.Say(refusal, isError: true);

            return;
        }

        // A post never goes out without something its author attached (ADR-0026): not without one the instance refused,
        // and not before the rest are ready, which it waits for rather than refuses — esc calls the wait off (#375).
        if (compose.Media.Refused > 0)
        {
            Reach.Say(
                compose.Media.Refused == 1
                    ? "Something attached was refused — take it off to send."
                    : $"{compose.Media.Refused} attachments were refused — take them off to send.",
                isError: true);

            return;
        }

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var reach = Reach;
        var profile = reach.Profile;

        // A description the instance has not yet taken goes now — one that failed to before is sent again on the
        // author's ctrl-s, never on its own (ADR-0006) — and the send waits on it as on an upload (#377).
        foreach (var untold in compose.Media.Attachments.Where(attachment => attachment.Untold))
        {
            _ = _attachments.Describe(profile, compose, untold);
        }

        if (compose.Media.Attachments.Count(attachment => attachment.Unfinished) is > 0 and var unfinished)
        {
            _waitingToSend = compose;

            SayWaiting(unfinished);

            return;
        }

        _waitingToSend = null;

        switch (compose.Outgoing)
        {
            case Outgoing.Saving(var postId, var edit):
                await reach.Put(
                    ask => ask.Of(token => reach.Ports.Author.Edit(profile, postId, edit, token)),
                    eitherWay: saved =>
                    {
                        Popped();
                        Reach.Tell(new Change.PostChanged(saved));
                        Reach.Say("Saved.", isError: false);
                    });

                break;

            case Outgoing.Publishing(var draft):
                // A reply to a message carries the conversation it is in, taken now from whichever conversation on the
                // stack holds what it answers — beneath the compose or beneath a post opened from it (#484) — rather
                // than read back when it lands, by when the reader may be anywhere.
                var within = compose.Purpose == ComposeFor.Reply ? HeldIn(compose.About) : null;

                // And which screen it was written from, taken now for the same reason: only that one moves its pick to
                // the reply once it lands, wherever the reader is by then (#482).
                var from = compose.Purpose == ComposeFor.Reply ? Beneath(compose) : null;

                await reach.Put(
                    ask => ask.Of(token => reach.Ports.Author.PublishAttached(profile, draft, token)),
                    eitherWay: published =>
                    {
                        // A reply written in a conversation goes on the end of it, which the conversation and the
                        // list it was opened from each hear for themselves.
                        Popped();
                        Reach.Tell(new Change.PostSent(published, within, from));
                        Reach.Say("Sent.", isError: false);
                    });

                break;
        }

        // What both arms do to get back to where the reader was: the compose that was sent off the stack, with whatever
        // has been put over it meanwhile, before the change is heard by what is left on it — named rather than read off
        // the top, so an answer that lands after something else is in front takes off only what it was about, and
        // nothing where the compose has already gone (#423).
        void Popped() => _stack.TakeOff(compose);
    }

    /// <summary>
    ///     The session has started acting as <paramref name="profile" />, at launch or at a switch: its account's
    ///     defaults are asked now, where nobody has yet, so that opening a compose asks nothing (ADR-0024).
    /// </summary>
    public void ActingAs(ActiveProfile profile) => _defaults.Ask(profile);

    /// <summary>
    ///     <paramref name="screen" /> is about to come off the stack, by whichever way out: called for each screen
    ///     before it is off, so that what it is about is still on the stack beneath it.
    /// </summary>
    /// <remarks>
    ///     A compose leaving calls off whatever it is still sending up, and a send waiting on it with it (#375). A
    ///     description is done as its editor is left, by whichever way out, and goes to the instance then (#377).
    /// </remarks>
    public void Left(Screen screen)
    {
        switch (screen)
        {
            case ComposeScreen compose:
                if (ReferenceEquals(_waitingToSend, compose))
                {
                    _waitingToSend = null;
                }

                _attachments.LetGo(compose);

                break;

            case DescriptionScreen described:
                _ = _attachments.Describe(Reach.Profile, described.Compose, described.Attachment);

                break;
        }
    }

    /// <summary>
    ///     The stack has settled with a screen uncovered, its notice cleared — by <c>esc</c>, or by walking back down
    ///     it.
    /// </summary>
    /// <remarks>
    ///     Compose back in front with a send still asked for on it: sent, where everything it waited on finished while
    ///     it was behind another screen, and otherwise said again, since the screen that was open over it took the
    ///     notice off the status row.
    /// </remarks>
    public void Uncovered()
    {
        if (_stack.Top is not ComposeScreen compose || !ReferenceEquals(_waitingToSend, compose))
        {
            return;
        }

        if (compose.Media.Attachments.Count(attachment => attachment.Unfinished) is > 0 and var unfinished)
        {
            SayWaiting(unfinished);

            return;
        }

        Waited(compose);
    }

    /// <summary>
    ///     <c>esc</c>, once the shell has no question open to answer with it: while a send is waiting on the compose in
    ///     front, which is a level of its own over the draft, the send is called off and the draft left as it was, to be
    ///     changed or sent again (ADR-0026, #375).
    /// </summary>
    /// <returns>Whether there was a send to call off; where there was not, <c>esc</c> is the shell's.</returns>
    public bool Back()
    {
        if (_waitingToSend is null || !ReferenceEquals(_waitingToSend, _stack.Top))
        {
            return false;
        }

        _waitingToSend = null;

        Reach.Say("Not sent — the draft is as it was.", isError: false);

        return true;
    }

    /// <summary>
    ///     What follows from <paramref name="made" />, a change to a draft, by the one rule both kinds of draft keep
    ///     (#364): an edit spends a notice said over it, and anything but no change at all is announced so the screen is
    ///     drawn again.
    /// </summary>
    /// <returns><paramref name="made" />.</returns>
    private DraftChange Settled(DraftChange made)
    {
        if (made == DraftChange.Edited)
        {
            // Saying nothing takes the notice down where there is one, and announces the change either way.
            Reach.Say(null, isError: false);
        }
        else if (made != DraftChange.None)
        {
            Reach.Changed();
        }

        return made;
    }

    /// <summary>
    ///     The compose screen in front: on top, or under the attachments screen listing its rows on a short terminal
    ///     (story 58).
    /// </summary>
    private ComposeScreen? InFront => _stack.Top switch
    {
        ComposeScreen compose => compose,
        AttachmentsScreen listing => listing.Compose,
        _ => null,
    };

    /// <summary>
    ///     <c>ctrl-o</c> on a compose or a reply, <c>⏎</c> on its Media header or a click on its words: pushes the file
    ///     browser over it, in the folder last attached from this session or, the first time, the one Wooly was launched
    ///     from (#376). Not on an edit, which has nothing to attach to, nor on a post that carries all it can.
    /// </summary>
    private void Browse()
    {
        if (InFront is not { Media.Takes: true } compose)
        {
            return;
        }

        if (compose.Media.Room == 0)
        {
            Reach.Say(
                $"This post carries all it can — {compose.Media.Attachments.Count} of {compose.Limits.Attachments}.",
                isError: false);

            return;
        }

        _stack.Push(new FileBrowserScreen(LocalFiles.Listing(BrowsingFrom), compose.Limits, compose.Media.Room));
    }

    /// <summary>
    ///     <c>→</c>, <c>←</c> or <c>⏎</c> in the file browser: the folder <paramref name="going" /> says, read off the disk
    ///     here and shown there (#376) — the screen reading nothing itself (ADR-0015). Nothing where it says none.
    /// </summary>
    private void Browse(Func<FileBrowserScreen, string?> going)
    {
        if (_stack.Top is not FileBrowserScreen browser || going(browser) is not { } folder)
        {
            return;
        }

        browser.Show(LocalFiles.Listing(folder));
        Reach.Changed();
    }

    /// <summary>
    ///     <c>⏎</c> in the file browser: the browser goes and what was chosen — or, with nothing chosen, the file under the
    ///     cursor — is handed to the draft it was opened over, and the folder it came from is remembered for the next
    ///     time. A compose attaches it, exactly as a drop is (#375, #376); the edit public profile form takes the one file
    ///     as the picture its walk is on, sent when the form is saved and not before (#407, ADR-0028). With neither, the
    ///     folder under the cursor opens: what is chosen comes first wherever the cursor is (review of #372).
    /// </summary>
    private void UseChosen()
    {
        if (_stack.Top is not FileBrowserScreen browser)
        {
            return;
        }

        var chosen = browser.Take();

        if (chosen.Count == 0)
        {
            Browse(opening => opening.FolderPicked);

            return;
        }

        _lastFolder = browser.Folder;

        if (OpenedOver(browser) is not { } draft)
        {
            return;
        }

        _stack.TakeOff(browser);

        switch (draft)
        {
            case ComposeScreen compose:
                Attach(compose, chosen);

                break;

            case PublicProfileScreen form:
                _ = Settled(form.Pick(chosen[0]));

                break;
        }
    }

    /// <summary>
    ///     The draft <paramref name="browser" /> was opened over: the nearest beneath it on the stack — the compose under
    ///     the attachments screen it was opened from counting as that compose (story 58) — or none.
    /// </summary>
    private Screen? OpenedOver(FileBrowserScreen browser) => _stack.Screens
        .TakeWhile(screen => !ReferenceEquals(screen, browser))
        .Select(screen => screen is AttachmentsScreen listing ? listing.Compose : screen)
        .LastOrDefault(screen => screen is IDraft);

    /// <summary>
    ///     The screen <paramref name="screen" /> was opened over: the one beneath it on the stack, found by the screen
    ///     itself rather than by where it stands from the top — or none.
    /// </summary>
    private Screen? Beneath(Screen screen) => _stack.Screens
        .TakeWhile(below => !ReferenceEquals(below, screen))
        .LastOrDefault();

    /// <summary>
    ///     The id of the conversation <paramref name="answered" /> is in: that of the nearest conversation screen on the
    ///     stack holding it, wherever on the stack that is (#484) — or none, where no conversation on it does.
    /// </summary>
    private string? HeldIn(Post? answered) => answered is null
        ? null
        : _stack.Screens
            .OfType<ConversationScreen>()
            .LastOrDefault(conversation => conversation.Posts.Any(post => post.Id == answered.Id))
            ?.Conversation.Id;

    /// <summary>
    ///     Opens the description editor over the compose screen in front, on the attachment whose row the walk is on
    ///     (#377) — or on <paramref name="on" />, a row clicked, which is picked first so that the editor's done comes back
    ///     to the row it was opened from. Nothing where it is on no row.
    /// </summary>
    private void Describe(ComposeAttachment? on)
    {
        if (on is not null)
        {
            _ = Edit<ComposeScreen>(compose => compose.Pick(on));
        }

        if (InFront is { Typing: ComposeField.Attachment, Media.Picked: { } attachment } compose)
        {
            _stack.Push(new DescriptionScreen(compose, attachment));
        }
    }

    /// <summary>
    ///     <c>s</c> on compose's Media header, or a click on its toggle: puts what is attached behind a click or takes it
    ///     back out (#379). While a warning holds it on, the status row says so instead, since a press that changes
    ///     nothing on screen would otherwise read as one the shell missed.
    /// </summary>
    private void ToggleSensitive()
    {
        if (InFront is { SensitiveByAWarning: true, Media.Attachments.Count: > 0 })
        {
            Reach.Say("The warning already hides what is attached.", isError: false);

            return;
        }

        _ = Edit<ComposeScreen>(compose => compose.ToggleSensitive());
    }

    /// <summary>
    ///     <c>⏎</c> on compose's Media header, or a click on its line, where a terminal too short for a row each folded
    ///     the rows into it: pushes the attachments screen listing them, the first picked (story 58).
    /// </summary>
    private void ListAttachments()
    {
        if (_stack.Top is not ComposeScreen { RowsFolded: true } compose)
        {
            return;
        }

        compose.Pick(compose.Media.Attachments[0]);
        _stack.Push(new AttachmentsScreen(compose));
    }

    /// <summary>
    ///     Takes an attachment off the compose in front, the one <paramref name="removing" /> says (#378) — and where a
    ///     send was waiting on nothing else, the send: what it waited on is no longer on the post.
    /// </summary>
    private void RemoveAttachment(Func<ComposeScreen, DraftChange> removing)
    {
        if (Edit(removing) != DraftChange.None && InFront is { } compose)
        {
            Waited(compose);
        }
    }

    /// <summary>
    ///     Sends up again the attachment <paramref name="retrying" /> starts over on the compose in front, where a retry
    ///     could mend its refusal (#378) — the author's to ask for, never done by itself (ADR-0006).
    /// </summary>
    private void Retry(Func<ComposeScreen, ComposeAttachment?> retrying)
    {
        if (InFront is not { } compose || retrying(compose) is not { } attachment)
        {
            return;
        }

        Reach.Changed();

        _ = _attachments.SendUp(Reach.Profile, compose, attachment);
    }

    /// <summary>
    ///     Where <paramref name="attachment" /> on <paramref name="compose" /> has got to, heard on the drawing thread —
    ///     and, where that was the last thing a waiting send was waiting on, the send (ADR-0026, #375).
    /// </summary>
    /// <remarks>
    ///     A report for a compose no longer on the stack is ignored: one already on its way as the compose left still
    ///     lands, and the draft it was for has gone (#425).
    /// </remarks>
    private void Progressed(ComposeScreen compose, ComposeAttachment attachment, AttachmentState state)
    {
        if (Gone(compose) || !compose.Media.Progressed(attachment, state))
        {
            return;
        }

        Reach.Changed();

        // A description written while it was going up goes now there is an attachment to put it on (#377).
        if (state is AttachmentState.Ready)
        {
            _ = _attachments.Describe(Reach.Profile, compose, attachment);
        }

        Waited(compose);
    }

    /// <summary>
    ///     The send <paramref name="compose" /> was waiting on, where it no longer has anything unfinished to wait on —
    ///     an upload, or a description on its way (ADR-0026, #375, #377).
    /// </summary>
    /// <remarks>
    ///     Only from the screen it was asked on, as any send is. Where a screen is open over it — the file browser, a
    ///     description being edited — the send stays asked for, and goes once compose is back in front
    ///     (<see cref="Uncovered" />): dropped there, it was neither sent nor said (review of #372).
    /// </remarks>
    private void Waited(ComposeScreen compose)
    {
        if (ReferenceEquals(_waitingToSend, compose) && !compose.Media.Unfinished && ReferenceEquals(_stack.Top, compose))
        {
            _waitingToSend = null;

            // One that was refused meanwhile is said rather than sent, by the same rule as a send asked for then.
            _ = Send();
        }
    }

    /// <summary>
    ///     Whether <paramref name="compose" /> is no longer on the stack — taken off, its uploads let go — so that a report
    ///     still landing for it changes nothing, says nothing and redraws nothing (#425).
    /// </summary>
    private bool Gone(ComposeScreen compose) => !_stack.Screens.Any(screen => ReferenceEquals(screen, compose));

    /// <summary>That a send is waiting on <paramref name="unfinished" /> attachments, and how to call it off.</summary>
    private void SayWaiting(int unfinished) => Reach.Say(
        unfinished == 1
            ? "Will send once 1 attachment finishes — esc to stop."
            : $"Will send once {unfinished} attachments finish — esc to stop.",
        isError: false);

    /// <summary>
    ///     The instance took <paramref name="description" /> for <paramref name="attachment" /> (#377): said so on
    ///     <paramref name="compose" />, and sent again where it has changed meanwhile — and, where that was the last thing
    ///     a waiting send was waiting on, the send.
    /// </summary>
    private void Told(ComposeScreen compose, ComposeAttachment attachment, string description)
    {
        if (Gone(compose))
        {
            return;
        }

        if (compose.Media.Told(attachment, description))
        {
            Reach.Changed();
        }

        _ = _attachments.Describe(Reach.Profile, compose, attachment);

        Waited(compose);
    }

    /// <summary>
    ///     A description did not reach the instance, for the reason <paramref name="why" /> says: said on the status row,
    ///     and a send waiting on it stopped rather than left waiting. It is not sent again until the author sends again
    ///     (ADR-0006).
    /// </summary>
    private void Untold(ComposeScreen compose, ComposeAttachment attachment, string why)
    {
        if (Gone(compose))
        {
            return;
        }

        if (ReferenceEquals(_waitingToSend, compose))
        {
            _waitingToSend = null;
        }

        Reach.Say($"The description of {attachment.Name} was refused: {why}", isError: true);
    }

    /// <summary>An arm of <see cref="Do" /> that answers at once, as a used key.</summary>
    private static Drafted Ran(Action verb)
    {
        verb();

        return Drafted.Used;
    }

    /// <summary>
    ///     Opens a compose screen for <paramref name="purpose" /> on what the screen on top has picked out: a fresh post,
    ///     or one to a mention picked out; a reply to the post picked; or an edit of it, where it is the profile's own.
    /// </summary>
    private void Compose(ComposeFor purpose)
    {
        var screen = _stack.Top;
        var about = screen.Picked?.Boosted ?? screen.Picked;

        // A mention picked out is an account the reader walked to, so c writes to them — a fresh post rather than a
        // reply, since what they picked is somebody named in the post rather than the post itself (#85).
        if (purpose == ComposeFor.Post && screen.MentionedAs is { } handle)
        {
            Composing(new ComposeScreen(
                purpose,
                addressing: $"@{handle}",
                from: ComposeFrom.Of(Reach.Profile),
                starting: StartingDefaults(purpose, about: null)));

            return;
        }

        switch (purpose)
        {
            case ComposeFor.Reply or ComposeFor.Edit when about is null:
                return;
            case ComposeFor.Edit when !IsMine(about!):
                Reach.Say("Only your own posts can be edited.", isError: true);

                return;
        }

        Composing(new ComposeScreen(
            purpose,
            purpose == ComposeFor.Post ? null : about,
            purpose == ComposeFor.Reply ? Addressed(about!) : null,
            aboutIsMine: purpose == ComposeFor.Reply && IsMine(about!),
            from: ComposeFrom.Of(Reach.Profile),
            starting: StartingDefaults(purpose, about)));
    }

    /// <summary>
    ///     What a compose screen's To and Lang start on, which is what would go out if nobody touched them (ADR-0024): the
    ///     config's <c>default_visibility</c> and <c>default_language</c>, else the account's own as its instance said
    ///     them (#339), else nothing known. On a reply the visibility is the narrower of that and the post being
    ///     answered, which is where a reply with no visibility of its own goes out anyway (#338); the language is not,
    ///     since a reply is in its author's language rather than the answered post's (#340). An edit opens on the post's
    ///     own, which is the screen's to say.
    /// </summary>
    private PostDefaults StartingDefaults(ComposeFor purpose, Post? about)
    {
        var preferred = _preferred.Or(_defaults.For(Reach.Profile));

        return purpose == ComposeFor.Reply && about is { } answered
            ? preferred with
            {
                Visibility = PostAudience.Narrower(preferred.Visibility ?? answered.Visibility, answered.Visibility),
            }
            : preferred;
    }

    /// <summary>
    ///     Reads the own account's public profile as written and puts the form that edits it up over the account it
    ///     was asked from (#406), measured against the instance's own limit on a bio as far as that is known. Read fresh
    ///     every time, and never prefilled from the account on screen, whose bio is the flattened one (ADR-0028); a read
    ///     that fails opens nothing, and the status row says why.
    /// </summary>
    private async Task EditPublicProfile()
    {
        if (_stack.Top is not AccountScreen { IsOwnAccount: true })
        {
            return;
        }

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var reach = Reach;
        var profile = reach.Profile;

        await reach.Put(
            ask => ask.Of(token => reach.Ports.PublicProfile.Read(profile, token)),
            ifStillHere: read => _stack.Push(new PublicProfileScreen(read, profile.Instance) { Limits = _limits.For(profile) }));
    }

    /// <summary>
    ///     <c>ctrl-s</c> on the edit public profile form: sends what the form says changed and nothing else
    ///     (<see cref="PublicProfileScreen.Outgoing" />, ADR-0028), then takes the form off, says so, and reports the
    ///     public profile saved so that whatever shows the account reads it again.
    /// </summary>
    /// <remarks>
    ///     Nothing is refused before it is sent: the limits are the instance's (ADR-0028). A refusal leaves the form as
    ///     it is, every edit in it, and the status row says why in the instance's words — the enquiry's, as every
    ///     failure is — until an edit spends it (<see cref="Edit{T}" />).
    /// </remarks>
    private async Task SavePublicProfile()
    {
        if (_stack.Top is not PublicProfileScreen form)
        {
            return;
        }

        var change = form.Outgoing;

        if (change.IsEmpty)
        {
            Reach.Say("Nothing has changed to save.", isError: false);

            return;
        }

        // Taken now rather than read when the call is made, which a rate-limit wait can put after a switch (#243).
        var reach = Reach;
        var profile = reach.Profile;

        const string Saved = "Public profile saved.";

        await reach.Put(
            ask => ask.Of(token => reach.Ports.PublicProfile.Save(profile, change, token)),
            eitherWay: saved =>
            {
                // Off the stack only where it is still on top: a form thrown away while the save was on its way has
                // gone already, and what is on top now is somewhere else the reader went.
                if (ReferenceEquals(_stack.Top, form))
                {
                    _stack.TakeOff(form);
                }

                Reach.Tell(new Change.PublicProfileSaved(saved));
                Reach.Say(Saved, isError: false);

                // The account the form was opened over, read again rather than patched with what the save answered:
                // its posts carry the display name too, and an account screen is one reading (#84, #229). Said again
                // once it lands, since a fresh copy takes down what was said over the old one.
                if (_stack.Top is AccountScreen { IsOwnAccount: true, Subject: { } account })
                {
                    _ = Reach.Again(account, landed: () => Reach.Say(Saved, isError: false));
                }
            });
    }

    /// <summary>
    ///     <c>⏎</c> or a click on the edit public profile form's avatar or header (#407): pushes the file browser over it
    ///     for that one picture, in the folder the file browser opens in for a compose (#376).
    /// </summary>
    private void ChoosePicture()
    {
        if (_stack.Top is PublicProfileScreen form && PublicProfilePicture.Of(form.Typing.Part) is { } picture)
        {
            _stack.Push(FileBrowserScreen.ForAPicture(LocalFiles.Listing(BrowsingFrom), picture));
        }
    }

    /// <summary>
    ///     <c>ctrl-v</c> or <c>alt-v</c> on the edit public profile form's avatar or header (#407): a picture on the
    ///     clipboard is written to a file of its own and taken as that picture, and of copied files the first that is a
    ///     picture is. Sent with the rest of the form when it is saved, and not before (ADR-0028).
    /// </summary>
    /// <remarks>
    ///     There is no text to paste on either row, so whatever else the clipboard holds is said on the status row, every
    ///     time and there and then, rather than left to a field's own paste as on compose — nothing to read the clipboard
    ///     with included, which compose says once a session because text still pastes there, and here nothing would.
    /// </remarks>
    private void PastePicture()
    {
        if (_stack.Top is not PublicProfileScreen { Typing.IsAPicture: true })
        {
            return;
        }

        switch (_clipboard.Read())
        {
            case Clipped.Picture(var png):
                var pasted = _pasted.Keep(png);

                _ = Edit<PublicProfileScreen>(form => form.Pick(pasted));

                break;

            case Clipped.Files(var paths)
                when paths.FirstOrDefault(path => File.Exists(path) && FileBrowserScreen.Pictures.Accepts(path)) is { } picture:
                _ = Edit<PublicProfileScreen>(form => form.Pick(picture));

                break;

            case Clipped.Files:
                Reach.Say("None of the copied files is a picture.", isError: true);

                break;

            case Clipped.NoTool(var why):
                Reach.Say(why, isError: false);

                break;

            default:
                Reach.Say("There is no picture on the clipboard.", isError: false);

                break;
        }
    }

    /// <summary>
    ///     Pushes <paramref name="compose" />, measured against its instance's own limit as far as that is known (#319).
    /// </summary>
    private void Composing(ComposeScreen compose)
    {
        compose.Limits = _limits.For(Reach.Profile);

        _stack.Push(compose);
    }

    /// <summary>
    ///     An instance said how long it lets a post be, which every compose on the stack going out on that instance is
    ///     measured against from now on, whatever covers it (#424) — and how long it lets a bio be, which every edit
    ///     public profile form on the stack whose profile is on that instance counts against, likewise (#422). One
    ///     redraw, however many it reached.
    /// </summary>
    private void Measured(string instance, PostLimits limits)
    {
        if (_reach() is not { } reach)
        {
            return;
        }

        var reached = false;

        foreach (var compose in _stack.Screens.OfType<ComposeScreen>().Where(compose => compose.GoesOutOn(instance)))
        {
            compose.Limits = limits;
            reached = true;
        }

        foreach (var form in _stack.Screens.OfType<PublicProfileScreen>().Where(form => form.GoesOutOn(instance)))
        {
            form.Limits = limits;
            reached = true;
        }

        if (reached)
        {
            reach.Changed();
        }
    }

    /// <summary>
    ///     What a reply has to be written to, or <see langword="null" /> where there is nobody left to name. Mastodon
    ///     routes by the handles it parses out of a post's <em>text</em>: a direct post reaches the accounts its text
    ///     mentions and nobody else (ADR-0013), and on every other visibility <c>in_reply_to_id</c> puts the reply in
    ///     the thread and notifies nobody at all. Either way a reply that named nobody would not reach the account it
    ///     answers, which is the one thing a reply is for (#130) — so every reply is addressed, and <c>dm send</c>
    ///     writes its mention for the same reason.
    /// </summary>
    /// <remarks>
    ///     The mention is written into the editor rather than added on the way out, so a reader who does not want to
    ///     ping somebody can delete the name before sending — and so this client never sends words the reader could
    ///     not see.
    ///     <para>
    ///         Who it goes to is the conversation where there is one, rather than whoever spoke last: a thread with
    ///         three accounts in it answered to only one of them is a reply that dropped the rest of the conversation.
    ///         Elsewhere it is the account being answered followed by everyone their post named, which is that same
    ///         conversation as the post itself carries it — a direct message included, since the accounts a direct post
    ///         names are the accounts its instance delivered it to (#132). The conversation still wins where there is
    ///         one on screen, being who is left in it rather than who one message in it happened to name.
    ///     </para>
    /// </remarks>
    private string? Addressed(Post about)
    {
        // An instance says who a conversation is with rather than who is having it, so the profile's own account is
        // already not among them. Everywhere else it is the post itself, whatever its visibility: the account being
        // answered and everyone their post named, off the post already in hand.
        IReadOnlyList<string> with = (about.Visibility, _stack.Top) switch
        {
            (PostVisibility.Direct, ConversationScreen conversation) => conversation.Conversation.With,
            _ => [about.Account, .. about.Mentions],
        };

        // An address this client cannot make sense of is left out rather than thrown over the reply, since a handle
        // an instance sent is not something the reader can do anything about. What is left is in the editor in front
        // of them, so a mention that is missing is missing where they can see it and type it themselves. The reader's
        // own account goes the same way: nobody is notified of their own reply, and a post answering a thread they are
        // in names them where the instance listed them.
        var accounts = with
            .Where(account => AccountAddress.IsWellFormed(account) && !Reach.IsMe(account))
            .Select(AccountAddress.Parse)
            .DistinctBy(account => account.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return accounts.Count == 0 ? null : DirectMessage.To(accounts, string.Empty);
    }

    /// <summary>
    ///     Whether a post is the profile's own, which is what settles whether it can be edited.
    /// </summary>
    /// <remarks>
    ///     Asked of the profile rather than read off <see cref="Post.IsMine" />, which is what the status row reads: both
    ///     are the one comparison <see cref="ActiveProfile.SignsInAs" /> makes, so the row and the refusal cannot come
    ///     to disagree (#220).
    /// </remarks>
    private bool IsMine(Post post) => Reach.IsMe(post.Account);
}
