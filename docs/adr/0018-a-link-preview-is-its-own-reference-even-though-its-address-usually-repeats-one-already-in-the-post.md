# A link preview is its own Reference, even though its address usually repeats one already in the post

Mastodon's `card` is server-generated metadata about a URL already sitting inside a post's own text — a title, a site
name, sometimes an image — not something the author attached (CONTEXT.md's **Attachment** is deliberately "besides its
text"; this is about it). So it lands as `Post.LinkPreview`, a new nullable field alongside `Media` and `Poll` rather
than a `MediaKind`, carrying only what a terminal can use: `Url`, `Title`, `Description`, `ProviderName`, `Image`, and
the author's name as plain text. `Type`, `Html`, `EmbedUrl`, `Width`, `Height`, `Blurhash`, `AuthorUrl` and
`PublishedAt` are dropped — the wire's `photo`/`video`/`rich` types exist to embed an iframe player, which nothing here
can render, and an author's own address is left unlinked on purpose (below).

**It is its own `Reference` anyway — walked with `←`/`→`, opened with `⏎` — even though its `Url` will usually be the
same address a `Link` reference already reaches inside the post's flattened text.** The alternative, considered
directly: render it as pure enrichment with no reference of its own, on the reasoning ADR-0017 already used to
*exclude* a still picture from the walk — "already the whole of itself," nothing left to open. That reasoning doesn't
transfer cleanly here. A picture drawn in place *is* the whole of the thing; a link preview's title and description are
not the article, only a pointer to it, and a reader who wants the article still needs something to press `⏎` on.
Leaving that to "walk through the text until you find the matching link" breaks down on a long post, or one where the
link's display text was elided. So the redundancy is accepted deliberately: it is the *address* that repeats, not the
thing offered, and every other attachment on this client already earns its own reference the same way — consistency
with that pattern outweighs one duplicate `⏎` target on the rare post where a reader tries both.

The author's name is shown as plain byline-style text, the same way `Post.Author` already is, and does not become a
third openable address on the post. Two things opening the same place was already the trade-off just made for the
preview's own `Url`; a second address that usually differs from it, and rarely matters enough to open, is where
"consistency with attachments" stops being the stronger argument.

**A link preview renders after a post's attachments, and is gated behind `IsWarned` exactly as an attachment is
(#113's amendment to ADR-0016).** Mastodon's docs don't state that `card` and `media_attachments` are mutually
exclusive, so a client can't assume only one is ever sent — text, then attachments, then link preview is the same
order Mastodon's own web UI uses, and costs nothing to get right now rather than as a surprise later. Its image, where
present, reuses `IPictures`/`PictureView` exactly as an attachment's does: same width-driven box, same cap, same
"linked, not drawn" fallback on a terminal or CLI that can't draw one.

Both surfaces grow accordingly: the CLI prints a link preview on every post it shows (title, provider, description,
address), the same reasoning ADR-0016 gave for printing an attachment's address on every post — the preview carries
information the raw text doesn't. `--json` grows a `linkPreview` object for the same parity reason it grew `media`.
