# Build-A-Tower — Reward Challenges, Claim Book & Trophy Room

**Date:** 2026-11-03  
**Status:** Approved (brainstorming, section by section)  
**Depends on:** `TowerSimulation` clock/day roll; `StarSystem`; economy wallet; build/demolish grid events; `HealthSystem` / quarantine; condemn paths if present; `TowerNews` ticker; HUD command bar + bottom money readout; `StarCelebrationPauseGate` (pause pattern only); room catalog / dollhouse paint  
**Engine target:** Unity (2D Tilemap), desktop/Editor-first  
**Out of scope:** Leaderboards / Steam achievement sync; multiplayer trophy sharing; sim buffs from Trophy Room (happiness, demand, rent); auto-claim settings; numeric rebalance / heatmaps / visual polish outside this feature’s art

---

## 1. Goals

Reward players for a wide variety of tasks and challenges: tiered milestones, build counts, sustained ops goals, star ranks, negative/infamy acts, and quirky one-offs. Completion never auto-pays — players open a **Reward Book**, claim prizes (`$` + optional extras), and advance **subject trophies** (Bronze → Galactic). A placeable **Trophy Room** shows the case to tenants as pure vanity. Rotating **contracts** keep the board lively. First trophy ever pauses with a short tutorial nudge; after that, news + a pulsing HUD badge only. Claims deliver dopamine: **Claim All**, `$` rain on the savings readout, and star/light bursts from Trophy Rooms when a metal upgrades.

### Success criteria

In Play Mode a player can:

1. Complete a milestone (e.g. population tier) and see a news line + pulsing Reward Book badge with pending count — **wallet unchanged** until claim.
2. Open the Reward Book (Ready / Catalog / Contracts / Trophies), claim one reward or **Claim All**, see `$` pour toward the bottom-bar money readout, and see the wallet increase once.
3. On the first claim that awards any trophy metal, get a **paused** tutorial modal explaining the book and Trophy Room; later claims never pause for rewards.
4. See that subject’s trophy upgrade in the Trophies tab and in every placed Trophy Room; rooms play a light/star burst when metal rises.
5. Accept or pursue rotating contracts; succeed → pending claim; fail/expire → cheeky news, no claim, no trophy.
6. Place a Trophy Room anywhere legal, see empty pedestals then filled medals matching `TrophyCase`; demolishing the room does not erase trophies.
7. Reload a save and keep pending claims, claimed ids, trophy metals, tutorial flag, and active contracts.

EditMode tests cover pending-vs-paid, double-claim, metal upgrade on claim, duration holds, first-tutorial flag, absent snapshot load, contract expiry, and Trophy Room reading the case.

---

## 2. Product decisions (locked)

| Decision | Choice |
|----------|--------|
| Shape | Hybrid: permanent catalog + rotating contracts |
| Payout | Claim-only from Reward Book (no auto `$`) |
| Prizes | `$` + optional extras (tokens, titles, ribbons) |
| Trophies | Subject-consolidated; metals Bronze → Silver → Gold → Platinum → Diamond → Rainbow → Galactic |
| Trophy advance | On **claim** only (not on mere completion) |
| Trophy Room | Placeable vanity; no happiness/demand/rent buff |
| Notifications | News ticker + HUD badge with pulse while pending > 0 |
| First trophy | Pause + short modal once; `rewardTutorialSeen` thereafter |
| Claim UX | Per-item claim + **Claim All**; `$` rain on money HUD; Trophy Room burst on metal up |
| Goals button | Unchanged (next-star text); Reward Book is a **separate** badged control |
| Save | Schema stays **1**; optional `RewardsSnapshotV1`; absent = fresh rewards state |
| Approach | Data-driven catalog + claim ledger + trophy case + contract board |

---

## 3. Architecture

| Piece | Responsibility |
|-------|----------------|
| `ChallengeCatalog` | Static definitions: id, title, flavor, subject, metal, kind, condition, `$`, extras |
| `ChallengeTracker` | Event/day hooks → progress; on meet → enqueue pending (never pays) |
| `ClaimLedger` | Pending / claimed ids; `TryClaim` / `ClaimAll` apply rewards once |
| `TrophyCase` | Per-subject highest `TrophyMetal`; upgrade only from successful claim |
| `ContractBoard` | 3–5 active offers from contract pool; refresh on complete/expire/weekly roll |
| `RewardExtras` | Apply instant extras or stash tokens (build credit, timed bump, crew day) |
| `RewardBookUI` | HUD button, badge pulse, tabs, claim / Claim All, tutorial modal hook |
| `RewardClaimFx` | `$` rain toward bottom money rect; coordinates with wallet tick-up |
| `TrophyRoom` + view | Room type + cutaway pedestals from `TrophyCase`; burst FX on metal up |
| `RewardsNews` | Completion / expire / quirky lines into `TowerNews` |

**Ownership:** `TowerSimulation` constructs and ticks tracker, ledger, case, board (same pattern as `ResearchSystem`). HUD binds for UI/FX. Grid paint / `TilemapTowerView` (or dollhouse layer) reads case for Trophy Room faces.

**Flow:** sim event → tracker → pending + news + badge++ → player claims → wallet/extras + trophy maybe++ → `$` rain; if metal rose → Trophy Room bursts; if first metal ever → pause tutorial once.

---

## 4. Subjects & metals

### 4.1 Metals

`None < Bronze < Silver < Gold < Platinum < Diamond < Rainbow < Galactic`

Visuals escalate (material, trim, glow). Rainbow / Galactic use distinct flair. Book Trophies tab and Trophy Room share the same art keys.

### 4.2 Subjects (trophy shelves)

| Subject id | Covers |
|------------|--------|
| `Population` | Population ladders |
| `Stars` | Star level milestones |
| `Construction` | Build N of room type / family |
| `Demolition` | Demolish counts |
| `Operations` | Hold health/clean (etc.) for duration |
| `Infamy` | Condemn, quarantine, chaos ladders |
| `Commerce` | Shops, income, visits |
| `Transit` | Elevators, stairs, traffic feats |
| `Time` | In-game days / weeks / months survived or played |
| `Quirks` | Silly one-offs not owned by another shelf |

Claiming a challenge tagged with subject S and metal M sets `TrophyCase[S] = max(current, M)`. Lower-metal challenges in S still pay if pending and unclaimed.

---

## 5. Challenge kinds & conditions

### 5.1 Kinds

1. **Milestone** — always tracked; permanent ladder rows.  
2. **Contract** — only while on the board; timed or count; may tag a subject metal.  
3. **Quirk / Infamy** — one-shots or ladders; often humorous or “bad landlord” flavored.

### 5.2 Condition types (evaluator library)

| Type | Meaning |
|------|---------|
| `Threshold` | Metric ≥ value (pop, stars, room count, demolish total, …) |
| `DurationHold` | Metric stays in band for N in-game days (streak resets on breach) |
| `Counter` | Incremental event count since challenge armed (contracts) |
| `Flag` | One-shot boolean (quarantine entered, etc.) |
| `Compound` | AND of simple conditions (rare; keep shallow) |

Metrics hook existing systems: population, `StarSystem.CurrentStars`, grid room counts by type/family, demolish events, condemned room count, quarantine latch, effective health/cleanliness tower or floor aggregates, day index, elevator/stairs counts, etc.

### 5.3 Launch catalog size

Ship a **fat first set** (~40–60 definitions) spanning all subjects, including negative and quirky rows. Exact copy can be tuned in data; systems must not hardcode individual challenge logic beyond condition types.

### 5.4 Example milestones (illustrative)

| Id | Subject | Metal | Condition | `$` (ballpark) |
|----|---------|-------|-----------|----------------|
| `pop_100` … `pop_25000` | Population | Bronze→Galactic | Pop ≥ 100 / 500 / 1k / 2.5k / 5k / 10k / 25k | scaling |
| `star_1` … | Stars | Bronze→… | Stars ≥ N | scaling |
| `build_condo_10` … | Construction | … | Count condo ≥ N | scaling |
| `health_hold_week` … | Operations | … | Effective health ≥ band for 7 / 30 / 90 days | scaling |
| `demo_25` … | Demolition | … | Lifetime demolish ≥ N | scaling |
| `quarantine_1` | Infamy | Bronze | City quarantine once | modest |
| `condemn_10` | Infamy | … | ≥ N condemned | scaling |

### 5.5 Example quirks / contracts (illustrative)

- “Controlled Demolition” — demolish 10 rooms in 7 days (contract).  
- “Five-Star Flu” — hit quarantine while holding 5★ (quirk).  
- “Stair Master” — more stair cells than elevator shaft cells (quirk).  
- “Ghost Tower” — 0 population while ≥ 2★ (quirk).  
- “Vending Empire” — build 3 vending (or equivalent small shop) (quirk).  
- “Fire Sale” — demolish 10 shops in a week (contract / infamy).  
- “Clean Freak” — keep cleanliness ≥ 95% for 14 days (ops contract).

Tone: playful, slightly mean when infamy; never instructs real-world harm — in-game only.

---

## 6. Rewards & extras

Every claimable definition includes a `$` amount (scales with metal / difficulty).

**Extras pool (optional per definition):**

| Extra | Behavior |
|-------|----------|
| Title / ribbon | Metadata on subject trophy; shown under medal in book + room |
| Build credit | Token: next N cells or one room placement discounted/free per rules in `RewardExtras` |
| Demand bump | Timed multiplier token; expires with clock |
| Crew day | Free repair or clean crew day token |
| Pedestal cosmetic | Rare ribbon art key on Trophy Room pedestal |

Instant extras apply on claim. Tokens go to `extrasInventory` until spent or expired. **Never** grant `$`, extras, or trophy metal without `ClaimLedger` success.

---

## 7. Claim ledger & Contract Board

### 7.1 Ledger

- States per challenge id: locked/in-progress (tracker only) → **Pending** → **Claimed**.  
- `TryClaim(id)`: if pending → pay `$`, apply extras, mark claimed, maybe upgrade trophy; return claim result (cash paid, metals raised, firstTrophy).  
- `ClaimAll()`: claim every pending id; sum `$` for FX; union of metal-ups; firstTrophy if any claim was the first metal.  
- Idempotent: claimed ids never pay again.

### 7.2 Contract Board

- Maintain **3–5** active offers from the contract-tagged pool (no duplicates of active/claimed-as-needed rules).  
- Refresh when a slot frees (success → pending claim then leave board; fail/expire → remove + news) and on a **weekly** in-game roll to avoid staleness.  
- Weight quirky / infamy / ops so the board stays spicy.  
- Failed/expired: no pending claim, no trophy.

---

## 8. UI, notifications & dopamine FX

### 8.1 Reward Book control

- Command-bar button near Goals; unlock with lobby (same gate as Goals) unless a pending claim exists earlier.  
- Badge = pending count; **pulse/glow** while count > 0 (ongoing nudge after tutorial).  
- Tabs: **Ready** · **Catalog** · **Contracts** · **Trophies**.  
- Ready: list pending with Claim + **Claim All**.  
- Catalog: subjects, progress bars, locked/ready/claimed.  
- Contracts: objective, progress, time left, reward preview.  
- Trophies: shelves by subject with metal art, titles, ribbons.

### 8.2 Notifications

- On completion → `RewardsNews` ticker line + badge++.  
- No wallet change until claim.

### 8.3 First trophy tutorial

- When a claim results in the tower’s **first** non-`None` trophy metal and `rewardTutorialSeen` is false:  
  - Pause via the same pattern as `StarCelebrationPauseGate`.  
  - Modal: Reward Book holds prizes until claimed; Trophy Room shows medals; badge pulses when ready.  
  - Dismiss → resume; set `rewardTutorialSeen = true`.  
- Later completions/claims: **never** pause for rewards.

### 8.4 `$` rain

- On single claim or Claim All, play `$` glyphs pouring into / erupting from the **bottom-bar money/savings** readout while the balance ticks up. Presentation-only.

### 8.5 Trophy Room burst

- When claim raises any subject metal, every placed Trophy Room plays a short light burst + stars shooting outward from the cutaway. No pause. Vanity only.

---

## 9. Trophy Room

- New placeable room (warm architectural dollhouse gallery; magenta key plate only; Art/Menu square icons).  
- Footprint ~4×1 or 6×1; mid build cost; **$0 income**; no sim buffs.  
- Buildable after lobby (empty pedestals OK).  
- Pedestals/plaques bind to `TrophyCase`; empty = silhouette; filled = metal art.  
- Multiple rooms mirror the **same** case.  
- Optional: agents path in and look-at emote only.  
- Demolish room ≠ lose trophies.

---

## 10. Save

Schema version remains **1**.

`RewardsSnapshotV1` optional on `TowerSnapshotV1`:

- `claimedIds[]`, `pendingIds[]`  
- Progress blobs for active duration holds / contract counters as needed  
- `trophies[]` — subject → metal  
- `rewardTutorialSeen`  
- `contracts[]` — offer id, progress, expiry day  
- `extrasInventory[]` — token type, remaining charges/expiry  

Absent/null snapshot → empty ledger, no trophies, tutorial not seen, empty board, empty extras (same spirit as missing cleanliness → defaults).

---

## 11. Integration points

| Hook | Use |
|------|-----|
| Day/hour roll | Duration holds, contract expiry, weekly board refresh |
| Grid build / demolish | Construction / demolition counters |
| Population / stars | Threshold milestones |
| Health / clean / quarantine / condemn | Ops + infamy |
| Wallet credit API | Claim payout only |
| `TowerNews` | Completion / expire / tutorial-adjacent copy |
| HUD bottom money rect | `$` rain anchor |
| Star pause gate pattern | First-trophy modal only |
| Room catalog + dollhouse | Trophy Room + trophy sprites |

---

## 12. Testing

EditMode (representative):

1. Meet condition → pending, wallet unchanged.  
2. Claim once → `$` applied, id claimed; second claim no-ops.  
3. ClaimAll claims each pending once; summed cash.  
4. Claim with metal M upgrades subject; lower M does not downgrade.  
5. DurationHold: N days in band completes; breach resets streak.  
6. First metal claim requests tutorial pause path and sets flag; second does not.  
7. Missing `RewardsSnapshotV1` loads clean defaults.  
8. Contract expire → removed, no pending.  
9. Trophy Room presenter maps case → pedestal slots; demolish room keeps case.  

FX (`$` rain, bursts) may be smoke-tested in Play Mode; logic must not depend on particles.

---

## 13. Art notes

- Trophy metals: distinct colors — bronze, silver, gold, platinum (cool metal), diamond (icy), rainbow (gradient), galactic (deep space / ornate).  
- Escalating extravagance per tier.  
- Trophy Room: wood/stone gallery, industrial-warm dollhouse — not pink/toy.  
- Menu icon: dedicated square Art/Menu asset.  
- Shared trophy sprites between book UI and room cutaway.

---

## 14. Non-goals (restate)

No Trophy Room gameplay buffs. No auto-claim. No schema bump. No Steam sync. No burying star Goals inside the Reward Book.
