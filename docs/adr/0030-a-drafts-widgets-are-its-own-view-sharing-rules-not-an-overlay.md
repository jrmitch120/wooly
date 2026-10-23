# A draft's widgets are its own view's, sharing the rules rather than one overlay module

Three views lay real widgets over a screen of the content panel: compose's (#365), the edit public profile form's
(#406), and the **Description editor**'s over a compose. An architecture review proposed one deep "draft overlay"
module behind them — placing widgets at the screen's stops, showing, filling and hiding them, keeping focus with the
walk, the mouse hook, the frame's keys — with each draft supplying only its widgets and how to fill them. It is not
built, and the reason is where the variation lives.

What the three views genuinely share is mechanical and small: binding a widget to a rectangle the screen works out,
the window's relaying of redraws and suppressing of its own scroll while one is showing, and the rules for a question
open on the status row and for the mouse (a right click is the draft's `esc`; a press over a field while a question is
open goes to the question), which `DraftFields` already holds. Measured, that is about 115 lines that would come
together. What differs is most of each view: how it fills its fields (compose lays each field out before filling it;
the form grows rows as custom fields are added and refreshes by which form it is showing), which widget sits at which
stop, how focus follows the walk, and which keys it takes before the frame. A module owning all of it would need an
interface carrying every one of those per draft — a stop type, a widget per stop, a rectangle per stop, a fill, a key
list — which is a wide interface over a thin implementation: shallow, however many adapters it has.

So each view keeps its widgets, and what is shared is shared as rules and helpers: `DraftFields` for the question and
the mouse, a placement helper for binding a widget to a screen's rectangle, and one list of overlays in the window so
its relay and scroll check are loops rather than three copies. A third kind of draft with its own widgets joins by
using those, not by implementing an overlay. Revisit if a later draft turns out to fill, walk and focus the way an
existing one does — then the variation would have moved, and the deep module with it.
