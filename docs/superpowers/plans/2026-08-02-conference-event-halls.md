# Conference / Event Halls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Conference daily meetings from office workers, 4★ + Event Halls with major events and hybrid attendees, plus tower news banner/ticker.

**Architecture:** `StarSystem` unlocks 4★ (staffed Security). `ConferenceSystem` owns daily meeting math, major scheduling/booking/payouts, and attendee spawn targets. `TowerNews` is a priority feed; HUD renders banner + ticker. `EventVisitor` agents provide foot traffic; hotel-backed fraction uses vacant beds.

**Tech Stack:** Unity 6000.x, C#, NUnit EditMode, existing EconomySystem / AgentSystem / TowerHudController

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-02-conference-event-halls-design.md`
- Conference @ **3★** (~8×1); Event Hall @ **4★** (~12×2); no hall staff MVP
- Daily demand = **office worker agent count**; Event Halls **no** daily meetings
- Major payout = hotel guests × stars × booked capacity × mult; daily paused **only on booked halls**
- Attendees = hybrid day crowd + hotel fraction; hard concurrency caps
- `MaxStars = 4`; 4★ = pop **100**, stress **≤15**, staffed Security
- News: MajorEvent + OpsSerious + Quirk; banner for majors; ticker for all
- Do not commit `.superpowers/sdd/*` or `Assets/_Recovery/`
- Branch: `feature/conference-event-halls`

## File map

| File | Role |
|------|------|
| `Assets/Scripts/Economy/StarSystem.cs` | MaxStars=4; 4★ criteria |
| `Assets/Scripts/Data/RoomTypeSO.cs` | `eventCapacity` field |
| `Assets/Resources/Rooms/Conference.asset` (+ SO copy) | 8×1, capacity, 3★ |
| `Assets/Resources/Rooms/EventHall.asset` (new) | 12×2, 4★ |
| `Assets/Scripts/Economy/TowerNews.cs` | News feed |
| `Assets/Scripts/Economy/ConferenceSystem.cs` | Meetings, majors, booking |
| `Assets/Scripts/Economy/ConferenceMath.cs` | Pure payout helpers (testable) |
| `Assets/Scripts/Agents/AgentEnums.cs` | `EventVisitor` |
| `Assets/Scripts/Agents/AgentSystem.cs` | Spawn/despawn event visitors |
| `Assets/Scripts/Economy/EconomySystem.cs` / `TowerSimulation.cs` | Midnight + tick wire |
| `Assets/Scripts/UI/TowerHudController.cs` | Banner, ticker, Selection, build button |
| `Assets/Tests/EditMode/StarSystemTests.cs` | 4★ |
| `Assets/Tests/EditMode/ConferenceSystemTests.cs` | Meetings + majors |
| `Assets/Tests/EditMode/TowerNewsTests.cs` | Feed |
| `Assets/Tests/EditMode/RoomTypeAssetTests.cs` | Assets |
| `README.md` | Play notes |

---

### Task 1: StarSystem 4★

**Files:**
- Modify: `Assets/Scripts/Economy/StarSystem.cs`
- Modify: `Assets/Tests/EditMode/StarSystemTests.cs`

**Interfaces:**
```csharp
public const int FourStarPopulation = 100;
public const float FourStarMaxStress = 15f;
public const int MaxStars = 4; // was 3
// MeetsCriteria(4): pop/stress + HasStaffedSecurity(grid)
// FormatNextStarGoal includes 4★ security line
```

- [ ] **Step 1: Failing tests**

```csharp
[Test]
public void TryPromote_to_four_requires_staffed_security_and_thresholds()
{
    var stars = new StarSystem();
    stars.ForceStars(3);
    var grid = GridReadyForThreeStars(); // lobby, elevator, HK+Maint operational
    // pop 100, stress 10, no security → false
    Assert.IsFalse(stars.TryPromote(grid, 10f, 100));
    // place security with StaffedWorkers=1 → true → CurrentStars==4
}
```

- [ ] **Step 2: Implement 4★ criteria + FormatNextStarGoal**
- [ ] **Step 3: Tests PASS; Commit** `feat: unlock 4th star with staffed security`

---

### Task 2: Room assets + eventCapacity

**Files:**
- Modify: `Assets/Scripts/Data/RoomTypeSO.cs` — add `[Min(0)] public int eventCapacity;`
- Modify: Conference assets (Resources + ScriptableObjects) — size 8×1, cost tune, eventCapacity e.g. **40**, requiredStars 3
- Create: `EventHall.asset` both folders — id `service_event_hall`, displayName `Event Hall`, size 12×2, buildCost e.g. **150000**, requiredStars **4**, eventCapacity e.g. **120**, Service/Utility
- Modify: `RoomTypeAssetTests.cs`, `TowerHudController` AddRoomButton for EventHall
- Update README unlock line

- [ ] **Step 1: Tests for asset ids/stars/sizes**
- [ ] **Step 2: Assets + SO field + HUD button**
- [ ] **Step 3: Commit** `feat: conference resize and event hall room`

---

### Task 3: TowerNews feed

**Files:**
- Create: `Assets/Scripts/Economy/TowerNews.cs`
- Test: `Assets/Tests/EditMode/TowerNewsTests.cs`

**Interfaces:**
```csharp
public enum TowerNewsCategory { MajorEvent, OpsSerious, Quirk }

public sealed class TowerNewsItem
{
    public TowerNewsCategory Category;
    public int Priority; // higher first
    public string Text;
    public int CreatedDayIndex;
    public int ExpireDayIndex; // inclusive; drop after
}

public sealed class TowerNews
{
    public const int MaxItems = 32;
    public void Push(TowerNewsItem item);
    public IReadOnlyList<TowerNewsItem> Items { get; }
    public void Prune(int currentDayIndex);
    public TowerNewsItem PeekBannerCandidate(); // highest Priority MajorEvent not expired, or null
    public IReadOnlyList<TowerNewsItem> TickerOrder(); // MajorEvent/OpsSerious before Quirk
}
```

- [ ] **Step 1–4: TDD + implement + commit** `feat: add tower news feed`

---

### Task 4: ConferenceMath + daily meetings

**Files:**
- Create: `Assets/Scripts/Economy/ConferenceMath.cs`
- Create: `Assets/Scripts/Economy/ConferenceSystem.cs` (daily portion)
- Modify: `EconomySystem` or `TowerSimulation` midnight to credit meetings
- Test: `ConferenceSystemTests.cs`

**Interfaces:**
```csharp
public static class ConferenceMath
{
    public const int MeetingPayPerOfficeWorker = 15; // $ per worker per day baseline before split
    public const float MeetingStarsFactor = 0.25f; // +25% per star above 0 → (1 + stars * factor)
    // Daily for one hall when demand is shared:
    public static int DailyMeetingPayout(
        int officeWorkerCount,
        int hallCapacity,
        int totalEligibleCapacity,
        int stars,
        float climateSpendMult);
    // If totalEligibleCapacity<=0 return 0
    // share = hallCapacity / totalEligibleCapacity
    // raw = officeWorkerCount * MeetingPayPerOfficeWorker * (1 + stars * MeetingStarsFactor) * climateSpendMult
    // return RoundToInt(raw * share) capped somehow by capacity (optional: min(raw*share, hallCapacity * 50))
}

public sealed class ConferenceSystem
{
    public const string ConferenceId = "service_conference";
    public const string EventHallId = "service_event_hall";
    public HashSet<int> BookedHallInstanceIds { get; } // filled in Task 5
    public int ComputeDailyMeetings(
        TowerGrid grid,
        int officeWorkerCount,
        int stars,
        float climateSpendMult);
    public bool IsHallBooked(RoomInstance room);
}
```

- [ ] **Step 1: Unit tests for share + booked skip**
- [ ] **Step 2: Implement math + ConferenceSystem daily**
- [ ] **Step 3: Wire midnight credit into EconomySystem.ApplyMidnight (or sim) with office worker count from AgentSystem**
- [ ] **Step 4: Commit** `feat: daily conference meeting income from offices`

---

### Task 5: Major events schedule + payout + news

**Files:**
- Modify: `ConferenceSystem.cs`
- Modify: `TowerSimulation.cs` daily tick
- Test: extend `ConferenceSystemTests.cs`

**Interfaces:**
```csharp
public enum MajorEventPhase { None, Upcoming, Live, Ended }

public sealed class MajorEventState
{
    public string Name; // e.g. "TowerCon"
    public MajorEventPhase Phase;
    public int StartDayIndex;
    public int EndDayIndex; // exclusive or inclusive — pick inclusive end day
    public List<int> BookedHallInstanceIds;
}

public sealed class ConferenceSystem
{
    public const int EventForeshadowDays = 2;
    public const int EventMinGapDays = 14;
    public const int EventMaxGapDays = 21;
    public const float EventPayMult = 8f;
    public const float EventDailyWhileLiveMult = 0.15f; // of lump, optional small daily

    public MajorEventState Active { get; }
    public void TickDay(
        int dayIndex,
        TowerGrid grid,
        int hotelGuestCount,
        int stars,
        float climateSpendMult,
        FundsWallet wallet,
        TowerNews news,
        System.Random rng);
    // Schedule next if none and Event Hall exists
    // On foreshadow day: news upcoming; Phase=Upcoming
    // On start: book halls, lump payout, Phase=Live, news live
    // Each live day: optional small daily; spawn hook for Task 6
    // On end+1: clear bookings, Phase=None, news ended
    public static int MajorEventLumpPayout(int hotelGuests, int stars, int bookedCapacity, float climateMult);
}
```

Booking: among non-broken Event Halls, sort by eventCapacity desc, book until at least one (MVP: book **all** available Event Halls or top hall only — **book highest-capacity single hall** for MVP simplicity).

- [ ] **Step 1: Tests — lump formula; daily meetings 0 for booked instance; schedule foreshadow**
- [ ] **Step 2: Implement**
- [ ] **Step 3: Commit** `feat: major events book halls and pay lump sum`

---

### Task 6: EventVisitor agents

**Files:**
- Modify: `AgentEnums.cs` — `EventVisitor`
- Modify: `AgentSystem.cs` — spawn/despawn; day path to hall/shops; hotel fraction
- Modify: `AgentView.cs` — distinct color
- Modify: `ConferenceSystem` / sim — request spawn counts per day while Live
- Test: focused EditMode if possible (spawn cap / role); else smoke via helpers

**Constants:**
```csharp
public const int MaxConcurrentEventVisitors = 24;
public const float EventHotelBookFraction = 0.25f;
// SpawnPerDay ≈ min(MaxConcurrent, bookedCapacity / 5)
```

Behavior MVP:
- Day visitors: Outside → BeginTrip to Event Hall cell → dwell → optional shop → Outside (mirror street visitor patterns).
- Hotel fraction: if vacant hotel bed, assign like temporary guest for event nights; on event end force checkout/despawn.
- `IsEphemeralOrStaffRole` includes EventVisitor for SyncHomes skip.
- Crime/shop systems already count floor traffic / visitors — ensure EventVisitors count where StreetVisitor does if applicable.

- [ ] **Step 1–4: Implement + commit** `feat: spawn event visitors for major events`

---

### Task 7: HUD banner + ticker + Selection

**Files:**
- Modify: `TowerHudController.cs`
- Optional: small `TowerNewsHud` helper if HUD file too large

**Behavior:**
- Each frame/tick: `news.Prune(day)`; if `PeekBannerCandidate()` new id → show banner N seconds (`BannerSeconds = 6`)
- Ticker: scroll `TickerOrder()` texts
- Selection on Conference/Event Hall: estimated daily (if conference) or “Booked: TowerCon through day X”
- Ops pushers (lightweight): once per day if dirty hotel count high / avg crime high / avg stress high → `TowerNews.Push` OpsSerious (can live in ConferenceSystem.TickDay or AgentSystem/Economy midnight)

- [ ] **Step 1: Wire HUD (manual Play Mode); unit-test news ordering already in Task 3**
- [ ] **Step 2: Commit** `feat: tower news banner and ticker HUD`

---

### Task 8: Docs + spec status

- Spec Status → **Implemented (Play Mode pending)**
- README: Conference/Event Hall, 4★, meetings, majors, news ticker
- Commit: `docs: mark conference event halls implemented`

---

## Spec coverage

| Spec | Task |
|------|------|
| 4★ + Security | 1 |
| Room sizes / Event Hall | 2 |
| Daily meetings / office pop | 4 |
| Majors / booking pause / payout | 5 |
| Hybrid attendees | 6 |
| Banner + ticker + ops lines | 3, 7 |
| README / status | 8 |

## Execution

User approved spec and asked to proceed. Use **Subagent-Driven Development** (do not ask inline vs SDD). Continue on `feature/conference-event-halls`.
