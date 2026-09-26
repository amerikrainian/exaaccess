---
description: Audit the currently open puzzle for screen-reader parity (drawn map vs model vs speech), spoiler-free
---

The puzzle I just unlocked is open in the editor right now. Run a full map-parity
audit like the UC Berkeley one (2026-08-23 — see the map-parity notes in CLAUDE.md).

Rules of engagement: use the dev server (relaunch with ECHOPUNKS_DEV=1 only if it
isn't running). Never send input, move my focus, or navigate the game — I'm playing.
Read-only probes plus /screenshot only.

Gather three sources and compare them:

1. PIXELS — /screenshot and read the drawn map and side column yourself (zoomed
   crops where needed). Art is ground truth for what sighted players see.
2. MODEL — /eval dump: hosts (internal name, plate enum, hidden via method_10 both
   flags, size, occupants, registers incl. badge labels), links (BOTH side ids,
   locked flag), files (ids, flags, value counts, current host — watch for ids
   repeated across hosts), the mode/special-logic type (sim.method_43()), the
   editor windows dictionary, and the goal list.
3. SPEECH — read /speech, and call the live module generation's HostName and row
   readouts directly (the Bootstrap._moduleLoader trick) rather than guessing.

Check both directions:
- Everything a sighted player can SEE must be hearable: host names from EVERY
  mechanism (plates, the custom-puzzle name-display mode, vmethod_10 overrides,
  and BAKED ART or decals — if art letters something the model carries only as an
  internal name, that is a new sprite-content case needing its own per-puzzle
  model read, like the highway sign and UC Berkeley); one-way and locked links;
  file plates, file windows, and contents; register badges and live values;
  special panels (PanelCapture text vs sprite-drawn content); goal popup rows vs
  the drawn F1 view.
- Nothing hidden may LEAK: covered hosts speak only their cover caption,
  unlettered hosts stay unnamed, hidden contents stay unspoken.

Then either report parity CONFIRMED, or fix the gaps and hot-reload (safe
mid-session). Update CLAUDE.md when a new mechanism or puzzle-specific read lands.

SPOILER DISCIPLINE for your final reply: no puzzle content whatsoever — no host
names, topology, counts, file contents, goal text, or strategy hints. Describe any
gaps abstractly ("some drawn host names weren't spoken"), never concretely.
