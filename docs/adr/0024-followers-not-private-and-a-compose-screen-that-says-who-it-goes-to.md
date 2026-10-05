# The visibility Mastodon calls private is spelled followers, and a compose screen says who a post goes to

The TUI's compose screen sent no visibility at all and had no way to choose one — the CLI's `--visibility` and the
config's `default_visibility` reached it not at all — and neither surface could say what language a post is in. Adding
both to compose turned up one decision about a word and three about the screen.

**This project says followers where Mastodon says `private`.** To anybody who has not learned Mastodon's vocabulary,
a private post is one only its author can see; Mastodon's `private` is a post every follower can see. A picker is the
place that misreading does harm — it is where an author decides who reads something — so the word changes everywhere a
person reads it, not only in the TUI: `PostVisibility.Followers`, `--visibility followers`, `default_visibility =
"followers"`, `Posted … (followers).`, the feed's byline, and the `visibility` field of `--json`. `PostVisibilityName`
remains the one place it is spelled (ADR-0008), so none of those can say the other word. `private` stays only at the
wire, where `PostWire` translates, and as an alias `PostVisibilityName.Parse` accepts forever, so a config file or a
script that worked before keeps working.

Changing `--json` is the expensive part, and it was done on purpose. Keeping `private` there alone would have spared any
script matching `== "private"`, at the cost of `post create` and `post create --json` describing the same post in two
words. A machine-readable format that disagrees with the human-readable one is the thing ADR-0008's one-spelling rule was
written against, so the break is taken once, here, and called out in the release that carries it.

**Visibility is a header, chosen on a segmented row.** Compose is a mail client's compose (ADR-0015, #317), and the
visibility is its **To**, directly under **From**: all four on one row as radio buttons, `● public  ○ unlisted
○ followers  ○ direct`. Every choice is visible at once and any of them is one click or a few arrow presses away. The
filled bubble here means *chosen*, which the feed's audience marks do not, and the words beside each make that the
lesser cost than a second way to show selection. A one-value cycle (`◂ ● followers ▸`) is the fallback where a terminal
is too narrow for the row; a dropdown was rejected for four values as a step with nothing to show for it, and
Terminal.Gui's stock pickers because they draw in the library's colours rather than the theme's roles.

What the row starts on is what would go out: `default_visibility` if the config sets one, else the account's own default
as the instance reports it. A reply starts on the narrower of that and the post it answers, and values wider than the
post it answers are dimmed and cannot be chosen, so the TUI never offers what ADR-0013 would refuse. An edit shows the
row read-only, since Mastodon cannot change a published post's visibility, and keeps the headers of all three screens
alike. On a short terminal **To** is never given up, with the warning; it is the most consequential row on the screen.

**Language is a header too, chosen from a list.** **Lang** is a one-line field with a filtered dropdown in the mention
list's style, since there are some 180 languages and nobody should scroll through them. It starts on
`default_language` from the config, else the account's own posting language, else empty — which leaves it to the
instance. A reply starts on the author's own language rather than that of the post it answers: people write in their
language whatever they are answering. An edit opens on the post's language and always sends it, the warning's pattern
(ADR-0015, #140); the CLI's `post edit --language` keeps the third state, absent meaning leave it. The accepted codes and
their names live in `Wooly.Core`, beside `PostVisibilityName`, so the field, `--language` and `default_language` take the
same words.

**The account's own defaults are read once per session.** The starting values above need `source.privacy` and
`source.language`, which only `verify_credentials` reports. They are read when a profile becomes active and held for
the session, not on every compose, so opening the screen costs nothing. A read that fails leaves both unknown, and the
screen falls back to what it did before: To reads "account default" and sends nothing.

**The arrows walk the headers, and `tab` stays the rail's.** With more than two things to type into, `ctrl-w`'s toggle
no longer scales. `tab` was the obvious walk and was rejected: it is a frame key that means the same thing on every
screen, prompts included (`docs/tui-shell.md`), and a compose-only meaning would be the one place it lied. A chord per
field runs out of chords terminals leave alone, and `alt-` letters depend on a terminal setting. So the walk is the
mail client's: `↑` on the editor's first line goes up into the headers, `↑`/`↓` move between them, and `↓` off the
last goes back into the post; on **To** `←`/`→` choose. `ctrl-w` keeps its meaning, a jump into the warning and back.
While the mention list or the language list is open, `↑`/`↓` and `tab` are that list's, as they already are the
mention list's.

**Both pickers take the mouse.** A click on a value of **To** chooses it and gives the row the typing; a dimmed value,
and the whole row on an edit, ignores the click. A click on **Lang** opens its list, a click on a row picks it, the
wheel scrolls it, and a click outside closes it without changing anything. The language list and the mention list are
one list, so the mention list takes the mouse the same way rather than staying the one list on screen that does not.

## Amendment: what review of the landed spec settled (spec #333)

**The walk follows the drawn order.** The spec listed the fields as ⚠, To, Lang; the screen draws them To, Lang, ⚠,
and the arrows walk them in that order, then the post — the order a reader sees is the order `↑`/`↓` take. A field a
short terminal has given up (Lang, which goes with From) is stepped over rather than walked into unseen.

**"Account default" stays a choice.** Where To opens on "account default" — nothing known on a fresh post — it is the
first choice on the row, `● account default  ○ public  …`, so an author who steps off it can step back with the keys or
the mouse and send no visibility again. It is only there where To opened on it: a known default is a visibility, and
offering "whatever the account says" beside it would offer the same thing twice. The whole row is wider than a
standard terminal's value column, so there it is usually the narrow fallback, `◂ ● account default ▸`.

**Clearing Lang on an edit leaves the post's language alone.** Mastodon cannot remove a post's language:
`UpdateStatusService` sets it to the first of the language sent, the post's own, the account's posting language and the
instance's default that is a language it knows, so a blank one falls through to the post's own. Lang on an edit still
sends what it holds, and an empty field is still sent as `PostEdit.Language`'s "clear it", which on the wire is
nothing — so clearing it changes nothing on the instance. A post's language can be corrected, not taken away.

**The languages are Mastodon's own list.** `PostLanguageName` holds exactly what Mastodon validates a post's language
against (`LanguagesHelper::SUPPORTED_LOCALES`): its ISO 639-1 codes, the regional codes it takes as languages of their
own (`zh-TW`, `zh-YUE`, `mn-Mong`, `nan-TW` and the rest), and its ISO 639-3 codes. Mastodon does not refuse a code
outside it; it quietly replaces it with the account's posting language — so a code this client offered beyond the list
would publish a post in a language the author did not choose.

**The account's defaults are read by hand.** `AccountDefaults` reads `verify_credentials` through `RawMastodonCall`
rather than Mastonet, as `InstanceLimits` does, so that `source.privacy` is read as a plain word: a fork can answer
with a privacy Mastodon has no name for (`local`), which is a default this client does not know rather than an answer
it cannot read at all. `PostWire` stays the one place the wire's visibility words are translated; the hand-made call
asks it rather than spelling `private` again.

## Consequences

Scheduled posts, attachments, polls and media marked sensitive without a warning are still not on the TUI's compose
screen. Each is its own ticket: scheduling brings a noun of its own (a scheduled post that can be listed and cancelled),
and the other three come together.
