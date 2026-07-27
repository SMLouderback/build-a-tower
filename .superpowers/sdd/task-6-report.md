# Task 6 Report: BuildController

## Status

Complete.

## Commit

- `134c5fe` — `feat: add BuildController for lobby drag and room place/demolish`

## Implementation

- Added `BuildController` with the required grid, wallet, tool, room type, hover-cell, and state-change API.
- Added lobby drag preview and placement on floor 1.
- Added room hover previews, room placement, and bulldoze handling.
- Set `startingFunds` to `2_000_000`.
- Implemented the fixed room placement transaction order:
  1. Validate `CanPlace` and `CanAfford`.
  2. Spend with `TrySpend`.
  3. Place with `TryPlace`.
  4. Refund with `Wallet.Add` if placement fails.
- Added Unity `.meta` files for the new script directory and script.

## Verification

- Unity command:
  `C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe -batchmode -quit -projectPath . -logFile -`
- Result: exit code `0`; script compilation completed and Unity exited batch mode successfully.
- Cursor lints: no errors in `BuildController.cs`.

## Concerns

- Scene references (`view`, `worldCamera`, and `lobbyType`) remain intentionally unwired until Task 10.
- The repository's broad `[Bb]uild/` ignore rule also matches `Assets/Scripts/Build`; the controller and its script meta required a force-add to preserve the brief's required path.
- No automated controller tests were added because Task 6 specifies a Unity compile check and commits only the controller asset; input/scene integration remains for later tasks.

## Review Fix (lobby spend refund)

- **Finding:** Lobby drag completion spent funds before `Grid.TryPlaceLobby`; a failed placement did not refund.
- **Fix:** Split spend and place in `HandleLobbyDrag`; call `Wallet.Add(cost)` when `TryPlaceLobby` fails, matching the room placement path.
- **Commit:** `40a9e2d` — `fix: refund lobby spend when placement fails`
- **Compile:** Unity `6000.4.7f1` batchmode exit code `0`; script compilation succeeded.
