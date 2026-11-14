# Noor ↔ Sage

Questions and answers between Noor and Sage. Sage writes here; Noor reads it on GitHub; Tom brings her answers back.
Newest questions at the top. Each question has a number, so an answer can be as short as "Q3: closed".

**Noor:** answer in any form, in any order, and skip anything you don't care about. "No opinion" is a fine answer too.
Where Sage has a default it says so, and that is what happens unless you say otherwise.

---

## Open questions

### Q1 — Gate leaves: open or closed?
The gates in the courtyard passages are drawn standing **open**, flat against the passage walls. In Warsaw many are
locked, with a small pedestrian door cut into one leaf.
- **a)** always open (today);
- **b)** a mix, decided by the seed (some open, some closed with a door in the leaf);
- **c)** open or closed as a game state, so a player can close a gate (survival: a courtyard becomes a safe yard).

*Sage's default:* **b** now, built so **c** is easy later.

### Q2 — Staircases that go through to the yard?
Every courtyard now gets one staircase door from the yard. In many blocks the staircase runs right through: a door
on the street side and another on the yard side of the same stairwell.
- Should a staircase on a wall facing a courtyard always get a second door into the yard?
- Is one yard door per courtyard enough for small courtyards?

*Sage's default:* through-doors where the stairwell backs onto the yard; one yard door minimum.

### Q3 — Which style next?
After the PRL block, the roadmap's next styles are:
- **kamienica** (pre-war tenement: cornices, tall windows, a gate passage, shop fronts on the ground floor);
- **detached houses** (pitched roofs, the suburbs and villages of Mazowsze);
- **shop ground floors** under any style (shop windows, signs, roller shutters).

Which matters most to you first? Anything you'd want in a kamienica from the start?

*Sage's default:* kamienica, with shop fronts on its ground floor.

### Q4 — Light well or courtyard?
Holes under 40 m² count as light wells (no way in); larger ones are courtyards and get a passage. Does 40 m² sound
right, or do you know Warsaw yards smaller than that which people do walk into?

*Sage's default:* 40 m².

### Q5 — Licence for BadBuilding
The repository has no licence yet, so legally nobody else may use the code. This is Tom's and your decision:
- **MIT:** anyone may use it, also in closed games; only the copyright line must stay;
- **MPL 2.0:** anyone may use it, but changes to BadBuilding's own files must be shared back;
- **no licence for now:** stays "look, don't use".

*Sage's default:* none until you two decide.

---

## Answered

Noor's earlier feedback and what came of it, so nothing gets lost.

| When | Noor said | What Sage did |
|---|---|---|
| 14 Nov 2026 | The recon notes (docs/RECON_NOOR.md): site first, exposure, sources, safe failure, a hard footprint set. | Milestone 1 built on them (D1–D6). |
| 14 Nov 2026 | Party walls must stay blank. | Kept: walls touching a neighbour get no windows (D4). |
| 14 Nov 2026 | "How does anyone get into the courtyard?" Gate passage, pedestrian passage, or an existing gap; make it a validation rule. | Gate or pedestrian passage cut through the ground floor, a yard door, and an access check (D7). |
