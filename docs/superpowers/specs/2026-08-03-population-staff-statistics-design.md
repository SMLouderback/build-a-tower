# Build-A-Tower — Population & Staff Statistics HUD (backlog)

**Date:** 2026-08-03  
**Status:** Backlog — not implemented  
**Depends on:** Agent roles, hotel occupancy, commercial visits, conference/events, staffed services  
**Parent roadmap:** evaluation/heatmaps → polish (after deeper economy / stars / ops)

## 1. Intent

Today the Economy HUD shows a single **Population** total (condo move-ins + office workers + hotel guests **currently in the tower**). Players will eventually want a **breakdown** so they can see who is actually in the building and how staffing scales.

## 2. Proposed statistics (later slice)

Expose read-only counts (HUD panel, Selection, or Economy expand) along these axes:

| Bucket | Source |
|--------|--------|
| Condo residents | `CondoResident` with `HasMovedIn` |
| Office workers | `OfficeWorker` (assigned; may be Outside between shifts) |
| Hotel guests | `HotelGuest` currently in-tower (`Phase != Outside`) |
| Street walk-ins | `StreetVisitor` (shops) |
| Event visitors | `EventVisitor` (majors / halls) |
| Other amenities | Future room types that spawn day traffic |
| Staff total | Maid + Handyman + Security (+ Researchers if shown as staff) |
| Staff breakdown | Counts per role / per service room |

Optional later: peak-of-day snapshots, overnight vs daytime occupancy, vacancy vs capacity for hotels/offices/condos.

## 3. Non-goals for this note

- Implementing the UI or aggregators now.
- Changing star gates to use the breakdown (stars continue to use the single Population total unless a future design says otherwise).

## 4. Related live rules (already shipped)

- Hotel **beds** reserve `HotelGuest` agents, but **Population** ignores guests while `Outside` (between stays).
- Staff, street visitors, event visitors, and criminals never count toward star Population.
