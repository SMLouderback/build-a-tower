# Task 5 Report — Dollhouse + menu art

## Picks
| Room | Dollhouse | Menu |
|------|-----------|------|
| Mail | v1 cutaway → `mail_4x1` | v2 cart on tan wall → `service_mail` |
| Recycling | v2 cutaway → `recycling_6x1` | v1 cart on tan → `service_recycling` |
| Loading Dock | flat v2 cutaway → `loading_dock_8x1` | v2 pallet jack on warehouse wall/floor → `service_loading_dock` |

## Code
- `RoomDollhouseArt` leaf maps for three ids
- `BuildingServicesArtTests` + `RoomDollhouseArtTests` map rows
- Menu icons under `Resources/Art/Menu/`; dollhouse under `Resources/Art/Dollhouse/`

## Notes
- Loading Dock contacts regenerated as flat 2.5D to match Mail/Recycling perspective
- Flat v2 key plate was crimson; import keyed to alpha for clean Resources plate
