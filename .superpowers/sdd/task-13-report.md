### Task 13 Report: Trophy Room placeable + pedestal bind

Implemented:
- Added `TrophyRoomDisplay` to map a persistent `TrophyCase` into one pedestal slot per `ChallengeSubject`, with empty slots for `None` and deterministic subject/metal sprite keys for claimed trophies.
- Added `TrophyRoom.asset` as a 6x1, $76,000, zero-income Utility vanity room gated by normal lobby placement.
- Registered Trophy Room in the build HUD room list and dollhouse art mapping.
- Added warm gallery placeholder menu and dollhouse assets with no magenta scene color.

Verification:
- Red: `TrophyRoomDisplayTests` first failed on missing `TrophyRoomDisplay`.
- Green: `BuildATower.Tests.TrophyRoomDisplayTests` passed in Unity EditMode (`unity-trophy-room-green.log`).
- Green: `BuildATower.Tests.RoomTypeAssetTests` passed (`unity-trophy-room-roomtype.log`).
- Green: `BuildATower.Tests.MenuCatalogTests` passed (`unity-trophy-room-menu-catalog.log`).
- Known unrelated dirty-tree failure: `BuildATower.Tests.RoomDollhouseArtTests` fails on existing edit-mode `Object.Destroy` log assertions in `RoomDollhouseArt`, not on Trophy Room mapping (`unity-trophy-room-dollhouse.log`).

Notes:
- Existing dirty work in `TowerHudController.cs` and `RoomDollhouseArt.cs` was preserved; only Task 13 hunks were staged for commit.
