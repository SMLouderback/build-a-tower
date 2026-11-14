# Shop Dollhouse Art Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship painterly dollhouse cutaways for the six new shops via magenta contact-sheet generation, chroma crop (no stitch), `RoomDollhouseArt` mapping, and EditMode coverage.

**Architecture:** Generate one magenta contact sheet per shop with 2–3 full-room strips; crop strips with a PowerShell helper; commit PNG+`.bytes`+`.meta` under `Assets/Resources/Art/Dollhouse/`; map ids in `RoomDollhouseArt.LeafByTypeId`; extend the existing Catalog tests. Human picks the winning strip before each shop is wired.

**Tech Stack:** Unity 6000.4.7f1, C#, PowerShell + System.Drawing for crop, image generation for contact sheets, NUnit EditMode tests

## Global Constraints

- Magenta contact sheet → crop **whole rooms**; **no panel stitching**
- One shop per review round; 2–3 variants on the sheet; user picks winner
- Style: side cutaway, dark structural frame, warm lighting (match existing shops)
- Do **not** force office h=128 stitch pipeline
- Order: Taco → Chicken → Mexican → Gag Gifts → Shoes → Department Store
- Gameplay / footprints / costs unchanged
- Spec: `docs/superpowers/specs/2026-09-04-shop-dollhouse-art-design.md`

## File map

| File | Role |
|------|------|
| `.superpowers/sdd/shop-dollhouse-crop.ps1` | Split magenta sheet into strips; write PNG+bytes+meta |
| `Assets/Resources/Art/Dollhouse/<leaf>.png` (+ `.bytes`, `.meta`) | Six new shop sprites |
| `Assets/Scripts/Rendering/RoomDollhouseArt.cs` | `LeafByTypeId` entries |
| `Assets/Tests/EditMode/RoomDollhouseArtTests.cs` | Catalog + PNG existence (count 39 → 45) |

**Leaf names (exact):**

| id | leaf |
|----|------|
| `shop_food_taco` | `taco_counter_12x1` |
| `shop_food_chicken` | `chicken_shack_12x1` |
| `shop_food_mexican` | `mexican_restaurant_16x1` |
| `shop_retail_gifts` | `gag_gifts_10x1` |
| `shop_retail_shoes` | `shoe_store_12x1` |
| `shop_retail_department` | `department_store_16x2` |

**Human gate:** Tasks 2–7 each pause for user pick on the generated sheet before crop/wire. Do not commit art the user has not approved.

---

### Task 1: Magenta multi-strip crop script

**Files:**
- Create: `.superpowers/sdd/shop-dollhouse-crop.ps1`

**Interfaces:**
- Produces: script that reads a contact-sheet PNG, finds horizontal non-magenta bands (strips), writes chosen strip(s) to `Assets/Resources/Art/Dollhouse/<leaf>.png` + `.bytes` + metas cloned from `fast_food_16x1.png.meta`

- [ ] **Step 1: Create crop script**

```powershell
# .superpowers/sdd/shop-dollhouse-crop.ps1
# Usage:
#   pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet path\to\sheet.png -Leaf taco_counter_12x1 -StripIndex 0
#   pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet path\to\sheet.png -ListOnly
param(
    [Parameter(Mandatory = $true)][string]$Sheet,
    [string]$Leaf = "",
    [int]$StripIndex = 0,
    [switch]$ListOnly
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$dst = Join-Path (git rev-parse --show-toplevel) "Assets/Resources/Art/Dollhouse"
function New-Guid32 { [guid]::NewGuid().ToString("N") }

function Test-IsMagenta([System.Drawing.Color]$c) {
    return ($c.R -gt 200 -and $c.B -gt 200 -and $c.G -lt 120) -or ($c.A -lt 20)
}

function Get-StripBounds([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $rowHasContent = New-Object bool[] $h
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not (Test-IsMagenta $bmp.GetPixel($x, $y))) { $rowHasContent[$y] = $true; break }
        }
    }
    $strips = @()
    $y = 0
    while ($y -lt $h) {
        while ($y -lt $h -and -not $rowHasContent[$y]) { $y++ }
        if ($y -ge $h) { break }
        $y0 = $y
        while ($y -lt $h -and $rowHasContent[$y]) { $y++ }
        $y1 = $y - 1
        $minX = $w; $maxX = -1
        for ($yy = $y0; $yy -le $y1; $yy++) {
            for ($x = 0; $x -lt $w; $x++) {
                if (-not (Test-IsMagenta $bmp.GetPixel($x, $yy))) {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                }
            }
        }
        if ($maxX -ge $minX) {
            $strips += [pscustomobject]@{ Index = $strips.Count; X = $minX; Y = $y0; W = ($maxX - $minX + 1); H = ($y1 - $y0 + 1) }
        }
    }
    return $strips
}

$bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Sheet))
$strips = @(Get-StripBounds $bmp)
if ($ListOnly -or [string]::IsNullOrWhiteSpace($Leaf)) {
    $strips | ForEach-Object { "strip $($_.Index): $($_.W)x$($_.H) at ($($_.X),$($_.Y))" }
    $bmp.Dispose()
    return
}
if ($StripIndex -lt 0 -or $StripIndex -ge $strips.Count) {
    $bmp.Dispose(); throw "StripIndex $StripIndex out of range 0..$($strips.Count - 1)"
}
$s = $strips[$StripIndex]
$out = New-Object System.Drawing.Bitmap($s.W, $s.H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($yy = 0; $yy -lt $s.H; $yy++) {
    for ($xx = 0; $xx -lt $s.W; $xx++) {
        $c = $bmp.GetPixel($s.X + $xx, $s.Y + $yy)
        if (Test-IsMagenta $c) { $c = [System.Drawing.Color]::FromArgb(0, 0, 0, 0) }
        $out.SetPixel($xx, $yy, $c)
    }
}
$bmp.Dispose()
New-Item -ItemType Directory -Force -Path $dst | Out-Null
$png = Join-Path $dst "$Leaf.png"
$out.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Copy-Item $png (Join-Path $dst "$Leaf.bytes") -Force

$tpl = Get-Content (Join-Path $dst "fast_food_16x1.png.meta") -Raw
$pngMeta = Join-Path $dst "$Leaf.png.meta"
if (-not (Test-Path $pngMeta)) {
    $body = $tpl -replace 'guid: [0-9a-f]{32}', ("guid: " + (New-Guid32))
    $body = $body -replace 'spriteID: [0-9a-f]{32}', ("spriteID: " + (New-Guid32))
    Set-Content -Path $pngMeta -Value $body -NoNewline
}
$bytesMeta = Join-Path $dst "$Leaf.bytes.meta"
if (-not (Test-Path $bytesMeta)) {
    @"
fileFormatVersion: 2
guid: $(New-Guid32)
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content $bytesMeta
}
Write-Host "wrote $Leaf $($s.W)x$($s.H) from strip $StripIndex"
```

- [ ] **Step 2: Smoke-test ListOnly on the office magenta example sheet** (or any magenta multi-strip PNG available)

```powershell
pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet "<path-to-magenta-sheet>" -ListOnly
```

Expected: prints `strip 0: …`, `strip 1: …`, etc.

- [ ] **Step 3: Commit**

```bash
git add .superpowers/sdd/shop-dollhouse-crop.ps1
git commit -m "chore: add magenta contact-sheet crop helper for shop dollhouse art"
```

---

### Task 2: Taco Counter art + map

**Files:**
- Create: contact sheet (agent-generated; save under `.superpowers/sdd/shop-sheets/taco_contact.png` before crop)
- Create: `Assets/Resources/Art/Dollhouse/taco_counter_12x1.png` (+ `.bytes`, `.meta`)
- Modify: `Assets/Scripts/Rendering/RoomDollhouseArt.cs` (`LeafByTypeId`)
- Modify: `Assets/Tests/EditMode/RoomDollhouseArtTests.cs` (`Catalog` + count)

**Interfaces:**
- Produces: mapping `shop_food_taco` → `taco_counter_12x1`

- [ ] **Step 1: Generate magenta contact sheet**

Prompt requirements (must include):

- Solid hot magenta background (`#FF00AA` / chroma pink)
- **Exactly 3** complete horizontal dollhouse room strips, each a full **12:1** aspect taco fast-food counter cutaway
- Style match: side cutaway like `fast_food_16x1` — dark structural frame, warm spotlights, painterly, **no people**
- Props: service counter, stools, salsa/toppings, illuminated menu boards, orange/green warmth; optional small street window
- Strips separated by visible magenta gutters (for auto-split)

Save sheet to: `.superpowers/sdd/shop-sheets/taco_contact.png`

- [ ] **Step 2: HUMAN GATE — user picks strip index (0/1/2) or requests regen**

Do not proceed until user replies with the winning strip index (or “regen”).

- [ ] **Step 3: Crop winner**

```powershell
pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet .superpowers/sdd/shop-sheets/taco_contact.png -Leaf taco_counter_12x1 -StripIndex <N>
```

- [ ] **Step 4: Failing Catalog expectation**

In `RoomDollhouseArtTests.cs`, add to `Catalog`:

```csharp
("shop_food_taco", "taco_counter_12x1"),
```

Change:

```csharp
Assert.AreEqual(40, Catalog.Length);
```

(from 39 — only taco so far; later tasks bump this)

Add mapping in `RoomDollhouseArt.cs` only after PNG exists:

```csharp
["shop_food_taco"] = "taco_counter_12x1",
```

- [ ] **Step 5: Run EditMode filter `RoomDollhouseArtTests` — expect PASS**

- [ ] **Step 6: Commit**

```bash
git add Assets/Resources/Art/Dollhouse/taco_counter_12x1.png Assets/Resources/Art/Dollhouse/taco_counter_12x1.bytes Assets/Resources/Art/Dollhouse/taco_counter_12x1.png.meta Assets/Resources/Art/Dollhouse/taco_counter_12x1.bytes.meta Assets/Scripts/Rendering/RoomDollhouseArt.cs Assets/Tests/EditMode/RoomDollhouseArtTests.cs
git commit -m "art: add taco counter dollhouse sprite"
```

---

### Task 3: Chicken Shack art + map

**Files:** same pattern as Task 2 with leaf `chicken_shack_12x1`, id `shop_food_chicken`

- [ ] **Step 1: Generate sheet** `.superpowers/sdd/shop-sheets/chicken_contact.png` — 3 strips, 12:1, fried-chicken shack (warmers, buckets, stools, red/amber); magenta bg; no people; dark frame

- [ ] **Step 2: HUMAN GATE — pick strip**

- [ ] **Step 3: Crop**

```powershell
pwsh .superpowers/sdd/shop-dollhouse-crop.ps1 -Sheet .superpowers/sdd/shop-sheets/chicken_contact.png -Leaf chicken_shack_12x1 -StripIndex <N>
```

- [ ] **Step 4: Map + Catalog**

```csharp
["shop_food_chicken"] = "chicken_shack_12x1",
```

```csharp
("shop_food_chicken", "chicken_shack_12x1"),
```

`Assert.AreEqual(41, Catalog.Length);`

- [ ] **Step 5: Tests PASS — Commit** `art: add chicken shack dollhouse sprite`

---

### Task 4: Mexican Restaurant art + map

**Files:** leaf `mexican_restaurant_16x1`, id `shop_food_mexican`

- [ ] **Step 1: Generate sheet** — 3 strips, **16:1**, sit-down Mexican (booths/tables, cantina bar, pendants); **avoid big skyline window**; match `restaurant_16x1` booth rhythm; magenta bg; no people

- [ ] **Step 2: HUMAN GATE — pick strip**

- [ ] **Step 3: Crop** `-Leaf mexican_restaurant_16x1`

- [ ] **Step 4: Map + Catalog entry; `Assert.AreEqual(42, Catalog.Length);`**

- [ ] **Step 5: Tests PASS — Commit** `art: add mexican restaurant dollhouse sprite`

---

### Task 5: Gag Gifts art + map

**Files:** leaf `gag_gifts_10x1`, id `shop_retail_gifts`

- [ ] **Step 1: Generate sheet** — 3 strips, **10:1**, novelty gift shop (crowded shelves, joke displays, playful color); magenta bg; no people; dark frame

- [ ] **Step 2: HUMAN GATE — pick strip**

- [ ] **Step 3: Crop** `-Leaf gag_gifts_10x1`

- [ ] **Step 4: Map + Catalog; `Assert.AreEqual(43, Catalog.Length);`**

- [ ] **Step 5: Tests PASS — Commit** `art: add gag gifts dollhouse sprite`

---

### Task 6: Shoe Store art + map

**Files:** leaf `shoe_store_12x1`, id `shop_retail_shoes`

- [ ] **Step 1: Generate sheet** — 3 strips, **12:1**, shoe boutique (wall racks, benches, fitting stools); magenta bg; no people

- [ ] **Step 2: HUMAN GATE — pick strip**

- [ ] **Step 3: Crop** `-Leaf shoe_store_12x1`

- [ ] **Step 4: Map + Catalog; `Assert.AreEqual(44, Catalog.Length);`**

- [ ] **Step 5: Tests PASS — Commit** `art: add shoe store dollhouse sprite`

---

### Task 7: Department Store art + map

**Files:** leaf `department_store_16x2`, id `shop_retail_department`

- [ ] **Step 1: Generate sheet** — 2–3 strips, aspect **~8:1** (16 wide × 2 tall), **single sprite showing two stacked floors** (atrium or escalator cut, multi-dept); optional window on upper band only; magenta bg; no people; dark outer frame around whole 2-high mass

- [ ] **Step 2: HUMAN GATE — pick strip**

- [ ] **Step 3: Crop** `-Leaf department_store_16x2`

- [ ] **Step 4: Map + Catalog; `Assert.AreEqual(45, Catalog.Length);`**

- [ ] **Step 5: Tests PASS — Commit** `art: add department store dollhouse sprite`

---

### Task 8: Spec status + Play Mode smoke

**Files:**
- Modify: `docs/superpowers/specs/2026-09-04-shop-dollhouse-art-design.md` status → `Implemented`

- [ ] **Step 1: Update spec status to Implemented**

- [ ] **Step 2: Play Mode checklist**

1. Place Taco, Chicken, Mexican, Gag Gifts, Shoes — dollhouse overlays visible (not flat placeholders).
2. Place Department Store spanning 2 floors — art fills full footprint.
3. Spot-check basement taco/chicken: if street window looks wrong, note for regen (do not block ship unless user objects).

- [ ] **Step 3: Commit**

```bash
git add docs/superpowers/specs/2026-09-04-shop-dollhouse-art-design.md
git commit -m "docs: mark shop dollhouse art spec implemented"
```

---

## Plan self-review

| Spec requirement | Task |
|------------------|------|
| Magenta sheet → crop, no stitch | 1 + 2–7 |
| One shop at a time, 2–3 variants | 2–7 human gates |
| Six leaf mappings + Resources | 2–7 |
| Catalog/PNG tests | 2–7 (`Catalog` length → 45) |
| Department Store last / 16×2 | 7 |
| No gameplay changes | Global constraint |

No placeholders. Catalog count increments are explicit per task.
