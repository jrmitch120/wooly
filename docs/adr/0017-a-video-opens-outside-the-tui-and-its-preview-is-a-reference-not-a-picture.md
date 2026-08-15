# A video opens outside the TUI, and its preview is a reference, not a picture

ADR-0016 drew a hard line: only a still picture is drawn, and a video, an animation and a sound are linked, on the CLI
and the TUI alike, because a reader must never be shown a broken or misleading inline rendering — a frozen frame with
nothing to say it was meant to move is exactly that. Adding video *playback* to the TUI runs straight into that line,
and this ADR is the record of what was actually decided once it was pushed on: not to draw motion at all, and to
change, carefully, what "linked" gets to mean now that the TUI can act on a link and the CLI still cannot.

## Playback is handed to the reader's own player; nothing in this process decodes a frame of it

Three shapes were on the table. Decode the video in-process and draw its frames through the same sixel/Kitty path
`PictureView` already uses for a still picture. Shell out to `mpv --vo=kitty`, positioned over the box a post reserves,
so an external process paints the frames but the placement still looks inline. Or hand the attachment's address to
whatever the reader's system already opens video with — the same `SystemWebBrowser`/`BrowserLaunch` call a `Link`
reference already makes (`PostKeys.cs`, "an address in the platform's browser") — and decode nothing at all.

The first was never really available. There is no fully-managed .NET video decoder — the maintained options
(`FFMediaToolkit`, `H264Sharp`) wrap native FFmpeg or OpenH264, and the one dependency-free project found had an
incomplete H.264 decoder. `SixLabors.ImageSharp` was chosen for the picture path specifically *because* it is fully
managed and keeps a self-contained build to one artifact per platform (#32, ADR-0016); a native video codec dependency
would give that back for every platform this client ships.

The second was prototyped, not just reasoned about, against a real Mastodon attachment. `mpv --vo=kitty` streamed the
URL directly — no separate download, real h264/aac decode — and its `--input-ipc-server` socket answered `set_property`
on `vo-kitty-top`/`vo-kitty-left` with a genuine live reconfigure, confirmed in its own log, which means the
positioning half of "looks inline" really does work. But three things it does not do killed it. It does not check
whether the terminal understands the Kitty protocol before writing to it — confirmed by watching it happily emit
Kitty transmission bytes into a plain `xterm-256color` pty — so it has to be gated behind `RasterProtocol`'s own
detection or a reader on a terminal that cannot draw gets audio playing over nothing, which is worse than a link. Its
own aspect-fit does not follow `Inset.For`'s "full width, height follows, capped" rule — handed a 40×12 box for a
720×1280 clip it letterboxed down to a 6-column strip, so Wooly would still have to compute the box itself. And it
costs real CPU — about 30% of a core on this machine for one small embedded clip — for a process outside Wooly's own
concurrency limits (`Pictures.AtATime`). Worse, it is a *second* writer to a terminal whose every other invariant
(`PaintedView`'s release-before-place ordering, `OnClearingViewport`'s timing) exists because there is supposed to be
exactly one. Coordinating a second one — repositioning it on scroll, confirming its Kitty placement is actually
deleted on `quit` rather than merely stopped-being-written-to — is re-fighting the stuck-placement problem ADR-0016
already spent most of its effort on, against a process this codebase does not control the draw loop of.

The third needed nothing new. `SystemWebBrowser` already exists, already opens an arbitrary address in whatever the
OS associates with it, and already carries no dependency this project does not have today. It draws nothing and knows
nothing about codecs, which is exactly why it cannot get any of the three things above wrong.

## An attachment's address becomes a `Reference`, walked and opened the way a link already is

Once "open externally" is the action, the natural place to hang it is the mechanism that already does exactly this
for a `Link` inside a post's text: `←`/`→` walks to it, `⏎` opens it. But `Reference` was defined narrowly on purpose
— "found inside a post's text" — because an attachment is rendered on a separate path (`PostLines.Media`, not
`BodyText`) and was never part of what that walk reached.

It was widened anyway (CONTEXT.md, **Reference**). The alternative — a second, parallel walk/pick/open mechanism for
attachments alone — would have meant solving "which of several things on this post is picked right now" a second
time, including its edge cases (bounds, clearing the pick when the post changes, the bracket that marks it,
`PostKeys.OnAReference`'s hint row), rather than reusing the one solve that already exists and is already exercised.
The definition's boundary was deliberate; widening it was judged the smaller cost against duplicating tested
machinery. `Image`, `Audio` and `Unknown` are unaffected by the walk in one respect each: `Image` is drawn and never
opened this way; a picked attachment reference opens through the same `SystemWebBrowser.Open` a `Link` already calls,
so `⏎` keeps meaning one thing regardless of which of the four kinds it lands on.

## `Video` and `Animation` gain a preview, marked so it is never mistaken for a photograph

`PostMedia.Preview` was already populated for every kind straight off the wire (`PostWire.ToMedia`) — nothing was
ever `Image`-only about the data, only about `IsDrawable`'s one-line gate. Loosening that gate to include `Video` and
`Animation` reopens the exact sentence ADR-0016 rejected a preview frame on: a frozen frame with nothing to say it was
meant to move is a misleading rendering. What changed since is that there is now something to say it: a permanent,
stable, walkable label — the kind's own name (`MediaKindName.Written`, capitalized) — sitting where a picture's own
caption already sits, in the position ADR-0016 fixed specifically so a reader's eye is never disturbed by the picture
landing underneath it. Landing hides the description under it exactly the way a picture's caption already can
(`hideDrawnCaption`, #71); the label itself never does, because it is not describing the attachment anymore, it is
the thing `⏎` acts on. A reader is never shown a still frame standing in for the whole of a video — they are shown a
still frame standing beside the word that opens the whole of it.

`Audio` and `Unknown` were left out of the drawn half on purpose, even though `Audio` occasionally carries a
`Preview` too. Cover art is not a frozen frame of motion — ADR-0016's objection does not apply to it — but drawing it
does not earn a box either: it does not tell a reader anything the label and description do not already say, and
`Unknown` cannot promise a box means anything at all. Both stay label-plus-description, permanently, which needs no
special case: they simply never reach the "landed" state `Video`/`Animation` can.

## Consequences

No new dependency, managed or native: `SystemWebBrowser`, `IPictures`, `PictureView`, `Inset` and `Drawn` are all
reused exactly as they stand, with `IsDrawable`'s gate and a caption's composition the only things that change. The
CLI is untouched — it already links every kind uniformly (ADR-0016) and has no `⏎` to attach an action to. The mpv
prototype itself is not carried into the codebase; it answered a design question and is discarded once this ADR
records the answer.
