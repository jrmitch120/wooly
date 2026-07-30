# Composing is one call, an edit preserves what it was not asked to change, and deleting asks only where somebody can answer

Post authoring (#22) is the first feature that writes rather than reads, and five decisions fall out of that. Every later
write inherits them — boosting, favoriting, following, sending a direct message.

**Authoring is one narrow port, and attaching media is part of publishing rather than a step before it.**
`IPostAuthor` publishes, edits and deletes; it is ADR-0005's port over Mastonet, the counterpart to `ITimelineReader`,
and it is what front-end tests fake. Uploading lives inside `Publish`: a caller hands over a draft naming file paths, and
the adapter uploads each file and publishes the post carrying the ids. The alternative — an `Upload` call returning ids
for the caller to pass back in — would put the same two-step dance in the CLI, the TUI, and every later thing that
attaches a file, and each copy would get the failure case differently. Here there is one: every path is checked before
the first byte goes up, so a post with three attachments and a typo in the third publishes nothing at all rather than
leaving an author with uploads they cannot see and a post they cannot take back. Files go up one at a time, in the order
the author gave them, because an attachment's place on a post is part of what was composed.

**An edit preserves what it was not asked to change, which takes a read as well as a write.** Mastodon's
`PUT /api/v1/statuses/:id` does not amend a post, it replaces one: attachments the request leaves out are dropped, and so
is a content warning it leaves out. So `Edit` reads the post first and carries its attachments through, and carries its
warning through unless the edit says otherwise. Without that, `post edit <id> "fixed the typo"` would be a way to lose a
photograph, and — worse — a way to show a reader exactly what a warning had been put there to hide. Saying so takes three
states out of one field: `--cw <TEXT>` replaces the warning, `--cw ""` removes it, and no `--cw` at all leaves it. Silence
has to mean "leave it" rather than "remove it" for the reason just given.

A poll cannot be carried through at all, and that is the one thing this client refuses to do rather than do badly. An
edit that omitted the poll would delete it and every vote in it; one that resent it would restart the voting. So a post
carrying a poll is refused (`UneditablePostException`) with the suggestion to delete and republish, which loses nothing
silently. Reopening this would mean deciding which of those two losses is acceptable — not adding a flag for both.

**Visibility unsaid stays unsaid.** A draft's `Visibility` is nullable and a null sends no `visibility` parameter, leaving
the choice to the account's own default on the instance. Defaulting to public here would publish an account whose own
default is followers-only wider than it asked for, which is not a mistake its author can take back. Where the command
line says nothing, the `default_visibility` preference in the config file is consulted first — the key already existed
with nothing reading it, and this is what it was for. A published post's actual visibility is read back off the post and
reported, because for a draft that left the choice open it is the only place the answer exists. That is what `Post`
gained a `Visibility` field for; it is additive to ADR-0007's `--json` contract, and a timeline's posts now carry it too.

**Deleting asks first, but only where there is somebody to ask.** There is no undoing it, so a person at a terminal is
asked to confirm and `--yes` is how they skip it. Under a pipe or in CI there is nobody to prompt and nobody to read the
prompt, so the command goes ahead: typing an id is that invocation's consent, and stopping to ask would make the command
unusable in the automation this CLI exists for. The port itself never asks — only a front end knows whether anyone is
there.

**A single post's `--json` is the post, not an envelope.** ADR-0007 wraps a timeline in an object because a fetch can be
partial and `[]` cannot say so. Publishing has no partial: it either happened, in which case there is a post with an id,
or it threw, in which case ADR-0006's handler reports it and the exit code says which failure. So `post create --json`
writes the post object itself. Its fields are the same `PostDocument` a timeline's posts are written as — one spelling,
so that `timeline home --json` and `post create --json` can never describe the same post differently.

One thing this deliberately does not touch: **a publish is never retried.** ADR-0006 already settled that no HTTP response
is retried because resending could publish a post twice, and this is the feature that decision was written for. Nothing
here adds a retry, and nothing here waits out a rate limit.

## Consequences

Where a spelling lives now has a rule worth stating, because this ticket added several. A spelling more than one entry
point needs lives in the core layer next to the value it spells — `PostVisibilityName` is asked by both the
`--visibility` flag and the config file's `default_visibility` key, so a word the command line accepts can never be one
the file turns down. A spelling only a command line needs lives in `Wooly.Cli.Options` — `--media <path>[:<alt text>]`
and `--poll-open 6h` are both answers to "a command line is one string", and a TUI composing a post has a field for the
path and a field for the description with no character between them.

`--media`'s colon cannot describe a file whose own name contains one. A Windows drive letter's colon is stepped over, and
everything after the next colon is alt text — which is unambiguous, predictable, and wrong for `my:file.png` on Linux. The
alternatives considered were splitting on the last colon (which breaks `C:\pics\cat.png` with no alt text) and probing the
file system to decide which colon separates (which makes the parse depend on what happens to exist). A user with such a
file can rename it or attach it without alt text; if that turns out to matter, an escape (`\:`) is the smallest way out.

`PostPoll` deliberately refuses to know an instance's limits — the most answers allowed, the longest a poll may stay open
— and lets the instance answer in its own words. The rules it does hold are the ones true of every instance: at least two
answers, each with something written in it, all different, open for some length of time.
