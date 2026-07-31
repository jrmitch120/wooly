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
