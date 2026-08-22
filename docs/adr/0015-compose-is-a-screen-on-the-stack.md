# Compose is a screen on the stack, not a region under the feed

ADR-0014 settled the shell and left three things open. This settles the second of them: **where compose lives**. The
choice was between an editor pushed onto the stack like any other screen, and a region that opens under the feed so
that the thing being replied to stays visible.

**Compose is a screen.** `c`, `r` and `e` push one, `esc` pops it and throws away what was written, `ctrl-s` sends it
and pops. The breadcrumb says which of the three it is — `home › post by @ben › reply to @ben@hachyderm.io` — the same
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
