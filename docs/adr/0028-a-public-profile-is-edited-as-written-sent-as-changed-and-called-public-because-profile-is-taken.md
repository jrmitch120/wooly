# A public profile is edited as written, sent as changed, and called public because profile is taken

A reader wanted to edit what their account shows everyone: its display name, bio, avatar, header, custom fields, and
whether it is locked, a bot, and discoverable. Mastodon calls this "Edit profile" and keeps it all behind one call,
`PATCH /api/v1/accounts/update_credentials`. Both surfaces get it: `wooly account edit` on the CLI, and a form on the
stack opened from **You** in the TUI. This ADR records the three choices a later reader would otherwise wonder about.

## Called public profile, because profile is taken

In this project a **Profile** is the local entry (a name, an instance and a token) behind `wooly profile …`,
`--profile`, `current_profile` in the config file, `ctrl-p` and ADR-0020. Mastodon's sense of the word needed a name of
its own, and the reader wanted it to keep "profile".

Renaming the local concept so that Mastodon's could have the bare word was weighed and refused. It would break every
script that runs `profile` commands and every config file already written, unless both names were kept as aliases, and
then the glossary would hold two words for one thing. Using plain "edit profile" for both was refused too: in the TUI it
would sit next to `ctrl-p` "profiles" and mean something else, and the glossary would hold one word for two things.

So the thing is the **Public profile**, which still says profile and says why only your own can be changed: it is the
part everyone sees. The TUI's crumb and status row say "edit public profile". The CLI command is `account edit` rather
than `profile edit`, because the `profile` branch is the local entry, and its help says "Edit your public profile". The
glossary's earlier loose uses of "profile" in Mastodon's sense (**Bio**, **Familiar followers**, **Pinned**) now say
"public profile".

## Edited as written, never as read

The **Bio** this client holds has been flattened from the HTML an instance serves. Prefilling an editor with it and
saving it back would write the flattening into the account: links reduced to their text, and whatever the flattener
dropped dropped for good. The custom fields have the same problem.

Mastodon keeps what the account actually wrote under `source` (`source.note`, `source.fields`) on
`verify_credentials`. Both surfaces read that, fresh, every time an edit begins, and never prefill from the cached
**Account**. If the read fails, the TUI form does not open and the status row says why. An edit that cannot start from
what was written does not start.

## Sent as changed

A save sends only the fields that differ from what was read. A save that resent everything would overwrite anything
changed on the web since the form opened. The exception is Mastodon's own: `fields_attributes` replaces all four custom
fields at once, so touching any one of them resends all four. On the CLI, any `--field` replaces all four, and
`--clear-fields` empties them.

Limits are the instance's to enforce. Mastodon's own limits (a display name's 30 characters, raised to 40 in 4.6, 500 for
a bio, and the image sizes) move between versions and differ on forks, and an instance does not report them. Nothing is refused before sending. A rejection
comes back as the instance's own words, the form stays open with every edit intact, and the CLI prints it to stderr with
ADR-0006's exit codes.

## Pictures go up when the form is saved

ADR-0026 has the TUI send an attachment up the moment it is attached, so a post is ready by the time `ctrl-s` is
pressed. An avatar or header cannot work that way, because Mastodon has no endpoint that holds a profile picture ahead
of time. It is part of the same multipart request as every other field. So a picked file is drawn in the form (through
the same pipeline as any picture, ADR-0016/0022) with its file name beside it, and it is sent only when the form is
saved. ADR-0026 stands for posts.

## Not decided here, on purpose

- **Removing an avatar or header.** That is a separate call (`DELETE /api/v1/profile/avatar|header`, Mastodon 4.2+)
  that MastoNet does not wrap. It has its own issue.
- **Posting defaults** (default visibility, sensitive, language). They are stored by the same call but are preferences,
  not things anyone sees. They have their own issue.
- **Reordering custom fields.** An emptied row removes its field, and nothing more.
