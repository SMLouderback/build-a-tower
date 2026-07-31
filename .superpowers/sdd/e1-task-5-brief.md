### Task 5: Assets, HUD catalog, README, closeout

**Files:**
- Create/update assets:
  - `Assets/ScriptableObjects/Rooms/ShopFastFood.asset` (id `shop_food_fast`, Food, pay 40, slots 4, hours 11â€“21)
  - `Assets/ScriptableObjects/Rooms/ShopRestaurant.asset` (id `shop_food_restaurant`, â€¦)
  - `Assets/ScriptableObjects/Rooms/ShopRetail.asset` (id `shop_retail`, â€¦)
  - Copy or mirror under `Assets/Resources/Rooms/` for HUD `Resources.Load`
  - Retire/repurpose `RetailFastFood.asset` (rename fields to Fast Food or leave unused)
- Modify: `TowerHudController.EnsureElevatorAndCatalog` â€” `AddRoomButton` for the three shops
- Modify: scene `placeableRooms` if needed (Resources load is enough)
- Modify: `README.md`
- Test: extend `BuildCatalogTests` for three shops Food/Retail grouping

- [x] **Step 1: Assets authored with `hasActiveHours`, costs, colors, sizes (16×1 commercial)**

- [x] **Step 2: HUD loads all three; catalog nests correctly**

- [x] **Step 3: README play bullets for Shops + visits + midnight payout**

- [x] **Step 4: Roslyn typecheck Scripts + EditMode — Commit**

```bash
git commit -m "feat: shop assets, HUD catalog, README for E1 visits"
```

---

