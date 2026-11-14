# Business Support Subgroup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Utility subgroup Business Support (Mail, Loading Dock, Conference, Event Hall) so venues reappear in the build menu; Services keeps security/maintenance/research/trophy.

**Architecture:** Extend `BuildSubgroup` + `BuildCatalog.Group` Utility nest order; tag assets and id fallbacks; HUD icon + glyph; EditMode updates.

**Tech Stack:** Unity EditMode NUnit, existing build catalog / HUD patterns.

**Spec:** `docs/superpowers/specs/2026-11-07-business-support-subgroup-design.md`

## Global Constraints

- Utility order: Health → Power → Water → Business Support → Services.
- Do not change conference/event sim logic.
- Magenta `#FF00FF` key plate only on new menu art.
- Commit named paths only; leave unrelated dirty WIP alone.
- Unity 6000.4.7f1 batchmode when unlocked.

## File map

| File | Change |
|------|--------|
| `Assets/Scripts/Data/BuildFamily.cs` | `BusinessSupport` enum |
| `Assets/Scripts/UI/BuildCatalog.cs` | Nest + label |
| `Assets/Scripts/Data/RoomTypeSO.cs` | Fallbacks |
| `Assets/Resources/Rooms/ServiceMail.asset` etc. | `buildSubgroup` |
| `Assets/Scripts/UI/TowerHudController.cs` | Icon id + glyph |
| `Assets/Resources/Art/Menu/subgroup_business.*` | Icon |
| `Assets/Tests/EditMode/BuildCatalogUtilitySubgroupTests.cs` | Expectations |
| `Assets/Tests/EditMode/RoomTypeAssetTests.cs` | Subgroup asserts |

---

### Task 1: Enum + catalog + fallbacks + assets

**Files:** BuildFamily.cs, BuildCatalog.cs, RoomTypeSO.cs, four room assets, BuildCatalogUtilitySubgroupTests.cs, RoomTypeAssetTests.cs

- [ ] **Step 1:** Add failing tests for Business Support order/membership and conference/event fallback.
- [ ] **Step 2:** Add `BuildSubgroup.BusinessSupport`; `SubgroupLabel` → `"Business Support"`; Utility `AddSubgroup` after Water before Services.
- [ ] **Step 3:** `ResolvedBuildSubgroup` maps mail/loading_dock/conference/event_hall → BusinessSupport; remove mail/dock from Services list.
- [ ] **Step 4:** Set `buildSubgroup` on ServiceMail, ServiceLoadingDock, Conference, EventHall assets to BusinessSupport (enum int — check Services value and append).
- [ ] **Step 5:** Run EditMode filter `BuildCatalogUtilitySubgroupTests|RoomTypeAssetTests` — PASS; commit.

**Enum note:** After current `Services`, add `BusinessSupport`. Asset YAML ints: Food=1 … Services=6, so BusinessSupport=7 if appended after Services. Verify against Unity serialization of existing Food=1.

---

### Task 2: HUD icon + glyph

**Files:** TowerHudController.cs (MenuIconIdForSubgroup, SubgroupGlyph only — surgical), Art/Menu/subgroup_business.png+.bytes

- [ ] **Step 1:** Map BusinessSupport → `subgroup_business` / glyph `Bs`.
- [ ] **Step 2:** Generate/bake themed icon (magenta key).
- [ ] **Step 3:** Commit named files only.

---

### Task 3: Regression

- [ ] Run EditMode `BuildCatalog|RoomTypeAsset|UtilitySubgroup` — all PASS.
