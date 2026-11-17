# A translation is the instance's, asked for one post at a time, and offered only where it would be given

Translating a post (`t` in the TUI, `post translate` in the CLI) asks the reader's **instance** for a **Translation**
through Mastodon's own `POST /api/v1/statuses/:id/translate`, which hands the post to whichever translator the
instance's admin configured (DeepL or LibreTranslate) and answers with the text, the warning, the poll's options and the
attachments' descriptions translated together, the language it detected and the provider.

**The instance is the only translator.** Wooly calls no translation service of its own — no DeepL key, no
LibreTranslate address, no model — so an instance without a translator offers no translation, and a post's words go
nowhere the instance would not already send them. A translator of Wooly's own would bring keys to keep, a third party
reading posts, and a privacy question each, and is a decision for a spec of its own if it is ever wanted.

**A Public profile is not translated.** Mastodon has no endpoint translating an account's bio, display name or custom
fields, nor any free text: the one translate endpoint takes the id of a stored post. Posts on an account's screen are
translated like any other; the bio waits on a translator of Wooly's own, above.

**Asked by hand, one post at a time.** Every translation is a call the instance pays its translator for, and a
timeline translated as it arrives would spend rate limit and someone's DeepL quota on posts nobody reads. Mastodon's
web app asks the same way.

**Offered only where it would be given.** The instance refuses a post that is followers-only or direct, a pair whose
target is the source, and everything when it has no translator. So the TUI announces `t` only on a post that is public
or unlisted, has words, and whose language the instance's `GET /api/v1/instance/translation_languages` map can take to
the reader's — the map read once per instance, `und` standing for a post with no language. The reader's language is
the `translate_to` setting, global rather than per profile because it is a fact about the reader, not an account.
Unset, Wooly sends no `lang`, the instance falls back to the account's web-interface language — which no API exposes —
and the language check is dropped. A refusal still possible (a stale map, a translator out of quota) is a **Notice**
in the instance's words, and the post stays as written. The CLI does not pre-check: one call, and a refusal is an
error under ADR-0006.

**The target the instance used is the one shown.** Mastodon honours `lang` only when it names one of its own interface
locales and otherwise quietly translates into the account's; the answer's `language` says which happened. Wooly shows
the translation either way and names both languages on its provenance line, rather than refusing a translation the
reader can still use.

**Instances older than 4.2 are not offered translation.** They have no languages map and translate the body alone;
supporting them would be a second path for less of the post.

## Consequences

ADR-0001's one library loses another endpoint family: Mastonet 3.1.3 has no translation at all, so both calls go
through `RawMastodonCall`, as trends did (ADR-0036).

Whether a post is showing translated is a field of its **Reading**, kept and dropped as a reveal is — per screen, gone on
`esc`, **Refresh** or **Arrival**. The translated text is held for the session by post and target language, so asking
again is instant, and dropped when the post is edited.

The integration suite gains a LibreTranslate container loading two languages, since the refusals that decide what is
offered are exactly what a fake would get wrong.
