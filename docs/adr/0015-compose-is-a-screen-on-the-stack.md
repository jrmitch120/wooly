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
