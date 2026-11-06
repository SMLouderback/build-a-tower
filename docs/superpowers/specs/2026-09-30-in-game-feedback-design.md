# Build-A-Tower In-Game Feedback Design

**Date:** 2026-09-30  
**Status:** Approved (brainstorming)  
**Extends:** `2026-09-27-production-playtest-pipeline-design.md` (Feedback UI was planned; SMTP now live; Unity UI was not shipped)  
**Does not replace:** Contact Us on the main menu, Account/cloud save, or website Request a Key

## Goal

During playtest / early access, players can send feedback from the **main menu** and the **in-game pause menu**: a typed note emailed to Escape Gmail via the playtest API, optional contact fields so the studio can reply, and a Facebook button for Messenger / page contact.

## Locked decisions

| Decision | Choice |
|----------|--------|
| Where | Pause menu and main menu |
| Primary send | Typed note → `POST /playtest/feedback` → Escape Gmail (SMTP already configured) |
| Secondary | **Message on Facebook** opens `https://www.facebook.com/EScapeMProd` |
| Contact | Optional **Name** and **Email** so the studio can reply when provided |
| Version | Always attach `Application.version` on Send |
| Download key | Not required for Feedback |
| Mailto fallback | Out of scope for this pass (API + Facebook only) |

## Player flow

1. Open **Feedback** from pause (Esc while playing) or from the main menu.  
2. Enter a **Note** (required). Optionally enter **Name** and **Email**. Short copy near contact fields: “Optional — only if you want a reply.”  
3. **Send** — POST to the playtest sidecar. On success: “Thanks — sent.” Clear the note; keep name/email if filled.  
4. **Message on Facebook** — opens the Escape Productions Facebook page in the system browser (available even if Send fails).  
5. **Back** — return to pause root or main menu root.

Empty / whitespace-only note: “Write a note first.” No network call.  
If email is filled but invalid: “Enter a valid email or leave it blank.”  
Rate limited (429): “Too many tries. Wait a minute.” Draft kept.  
Offline / 5xx / timeout: “Couldn’t send.” Draft kept.

## Architecture

| Piece | Role |
|-------|------|
| Playtest API (live) | Existing `POST /playtest/feedback` `{ message, name, email, version }` → operator mail; empty message 400; SMTP/API failure 503; rate limit 5/IP/min |
| `PlaytestFeedbackClient` | Unity client: validate locally, POST JSON to `https://escapeproductions.biz/playtest/feedback`, map results to Sent / RejectedEmpty / RejectedEmail / FailedKeepDraft / RateLimited |
| Main menu | UIToolkit `btn-feedback` + `panel-feedback` (note, name, email, Send, Facebook, Back) |
| Pause menu | IMGUI `PauseUiState.Feedback` with the same fields/actions; Esc from Feedback returns to Paused |
| Facebook | `Application.OpenURL("https://www.facebook.com/EScapeMProd")` — no Facebook API |

No new server endpoints. No screenshot/save attachments. No in-game chat.

## Privacy

Contact fields are optional. The game does not persist feedback history beyond the current draft in the open panel. Operator mail body includes message, optional name/email, and version (as today).

## Errors (summary)

| Case | Result |
|------|--------|
| Empty note | Local reject; no POST |
| Invalid optional email | Local reject; no POST |
| 429 | Message + draft kept |
| Offline / 5xx | “Couldn’t send.” + draft kept; Facebook still works |
| SMTP down | Same as 5xx from API; downloads/version unaffected |

## Testing

- EditMode: client empty reject, invalid email reject, success mapping, failure keeps draft, rate limit mapping.  
- EditMode / UI wiring: Feedback opens from main menu Bind and pause state; empty Send does not clear draft.  
- Manual: Send once against live API; confirm Escape Gmail receives note + optional contact + version; Facebook button opens the page.

## Out of scope

- Mailto fallback when API fails  
- Attaching screenshots, saves, or logs  
- Per-player identity beyond optional name/email  
- Changing Request a Key or download-key behavior  
- In-game update notice (separate playtest task)

## Success criteria

- Feedback is visible on pause and main menu.  
- Send delivers mail to Escape with note, optional contact, and version.  
- Facebook opens the Escape page.  
- Offline/API failure keeps the draft and does not block play or Facebook.
