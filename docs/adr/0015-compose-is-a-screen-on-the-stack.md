# Compose is a screen on the stack, not a region under the feed

ADR-0014 settled the shell and left three things open. This settles the second of them: **where compose lives**. The
choice was between an editor pushed onto the stack like any other screen, and a region that opens under the feed so
that the thing being replied to stays visible.

**Compose is a screen.** `c`, `r` and `e` push one, `esc` pops it and throws away what was written, `ctrl-s` sends it
and pops. The breadcrumb says which of the three it is — `Home › Post by @ben › Reply to @ben@hachyderm.io` — the same
way it says where everything else is.

The case for the split region is real and it is one thing: you can see what you are answering while you answer it. The
case against it is three.

The first is that it is a second layout, and the shell has one. Every other screen in this client — the feed, a post,
an account, and the six that #29 and #30 bring — is the whole content region, reached by pushing and left by popping.
A region that opens under the feed is a second arrangement to build, to keep readable at 61 columns, and to explain,
and it would be the only thing on screen that is not somewhere you went.

The second is that it is the arrangement that breaks at 80 columns, which is the width ADR-0014 rejected the
right-hand context pane over. An editor sharing the content region with the feed leaves each of them half of 24 rows.
Six rows of timeline is not enough timeline to be worth keeping, and six rows of editor is not enough editor.

The third is that what the split region is for can be had without it. A reply screen draws what it is answering at the
top — the handle, and the first three rows of what they said — and then the editor underneath. That is the part of the
context that was worth keeping, it costs four rows rather than half the screen, and it is not a layout.

## What this costs

You cannot scroll the timeline while composing. That is the honest price, and it is the same price the CLI pays, where
`post reply` takes an id and the timeline is not on screen at all. If it turns out to matter, the thing to add is a key
that shows more of what is being answered, not a second layout.

## What it settles for later tickets

Every screen that takes text — a search prompt (#29), a direct message (#30) — is a screen on the stack, entered the
same way and left the same way. There is one arrangement in this shell and the answer to "where does this new thing
go" is always the same.

## Amendment: the label line's wording (map #61, ticket #73)

The three rows still stand — nothing since this ADR has made leaving the compose screen any cheaper, so "seeing
enough of what you're answering to answer it accurately without leaving the screen" still holds. Only the label
above them changes, to match the wording a feed's own reply mark settled on (`docs/tui-shell.md`, "What a post's
byline settled"): `Answering @handle:` becomes `↳ answering @handle` (no trailing colon), or `↳ continuing` for a
self-reply. Compose always holds the full post it answers, so the bare `↳ reply` variant a feed sometimes falls back
to never applies here. The row count is unchanged, and so is everything below the label.

### What changing the wording turned up

The block was never on screen. "A reply screen draws what it is answering at the top ... and then the editor
underneath" is what this ADR has said since it was written, and the rows were being painted — but the editor is a
separate view laid over the region they are painted on, starting at the same row and opaque, so it covered every one
of them on every frame. Nobody had noticed, because the only way to see the block is to reply to something and the
only thing the block does is be seen.

So the editor now starts below it rather than on top of it. That is a layout change this ticket did not ask for, and
it is the change that makes the ticket's own acceptance — the label reading `↳ answering @handle` — mean anything at
all. It costs the editor up to five rows: this ADR priced the split region at "six rows of editor is not enough
editor", and five off a 24-row terminal leaves eighteen, so the price the split region was rejected over is not being
paid here. Below that the block gives way instead — the editor keeps three rows whatever a terminal's height, since
an editor pushed off the foot is worse than a truncated quote of what is being answered.

The block also stops scrolling, which is "What this costs" above finally being true rather than merely intended. The
region it is painted on is the one the arrows scroll, and it was left scrolling under the editor: everything below
the block is behind the editor, so a scroll could only lift the block off the top and leave rows in its place that
are the middle of something with no way to see the rest. It did not come back, either — a compose screen has nothing
picked out on it, so the scroll never corrects itself. Composing now turns the region's scrolling off outright, which
also pins it back to the top.

## Amendment: a compose screen holds two fields, not one (map #61, tickets #123, #139, #140 and #142)

A post being written carries a **content warning** as well as its text: one row above the editor, pre-filled on a
reply from the post being answered (#123) and on an edit from the post being changed (#140), and empty on a fresh post
(#139). This ADR settled where compose lives and priced the editor's share of the screen; a second field is a claim on
that share, so it is recorded here.

**It costs two rows on every compose screen** — the field, and the blank above it — of which the reply block already
paid one. That blank used to be the block's own trailing row, and moving it onto the warning is what puts it on the
two screens that have no block at all (#143): hung off the block it appeared on a reply and nowhere else, so the one
row all three screens have in common was the row they spaced differently. A reply reads label, quote, blank, warning,
editor and is one row deeper than before; a compose and an edit read blank, warning, editor and are two.

That is inside the price already accepted above — the block is up to four rows, and 24 minus six is still more editor
than the split region this ADR rejected would have left. Below that the reply block gives way first: the warning is a
row the reader types into, and one they cannot see is worse than a quote of what is being answered that stops early.

Every screen, including — while there was one — the screen with no field to put there. An edit held both rows blank
(#142), which is against the habit that a part with nothing in it is skipped rather than spaced, and was the exception
that earned it: the alternative is an editor that starts higher depending on which key opened it. Three screens whose
only difference is what they are for should not differ in where the writing begins. #140 gave the edit its own field
and the band was already the right height, which is what the row was being held for.

**It is not a second layout.** No region opens, nothing shares the content region with the feed, and the screen is
still one thing pushed on the stack and popped by `esc`. The row is painted where the "answering" block is painted,
and the editor starts below it exactly as it starts below the block.

**`ctrl-w` moves the typing between the two.** A terminal editor takes the keys of whichever field has them, so while
the warning has them the editor gives up focus and keeps its text, and every printable key goes into the field. That
is the rule the search prompt already keeps for `/` and `?`, and the status row says which way `ctrl-w` goes next.

**All three, `e` included.** Changing a warning already published has a third state — leave it alone — which `PostEdit`
carries and which a field that opens *empty* cannot say. That was read once as a reason to keep the field off an edit,
and it was too strong: it is a fact about an empty field rather than about fields. A field opening on the post's own
warning says all three by construction — left alone it sends the same warning back, cleared it sends empty and the
warning comes off, typed into where the post had none it puts one on — so the TUI always sets
`PostEdit.ContentWarning` and `ChangesContentWarning` is always true from this surface (#140). The third state is not
thereby dead: it is the CLI's, where `--cw` can be absent from the command line. A field the author is
looking at has no such state, because they saw the row and whatever it holds is what they want.

## Amendment: the screen says what goes out, and the shell sends it (ticket #146)

The amendment above gave compose a second field and left the *assembly* where it had always been — in `Shell.Send`,
which built a `PostDraft` or a `PostEdit` out of the screen's members. So one decision, what goes out when `ctrl-s` is
pressed, was spread across two modules, and the shell was the module that had to know which of the compose screen's
two warning members belonged to which purpose: the raw field on an edit, where an empty one means *take the warning
away*, and the trimmed one on a publish, where an empty one means *no warning at all*. That difference is real and
this ADR's third amendment is what makes it real — but it took a nine-line comment to explain, sitting in the one
module that should never have had to care. Five commits in a row touched both files to change one rule about one
field.

**A compose screen answers what goes out, as a value.** `ComposeScreen.Outgoing` is an `Outgoing`: either
`Publishing` a `PostDraft` or `Saving` a `PostEdit` against the id of the post being changed. `Shell.Send` refuses an
empty compose, makes the one call the value names, pops the stack, and says `Sent.` or `Saved.` — the parts that
genuinely need a port and a stack. It constructs neither a draft nor an edit, and the comment explaining raw versus
trimmed has moved to the two private methods that assemble them, a dozen lines apart from each other in the file that
holds the field.

**The field is still readable and no longer writable.** `ContentWarning` — the trimmed reading — is gone from the
screen's surface entirely, since it existed only for whoever was assembling the draft. `Warning` stays, because it is
the row on screen and a fact about the screen worth asserting, but its setter is private: what a keystroke changes is
the screen's own, and with one warning member left there is no longer a pair for anybody outside to choose the wrong
one of. That is what the split cost — not the field being visible, but two of them being visible to a module with no
way to tell which was which.

**A value, not a call.** This ADR's rule — a screen reaches no port and knows about no instance — is what makes every
screen drawable and assertable with no terminal and no network, and it is untouched: `Outgoing` is inert data handed
to whoever is doing the sending. That is also what lets the compose tests read what would go out directly instead of
standing a fake author at the port to catch it, which is how they used to have to ask.

**The whitespace rule collapses into `Wooly.Core`.** "A warning of nothing but spaces amounts to no warning" was
written out three times — the TUI's field, the CLI's `--cw`, and `PostEdit.ContentWarningWanted`. It is
`ContentWarnings.Written` now, and all three read it. Only the *reading* is shared: what none then looks like on the
way out stays with the thing being sent, since it differs on purpose (null on a `PostDraft`, the empty string on a
`PostEdit`, which keeps null for its third state). The CLI's `--cw` keeps that third state whole — it is the option
being absent from the command line, which is a fact about the invocation and not about what was written in the option,
so it never was the same question as whitespace.

### What this leaves where it is

`Shell.Addressed` — who a reply names, which is 45 lines of Mastodon reply-routing — stays in the shell. It was worth
asking about, being a decision about a post rather than about the shell, but it belongs to *opening* a compose screen
rather than to what leaves one: it runs before the screen exists, and it reads both the profile's own account and the
`ConversationScreen` the reader is standing on. Moving it would be a second decision in a ticket about the first one,
and the shape #146 names in passing — nineteen guards naming a concrete screen — is a map-level question rather than
this ticket's.

## Amendment: the screen is a block of headers over the post (map #313, ticket #317)

Compose is laid out as a mail client's compose — variant A of the prototype — on all three screens: a blank, a block
of right-aligned headers, a hairline, a blank, the editor, and a hairline and the row the count sits on (#319) at the
foot, two columns in from either side. The headers are **From** (the profile's handle and its instance), the reply
header on a reply (labelled with the feed's `↳`, worded by `PostReplyName`, and the three-row quote under it), and the
warning (labelled with the bare `⚠`). That moves what the second amendment above settled, so it is recorded here.

**It costs more rows than the second amendment priced, and it is still inside the price this ADR accepted.** That
amendment put two rows on every compose screen — the warning and a blank above it — and read a reply as label, quote,
blank, warning, editor. Now a fresh post and an edit spend five rows above the editor and two below it, and a reply
with a full quote spends nine above. On a 24-row terminal that leaves the editor fourteen rows on a post and ten on
the deepest reply. The split region this ADR rejected left six. The blank #143 put above the warning is the blank
above the whole block now, still on every compose screen alike, and the warning header follows the quote directly, as
one header follows another.

**The writing still begins in the same place whichever key opened the screen.** A fresh post and an edit carry the
same headers, so the editor starts at the same row on `c` and `e`. Only a reply is deeper, by its own header and
quote. That was the reason #142 held the warning's rows blank on an edit, and it holds without anything held blank.

**On a terminal too short for everything, rows give way in a fixed order:** the quote's tail first, as before, then
the blanks, the reply header, the foot, From and the hairline under the headers. The warning header and three rows of
editor are never given up — the rule the second amendment kept for the warning, with more dressing in front of it to
go first. To keep what is painted and where the editor is laid in step, the screen works out both from one layout at
the content region's height, rather than the window working the editor's place out from the rows.

**It is still not a second layout.** Everything above is rows painted on the content region with the editor laid over
the rows kept for it. Nothing opens beside the feed, and the screen is still pushed by `c`, `r` or `e` and popped by
`esc`.

**What it leaves to its own tickets.** The warning is still painted text the shell types letters into. Making it a
field of its own, and changing how `ctrl-w` moves the typing, is #320's amendment.

## Amendment: the content warning is a field of its own (map #313, ticket #320)

The warning was painted text the shell typed letters into: `ComposeScreen` answered `IsTyping` while `ctrl-w` had the
typing, the window's typing path carried each printable key to `Type` and `Backspace`, and a `▌` painted on the row
stood for the caret. It could not be selected, a caret could not be moved through it, it took no paste of its own and
no click. It is now a one-line text field laid over the warning header's value column, the way the editor is laid over
the body, so it edits exactly as the post does. That changes what the second amendment above said about how `ctrl-w`
moves the typing, so it is recorded here.

**The field takes its own keys, and the same keys off the widget the editor does.** `esc` throws the draft away,
`ctrl-s` sends, `ctrl-w` hands the typing back — and `enter` hands it back too, since finishing a one-line field is
finishing it. A right click does nothing, as on the editor (#307). Every other key is the field's before it bubbles to
the window, so `?`, `/` and `c` are letters in it without a rule of the shell's: the search prompt's rule is no longer
what keeps them out of the keymap. A compose screen never answers `IsTyping`, and the shell's `Type`, `Backspace` and
`Paste` carry nothing into it.

**`ctrl-w` and where the typing is stay one fact.** `ctrl-w` toggles `WritingTheWarning` and the window focuses
whichever field it names. A click that focuses either field toggles it to match, so the status row's `ctrl-w` keeps
saying which way it goes next however the typing got there. Both fields can take focus at all times: the editor no
longer gives up `CanFocus` while the warning is being written.

**The screen still decides what goes out.** It learns the field's text through `RewriteWarning`, as it learns the
editor's through `Text` — every change, so the count reads it. `Warning`'s setter stays private, which the third
amendment made it to keep the field's two readings the screen's own, and `Outgoing` is unchanged. The screen still
paints the warning row whole, the field's text or its hint under the field, so the row reads the same to a test with no
terminal behind it; the screen says where the field goes (`WarningAt`) from the one layout that paints the row, as it
does for the editor.

## Amendment: To says who the post goes to (map #333, ticket #338)

ADR-0024 puts the post's visibility on compose as a **To** header directly under From, which changes the layout #317
settled above and the walk #337 gave the arrows, so it is recorded here.

**The headers are From, To, the reply header and its quote, then ⚠.** To is a row of radio buttons, one per
visibility, named beside its bubble: `● public  ○ unlisted  ○ followers  ○ direct`. It costs one row on all three
screens, so a fresh post and an edit spend six rows above the editor and a reply with a full quote ten — on a 24-row
terminal still more editor than the split region this ADR rejected. The writing still begins in the same place on `c`
and `e`, which carry the same headers.

**To is never given up, with the warning.** The give-way order on a short terminal is the quote's tail, the blanks,
the reply header, the foot, From, then the hairline under the headers; To, the warning and three rows of editor are
kept whatever the height. Where the value column is too narrow for the row, To falls back to the one value chosen
with an arrow either side, `◂ ● followers ▸`, rather than being cut.

**To is a field the arrows walk, and the screen still says what goes out.** It is laid over its value column as the
warning field is, and joins the walk where it is drawn: `↑` from the post goes to the warning, then To; `↓` comes
back. `←`/`→` there choose, and the keymap rather than the widget says so; letters on To are nobody's. A click on a
value chooses it and gives To the typing. Which values To allows is one fact the screen holds — all four on a fresh
post, those no wider than the post being answered on a reply, none on an edit — and the dimming, the keys and the
clicks all read it, so an edit's To takes neither keys, clicks nor the typing. `Outgoing` sends the visibility To
shows, marked chosen only where the author moved it off what it opened on, so a starting preference too wide for a
reply is narrowed by `PostAuthor` exactly as on the CLI. Where nothing is known — no `default_visibility` on a fresh
post — To reads `account default` and sends nothing, as compose did before.

**The window's typing follows one field, not a toggle.** A click into any of the three fields moves the screen's
`Typing` to that field, where it used to toggle `ctrl-w`'s state; `ctrl-w` keeps its meaning, a jump into the
warning and back.


## Amendment: Media under Warn, and every header labelled with a word (map #372, ticket #375)

**The headers are From, To, Lang, the reply header and its quote, Warn, then Media**, each labelled with a word
right-aligned in a column five wide — five for `Media`. `Warn` replaces the feed's `⚠` as the warning row's label on
all three screens, so the block reads as one. Media and Warn share a format: what the header holds, then the key that
adds to it, all muted — `Media  none · ctrl-o to add`, then `2 of 4 · ctrl-o to add`, and `4 of 4` once full.

**A fresh post and a reply carry a Media header; an edit does not yet.** Its rows are pending attachments, one each,
in the layout #374 chose. That costs a fresh post and a reply one row more than an edit until the edit shows its own
attachments under a read-only Media header of its own (#381), when `c` and `e` start the writing in the same place
again.

**The rows fold before anything gives way.** Where a row each would leave the editor fewer than three, they fold into
the Media header's line — `3 of 4 · 3 no alt text · 1 failed · ctrl-o to add` — and unfold when there is room. Only
then does the give-way order run: the quote's tail, the blanks, the reply header, the foot, From and Lang, the Media
header, the hairline under the headers. To, the warning and three rows of editor are still kept whatever the height.

**The screen stays inert.** It holds what is attached and where each has got to, and says what goes out — a draft
naming the pending attachments by id, asked for only once every one is ready. The shell sends each file up as it is
attached and feeds its progress back (ADR-0026), and `Shell.Send` waits on them before making the one call.

## Amendment: an edit lists what the post carries under Media (map #372, ticket #381)

**An edit has a Media header too, read-only.** It lists the attachments the post already carries, a row each — its
kind, then its description in quotes or the quiet `no alt text` — under `Media  2 · kept as they are`, or
`Media  none` where the post carries nothing. There is no key on it and nothing on its rows to remove or reorder by:
changing an edit's attachments would reopen ADR-0008's carry-through, which is out of scope, and the edit still saves
the text, warning and language alone. The header is there so that `c` and `e` start the writing in the same place
again, as the previous amendment promised: with the same headers and as many rows under Media, the editor starts on
the same row on all three screens. The rows fold into the header's line on a short terminal as a fresh post's do.
