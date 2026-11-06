### Task 1: Catalog assets + HUD + dwell rules

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs` (add `public bool isAtrium;`)
- Create: `Assets/Resources/Rooms/LeisureCasino.asset`, `LeisureNightclub.asset`, `LeisureChapel.asset`, `LeisureAtrium.asset` (+ `.meta` via Unity import or mirror existing Leisure `.meta` pattern)
- Modify: `Assets/Scripts/UI/TowerHudController.cs` (`CollectMenuRoomButtons` TryAdd list)
- Modify: `Assets/Scripts/Economy/ShopVisitRules.cs` (dwell ranges)
- Modify: `Assets/Tests/EditMode/LeisureCatalogTests.cs`

**Interfaces:**
- Consumes: existing `RoomTypeSO` YAML / `Resources.Load("Rooms/â€¦")`
- Produces: four loadable assets; atrium `incomeModel = None` (0), `isAtrium = 1`, `maxOccupants = 0`; casino/nightclub/chapel `TrafficVariable` (4)

- [ ] **Step 1: Write failing catalog tests**

Extend `LeisureCatalogTests`:

```csharp
[Test]
public void New_leisure_attractions_match_spec()
{
    AssertRoom(Load("LeisureCasino"), "leisure_casino", 10, 1, 4, true);
    AssertRoom(Load("LeisureNightclub"), "leisure_nightclub", 8, 1, 3, true);
    AssertRoom(Load("LeisureChapel"), "leisure_chapel", 6, 1, 2, true);

    var atrium = Load("LeisureAtrium");
    Assert.IsNotNull(atrium);
    Assert.AreEqual("leisure_atrium", atrium.id);
    Assert.AreEqual(new Vector2Int(8, 3), atrium.size);
    Assert.AreEqual(3, atrium.requiredStars);
    Assert.AreEqual(IncomeModel.None, atrium.incomeModel);
    Assert.IsTrue(atrium.isAtrium);
    Assert.AreEqual(0, atrium.maxOccupants);
    Assert.IsFalse(ShopVisitRules.IsTrafficVenue(atrium));
}

[Test]
public void Casino_nightclub_chapel_noise_and_street_weights()
{
    var casino = Load("LeisureCasino");
    Assert.AreEqual(0.85f, casino.noiseOutput, 0.001f);
    Assert.AreEqual(1.5f, casino.streetVisitWeight, 0.001f);
    var club = Load("LeisureNightclub");
    Assert.AreEqual(0.90f, club.noiseOutput, 0.001f);
    Assert.AreEqual(1.75f, club.streetVisitWeight, 0.001f);
    var chapel = Load("LeisureChapel");
    Assert.AreEqual(0.15f, chapel.noiseOutput, 0.001f);
}
```

Update `AssertRoom` for TrafficVariable rooms only (keep atrium assertions separate as above).

Add dwell assertions in the same file or a small test:

```csharp
Assert.AreEqual((40, 70), ShopVisitRules.DwellRangeForTests("leisure_casino"));
```

If no public dwell helper exists, assert via `PickDwellMinutes` bounds over many rolls, or extract:

```csharp
public static (int min, int max) DwellRangeForId(string id) { /* existing private switch made testable */ }
```

- [ ] **Step 2: Run tests â€” expect FAIL** (missing assets / `isAtrium`)

- [ ] **Step 3: Implement**

1. Add `public bool isAtrium;` on `RoomTypeSO` (near `isStairs`).
2. Create four YAML assets mirroring `LeisureTheater.asset` / `LeisureGym.asset` with spec fields:

| Asset | id | size | stars | cost | baseIncome | maxOccupants | noise | street | hours | incomeModel | isAtrium |
|-------|-----|------|-------|------|------------|--------------|-------|--------|-------|-------------|----------|
| Casino | leisure_casino | 10Ã—1 | 4 | 420000 | 80 | 14 | 0.85 | 1.5 | 720â€“120 (wrap) | 4 | 0 |
| Nightclub | leisure_nightclub | 8Ã—1 | 3 | 295000 | 55 | 12 | 0.90 | 1.75 | 1200â€“240 | 4 | 0 |
| Chapel | leisure_chapel | 6Ã—1 | 2 | 185000 | 25 | 8 | 0.15 | 1.0 | 480â€“1200 | 4 | 0 |
| Atrium | leisure_atrium | 8Ã—3 | 3 | 450000 | 0 | 0 | 0.20 | 1 | hasActiveHours 0 | 0 | 1 |

`buildFamily: 7`, `category: 4`, `allowAboveGround: 1`, basement true for casino/nightclub/chapel; atrium above-ground only (`allowBasement: 0`).

3. In `TowerHudController.CollectMenuRoomButtons`, after existing leisure loads:

```csharp
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureCasino"));
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureNightclub"));
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureChapel"));
TryAddRoomButton(buttons, Resources.Load<RoomTypeSO>("Rooms/LeisureAtrium"));
```

4. In `ShopVisitRules` dwell switch, add casino / nightclub / chapel ranges from spec.

- [ ] **Step 4: Run tests â€” expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/RoomTypeSO.cs Assets/Resources/Rooms/LeisureCasino.asset Assets/Resources/Rooms/LeisureNightclub.asset Assets/Resources/Rooms/LeisureChapel.asset Assets/Resources/Rooms/LeisureAtrium.asset Assets/Resources/Rooms/LeisureCasino.asset.meta Assets/Resources/Rooms/LeisureNightclub.asset.meta Assets/Resources/Rooms/LeisureChapel.asset.meta Assets/Resources/Rooms/LeisureAtrium.asset.meta Assets/Scripts/UI/TowerHudController.cs Assets/Scripts/Economy/ShopVisitRules.cs Assets/Tests/EditMode/LeisureCatalogTests.cs
git commit -m "feat: add Casino Nightclub Chapel Atrium leisure catalog"
```

---

