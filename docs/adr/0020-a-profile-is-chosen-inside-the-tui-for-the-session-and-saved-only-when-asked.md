# A profile is chosen inside the TUI, for the session, and saved only when asked

Spec #238 asked how somebody running the TUI sets up, switches between and looks after their profiles. ADR-0003 decided
where a profile is kept and ADR-0004 decided how one is signed into, and both assumed the CLI was the only place that
happens. The TUI took `--profile` at launch (story 9) and nothing more. This ADR covers the TUI's side.

## A screen on the stack, not a destination on the rail

The profiles screen is reached with `ctrl-p` and left with `esc`, like any other screen on the stack. It is not an
eleventh rail destination. ADR-0019 counted growing the rail as a cost that had to be paid for, and a destination is
somewhere you read. It has unread counts and it costs a fetch to arrive at. The profiles screen has neither. It lists
what is on this machine and fetches nothing.

The key is a ctrl chord because the frame-wide keys have to work on every screen, including the ones where letters are
typed as text. It does nothing on compose. Switching from compose would drop the draft, and this ADR does not give
drafts a way to survive a switch.

## Two words for two things: current, and acting as

CONTEXT.md's **current profile** is the saved one, which the CLI uses whenever `--profile` is not given. A TUI session
can act as a different profile without changing the saved one, just as `--profile` does. **Acting as** is the name for
that, and it is what `profile show` already prints. The profiles screen marks both. Switching changes only acting as.
`D` changes current.

They are kept apart because the TUI is where somebody browses and the CLI is where somebody scripts. If switching
changed the saved profile, someone who looked at their work account for a minute would find their cron job posting as
it.

## A switch starts again

A switch rebuilds the shell on Home. The stack, the destination cache, the unread counts and every pick are dropped.
Every outstanding enquiry is cancelled and its answer thrown away, and a pending confirmation is dismissed. The rule is
that nothing fetched as one profile is ever shown, or acted on, as another. Carrying the stack across would mean a post
on screen whose marks (boosted, favourited, pinned) describe a different reader than the one pressing `b`.

An action the instance has already received cannot be taken back, and the shell does not pretend it has been.

## Adding a profile starts from the instance

The CLI asks for a name and then an instance. The TUI asks for the instance, signs in, and only then asks for a name,
defaulting to the handle it just learned. A name is easiest to choose once you know who it names.

Both of ADR-0004's ways in are offered. The browser is the default. The address is drawn on screen as well as opened,
for the same reason `profile add` prints it: a browser that opened is not necessarily the right one. Pasting a token
into a masked field is the fallback. `IProfileRegistry.Add` is already the one path both surfaces write through, so the
TUI adds no second way to store a profile. The plaintext-token warning ADR-0003 requires appears on the profiles screen
whenever that is the store in use.

## Remove comes to both surfaces

`IProfileRegistry.Remove` deletes the config entry and the token together, for the same reason `Add` writes them
together. The CLI gets `profile remove <NAME>` in the same change, so Core does not gain something only one surface can
do. The TUI refuses to remove the profile you are acting as. Removing the current profile clears current rather than
choosing another one, and says so.

## A profile that cannot sign in no longer ends the TUI

With no profiles, or with a token the instance refuses, the TUI opens on the add form with no rail, and `ctrl-q` still
quits. `TuiExit.Failed` keeps its meaning for what should still stop at the door: a config file that cannot be read,
and a `--profile` naming nothing. Both of those are mistakes somebody has just typed.

A token revoked mid-session shows a notice naming `ctrl-p`. The screen does not open by itself: a notice leaves the
decision with the reader.

## Who you are, in the frame

The profile name is your own label and says nothing about who you are. The address does, and the handle alone does not:
`@jeff` on two instances is two people. With two or more profiles, the rail's foot shows the instance on its own row
above the quota. The rail is 18 columns and a full address rarely fits, and clipping one cuts off exactly the part that
tells two profiles apart. Compose shows "as @handle@instance" on its breadcrumb every time, with one profile or many,
because compose is where acting as the wrong account does harm.

## Not decided here, on purpose

- **Rename**: out of scope. Remove and add again covers it.
- **Unread counts for other profiles**: each one spends another profile's rate limit to answer a question nobody asked.
- **Keeping a draft across a switch**: `ctrl-p` does nothing on compose instead.
