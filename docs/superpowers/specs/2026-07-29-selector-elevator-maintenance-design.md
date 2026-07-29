# Build-A-Tower — Selector + Elevator Maintenance Resize

**Date:** 2026-07-29  
**Status:** Approved (implement)  
**Depends on:** Slice #3 (normal elevators, TransitRouter, Floor G lobby)  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  

## 1. Goals

1. **Selector tool** — inspect any built room without a place/bulldoze tool active; highlight footprint and show a HUD summary (foundation for later metrics).
2. **Elevator edge handles** — when an elevator is selected, drag top/bottom edges to change shaft height.
3. **Maintenance mode** — stop new riders, finish existing queues/passengers, then allow permanent shortening.
4. **Correction window** — after an extension, briefly allow shrinking back toward the pre-extension bounds without maintenance.

## 2. Success criteria

In Play mode a player can:

1. Choose **Selector**, click any room, see highlight + name / floors / size / evaluation summary.
2. Select an elevator and drag top or bottom edge to **extend** (cost, placement, ≤30 floors).
3. Within **10 real-time seconds** after an extension, shrink only toward the previous bounds without maintenance (no refund).
4. Outside that window, enable **Maintenance**, wait until queues and passengers clear, then shorten permanently (no refund, min span 2).
5. Manually leave maintenance to restore service.
6. Extending never requires maintenance.

## 3. Selector

- Uses existing `BuildTool.Select`.
- Click occupied cell → select that `RoomInstance` (transit wins when it owns the cell).
- Click empty cell → clear selection.
- Switching to PlaceRoom / Bulldoze / Lobby tool clears selection highlight (or keeps selection but stops handle interaction — prefer clear selection for simplicity).
- HUD summary: display name, instance id, origin, size, floor span label, evaluation.
- Elevator-specific status line: Service / Maintenance / Draining / Ready to shorten / Correction Ns remaining.

## 4. Elevator resize rules

| Action | Rule |
|--------|------|
| Extend | Always allowed when placement + funds + max span allow. Starts/refreshes correction window. |
| Correction window | 10 real-time seconds. Stores previous `(minY, maxY)` at the moment of extension. Shorten only toward those bounds. |
| Shorten in window | No maintenance. No refund. |
| Shorten outside window | Requires `InMaintenance` **and** shaft drained (no passengers, empty up/down queues). No refund. |
| Min span | 2 floors. |
| Underlays | Removed shaft cells restore `_underElevator` rooms (same as demolish/extend path). |
| Car | Clamp floor into remaining range after shrink when drained. |

Grid API: `CanResizeElevator` / `TryResizeElevator` support grow and shrink geometrically. Policy (maintenance / correction) lives in build/UX + elevator system gates.

## 5. Maintenance mode

- Flag on `ElevatorShaftRuntime.InMaintenance` (toggled from HUD while elevator selected).
- While on:
  - `TryEnqueue` returns false.
  - `FindServing` skips the shaft (router will not plan new elevator legs onto it).
  - Existing queues and passengers continue; car keeps moving until drained.
- Does **not** auto-exit when drained; player toggles Service again.
- Agents that fail to enqueue / lose a route keep existing stress / stuck behavior.

### Anti-stranding rules

Committed agents resolve their shaft by **room instance id** (`Agent.ElevatorShaftId`), never by
`FindServing`, which skips maintenance shafts. Resolving by search would orphan waiters and riders
the moment maintenance starts.

- `AgentSystem.OnElevatorServiceChanged(shaftId)` runs on every maintenance toggle. Riders and
  correctly queued waiters are left alone so queues drain; anyone the shaft can no longer serve is
  dequeued and re-planned.
- A per-tick watchdog re-plans any `WaitingAtElevator` agent whose wait is orphaned: shaft gone,
  shaft no longer spans the trip, or queue slot lost. This also covers shortening and demolition.
- A rider whose shaft disappears is dropped at its current cell and re-planned.
- Failed `TryEnqueue` clears the recorded destination and re-plans once before stalling, so an
  agent arriving at a shaft that just entered maintenance does not freeze.

## 6. UX details

- Tools: add **Selector** (and keep Elevator place tool for initial 1×2 placement).
- Selected elevator shows top and bottom handle cells (ghost/tint).
- Dragging a handle previews the proposed shaft; release commits resize under policy.
- Place-tool extend-by-dragging-shaft (Slice 3) may remain for convenience; Selector handles are the primary discoverable path for extend/shorten.

## 6b. Queue visualization

Agents in `WaitingAtElevator` render in a line beside the shaft rather than inside it:

- Lane side = the side the agent walked in from (derived from the preceding walk leg), which keeps lines inside the building for edge-column shafts.
- Position = `shaft.X + 0.5 ± (QueueLaneOffset + queueIndex * QueueSpacing)` at the entry floor.
- Recomputed each tick from `ElevatorSystem.GetQueueIndex`, so the line compacts as agents board.
- Agents only occupy the shaft cell visually once `Riding`.
- `Agent.Cell` still holds the landing cell, so routing and stress logic are unchanged.

## 7. Out of scope

- Full metrics inspection panel
- Separate Expand Elevator menu tool
- Refunds on shorten
- Auto-exit maintenance after drain
- Express elevators / multi-car

## 8. Testing

- EditMode: grow always; shrink blocked unless drained+maintenance or within correction policy helper; maintenance blocks enqueue; router skips maintenance shafts; drain readiness.
- PlayMode / manual: select room summary; extend then undo within 10s; after window, maintenance → drain → shorten.
