# Drafting is one module for the session, beside the shell, reaching it through the stack and Reach

`Shell` had come to hold about a third of its 3,300 lines for the two **draft**s: the attachments a compose screen
sends up, a send waiting on them, what is pasted or chosen in the **file browser**, the **description editor**, and the
limits and defaults a draft opens with — about thirty arms of `Shell.Do`, pass-throughs the views called
(`ChangeCompose`, `WriteDescription`, …), and three places in the stack's own code (`Leave`, `Reset`, `Pop`) special-
casing a compose leaving or being uncovered. The edit public profile form added a second draft that shares some of it
under compose's name. So all of that becomes **Drafting**: one module for the session, beside `Shell`.

**One for the session, not one per draft and not the screens themselves.** What drafting remembers — the folder last
attached from, that the clipboard could not be read, the limits an instance answered and the defaults an account did —
outlives any one draft, and uploads were already one record keyed by the compose they belong to. Per-draft state stays
on the draft screens as it was.

**It reaches the shell through two things and no more.** A small stack seam — what is on it, what is on top, push a
screen, and take off *a named screen* and whatever is above it, never "the top" and never by index — with `Shell`'s
adapter in production and a list in the tests. Everything else it needs of the shell — the profile acting as, putting
an **Enquiry**, saying something, telling a **Change** — it reads off **Reach**, the same narrow reach a screen gets
for a verb of its own, read afresh on every call so that a profile switch needs no rewiring. It gets no rail, no cache,
no arrival, and no part in asking before a touched draft is thrown away: that question is about the whole stack and
every draft on it, and stays `Shell`'s. The shell tells it when a screen leaves (before the screen is off the stack,
so a description editor can still reach its compose) and when one is uncovered; a draft verb goes to it through one
arm of `Shell.Do`, which answers *not mine*, *used* or *declined* — declined being a clipboard paste that took nothing,
which leaves the field's own paste to run.

## Considered options

- **The draft screens answer their own verbs through `Reach`**, as the file browser already does. Rejected: it would
  widen **Reach** with uploads, the clipboard and the folder memory, and the glossary keeps Reach narrow on purpose — a
  verb that needs more than it offers is not screen-local. And the folder memory and the clipboard notice belong to no
  one screen.
- **A desk per kind of draft** behind one Drafting, so a third kind of draft is one more adapter. Rejected for now:
  with compose and the public profile form there is nothing a desk seam would vary that the module's own arms do not,
  and one adapter is a hypothetical seam. A third kind of draft is the time to revisit it.
- **Drafting with a wide port of its own** onto the shell (the stack, the notice, the enquiry, changes, an event on
  every stack move). Rejected: it moves the shell's complexity into the dependency rather than removing it, and
  duplicates what **Reach** already is.
