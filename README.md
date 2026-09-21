# ExaAccess — screen-reader accessibility mod for EXAPUNKS

Insert documentation here until we get time to write some.

## Keys

Everything the mod binds, plus the game's own keys it deliberately leaves working. There is
no mouse requirement anywhere; Escape is always the game's own back / close / leave.

### Everywhere

| Key | What it does |
|---|---|
| Any key | At the loading screen: continue (the mod announces the prompt) |
| Up / Down | Previous / next item in the current group |
| Left / Right | Move along a row (tabs, grids, the solitaire board); adjust a slider |
| Tab / Shift+Tab | Next / previous group (Tab stop) |
| Home / End | First / last item (in the run logs: the whole log's first / last row) |
| Page Up / Page Down | Adjust a slider by a large step |
| Ctrl+Up / Ctrl+Down | Previous / next region inside a group (chat log vs. roster, log cycles or tests) |
| Enter (or keypad Enter) | Activate the focused item |
| Backspace | Secondary action where one exists: delete the focused solution / network / EXA window, cancel a picked-up solitaire card, close a popup |
| Space | Read the game's own tooltip for the focused item, when it has one |
| Escape | Back / close / leave — handled by the game itself |

Lists and tabs select as you move; Enter is only needed to open or press something.
Text fields are typing-first: land on one and type. Typed and deleted characters are echoed.

### EXA editor

| Key | What it does |
|---|---|
| F2 | Step the simulation one cycle (hold to repeat) |
| F3 / F4 / F5 | Pause / run / fast-forward (game keys) |
| F1 | Open the goal view as a readable popup (game key; also the Show Goal button) |
| F8 | Run to the caret's line — stops when any copy of that program reaches it |
| Shift+Enter | Run to line for one specific EXA (on an EXA window row, or in the code field) |
| Ctrl+Up / Ctrl+Down | In the code field: switch to the previous / next EXA program (game keys) |
| Alt+Up / Alt+Down | While running: follow the previous / next live instance of the focused program |
| Ctrl+Enter | Create a new EXA (game key) |
| Ctrl+O | Open the solution browser (game key) |
| Ctrl+Z / Ctrl+Y | Undo / redo (game keys) |
| Arrows, Home, End, Page Up / Down, Shift+arrows, Ctrl+A, Ctrl+C / X / V | Normal text editing in the code field (game keys); the mod reads the line or character you land on |
| Enter on a file row | Open the file's values, one row per item |
| Enter on a link row | Jump to the host at the other end |
| Enter on a problem row | Jump the caret to the offending line |
| Digits, period, Backspace, then Enter | In the "Go to cycle" field: jump the execution log to a cycle (`test.cycle` pins a test run) |
| Escape | Stop and reset the run, or leave the editor (game key) |

In the Redshift sandbox, while a program is free-running every key belongs to the game
(the pad: W A S D, J K L, Enter for START by default); pause with F3 to browse again.

### Cutscenes and the credits

| Key | What it does |
|---|---|
| Space, Tab or Enter | Advance to the next line (game keys); each line is spoken as it appears |
| Escape | Skip (game key) |
| Up / Down, Enter | In an EMBER-2 conversation with answer choices: review the choices and pick one |
| 1 – 9 | Pick an answer directly (game keys) |

The end credits play by themselves and are read out card by card.

### Solitaire (ПАСЬЯНС)

| Key | What it does |
|---|---|
| Arrows | Move around the board (left / right between columns, up / down within one) |
| Enter | Pick up the focused card or run; Enter again on a destination to drop it |
| Backspace | Put the picked-up cards back |
