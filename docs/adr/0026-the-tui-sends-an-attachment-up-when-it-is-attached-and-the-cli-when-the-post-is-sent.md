# The TUI sends an attachment up when it is attached, and the CLI when the post is sent

ADR-0008 folded uploading into publishing: `Publish` takes a draft naming file paths, checks every one, sends them one
at a time and publishes the post carrying their ids, so a post with a typo in its third path publishes nothing at all
and leaves nothing behind. That stays the CLI's rule. **The TUI's compose screen sends each file up the moment it is
attached**, and the post it then publishes names **pending attachments** by id rather than files by path. The two
surfaces now attach the same thing at different times, on purpose.

The reason is what a person at a compose screen is doing that a command line is not. They attach a video and go on
writing for minutes. Sent on `ctrl-s`, that video is a long silent pause at the moment they expect the screen to close,
followed by whatever the instance makes of it — too large, a type it will not take — after everything else was written.
Sent on attaching, the row shows it going up and being processed while they write, a refusal arrives while there is
still a draft to change, and by `ctrl-s` the work is usually done. A pending attachment can also be described after it
is up, which lets the description be written beside the picture rather than before it exists. Mastodon's own web client
attaches this way for the same reasons.

The CLI keeps ADR-0008's rule because none of that applies to it. Its files and descriptions are all named before
anything runs, there is nobody watching a row in the meantime, and failing whole before the first byte goes up is worth
more to a script than progress is.

## Consequences

**What ADR-0008 bought, the TUI gives up, and pays for differently.** A compose screen thrown away now leaves its pending
attachments on the instance. Nothing sees them, no post carries them, and Mastodon clears unattached media away after
about a day, so the cost is storage briefly held on somebody else's instance — not an author's uploads lying about where
they can see them. Throwing a touched compose screen away now asks first, on every way out of the screen.

**A send waits rather than refuses.** An instance will not publish a post naming media it has not finished processing.
`ctrl-s` while an attachment is still going up or being processed keeps the screen up, says it will send once they
finish, and sends then; `esc` calls that off. It never sends without the unfinished ones, which would publish something
other than what was composed. An attachment the instance refused stops the send until it is taken off or tried again.

**Two routes into publishing.** `IPostAuthor` gains a way to send one attachment up, to describe one already up, and to
publish a draft naming pending attachments by id; `Publish` from paths stays, and is what the CLI calls. Whether those
end as one draft type or two is the implementation's to settle; that the CLI keeps its all-or-nothing check is not.

A publish is still never retried (ADR-0006). Nor is an attachment's upload retried on its own: trying again is the
author's choice, made on the row that failed.
