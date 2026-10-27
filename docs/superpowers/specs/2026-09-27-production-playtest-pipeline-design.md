# Build-A-Tower Production Playtest Pipeline Design

**Date:** 2026-09-27  
**Status:** Approved (brainstorming)  
**Does not replace:** local `.batsave`, Account/cloud-save client, or the live API at `https://api.escapeproductions.biz`  
**Related:** `2026-09-26-cloud-save-live-api-design.md` (Account ships as-is; this pass does not depend on that API being up)

## Goal

Friends and family play a frozen Windows production build of Build-A-Tower while development continues in Git and the Unity Editor. Their copy changes only when a production publish is pushed. They get the zip from a public Download on `https://escapeproductions.biz` using a shared key, see an in-game notice when a newer zip is available, and can send Feedback that emails the studio.

## Locked decisions

| Decision | Choice |
|----------|--------|
| Who plays production | Friends and family (and anyone you later give the shared key) |
| Isolation | Testers run a published Windows zip only. Editor Play and Git branches never replace that zip |
| How they get the game | Public **Download** button on `escapeproductions.biz` |
| Access | One shared family key you generate; required to download, **not** to play |
| Key rotation | You rotate when you want to stop new downloads. Existing zips keep working |
| Request a Key | Public form: email only. Waitlist — site does **not** send them the key. You decide whether to reply |
| Updates | In-game notice when the hosted version is newer; they re-download the zip. No auto-update |
| Feedback | In-game button (main menu + pause). Server emails `escapemobileproductions@gmail.com` |
| Account / cloud | Ship as-is. Local save always works. Cloud works only if the API is up and they are signed in |
| How you publish | Unity Windows 64-bit build on your PC, then a deliberate publish upload. No auto-deploy from Git |
| Hosting | Release sidecar next to the marketing site on `edge-proxy`, **not** on the cloud-save API |
| Platforms this pass | Windows 64-bit standalone zip only |

## Isolation and the tester loop

Testers never open the Unity project. A **production release** is three live artifacts on the site host:

1. The Windows zip  
2. A public version document  
3. The current family-key secret (hash only on disk)

Day-to-day feature work stays on Git branches and in the Editor. That work cannot reach testers until you build from a chosen commit and publish.

Their loop: open Download → enter the shared key **or** Request a Key → (if keyed) get the zip → play. Local `.batsave` works as today. When you publish a newer version, the running build notices and points them at the Download page. They may dismiss the notice and keep playing. Feedback emails you with the running build version attached.

## Site: Download and Request a Key

The Build-A-Tower card on `escapeproductions.biz` stops being “coming soon / get notified” as the primary action. It exposes a public **Download** control.

**I have a key.** They submit the shared key. Success starts the Windows zip download and shows the current production version string. Failure (empty or wrong) returns a generic **invalid key** — no indication the key was close, no zip.

**Request a Key.** No key required. They submit an email. Missing or invalid email is rejected on the form and does not email you. A valid request emails you their address (same inbox as Feedback) so you can see interest and, if you want, reply with the family key. The site never includes the key in that auto-mail.

The version document is public so the game can check for updates without a key. The zip is not a stable guessable public path; a successful key check **streams the zip** in that response. There is no second public file URL.

No per-person keys, no expiry, no account required to download.

## Release sidecar (edge-proxy)

A small service beside the static marketing site owns:

| Surface | Auth | Behavior |
|---------|------|----------|
| `GET` public version document | None | Current production `version` (semver), `releasedAt` (UTC), `downloadPage` URL |
| Keyed zip download | Shared family key | Constant-time compare against stored hash; success returns the zip |
| `Request a Key` | None (rate limited) | Validate email; email operator; do not send the key |
| `Feedback` | None (rate limited) | Require non-empty note; email operator with note, optional name/email, and game version |

Operator controls (LAN/Tailscale or SSH on `edge-proxy`): set or rotate the family key; the plaintext key is never stored — only a one-way hash. A normal publish does **not** rotate the key.

Mail goes to `escapemobileproductions@gmail.com` via configured SMTP. Missing SMTP must not take down version checks or keyed downloads; Request-a-Key and Feedback then fail with a support-safe error (form/game shows they could not send).

Rate limits apply per client address on Request-a-Key and Feedback so a bot cannot flood the inbox. Wrong-key attempts are also rate limited.

The sidecar does not call `api.escapeproductions.biz`. Cloud save remaining down must not block Download, version checks, or Feedback.

## In-game: update notice and Feedback

Production builds use Unity `Application.version` as the running semver (set at publish time to match the version document).

On launch, and again when pause opens, the build fetches the public version document.

- Hosted version is **newer** (semver compare): show a notice with the new version number and that they should get the zip from the Download page (`https://escapeproductions.biz/` download section). Dismiss keeps the current session.  
- Same or older hosted version: no notice.  
- Offline or fetch failure: no error banner; they play.

The game never downloads the zip and never asks for the family key.

**Feedback** is a button on the main menu and on pause. They type a note (required), optionally name and email, and send. The client POSTs to the sidecar. Success confirms sent. Failure or offline keeps the draft and shows a short **couldn’t send**. Feedback does not require the download key.

Account/cloud UI is unchanged.

## How you publish

1. Choose the commit that should be production.  
2. Set the project version to the new semver.  
3. File → Build Settings → Windows 64-bit standalone.  
4. Run the publish step on the same PC: zip the player, upload it as the new production zip, write the public version document.

Only after a successful upload do testers see an update notice. If the upload fails, the previous zip and version document stay live.

The family key is unchanged by publish. Rotate it only when you want to cut off new downloads.

There is no GitHub Actions / CI Unity build in this pass.

## Errors and abuse

| Case | Result |
|------|--------|
| Wrong or empty download key | Generic invalid key; no zip |
| Request-a-Key missing/junk email | Form error; no operator mail |
| Feedback empty note | Rejected in the game; no POST |
| Version check offline | Silent; play continues |
| Feedback offline or 5xx | Draft kept; “couldn’t send” |
| SMTP down | Downloads and version still work; Request-a-Key and Feedback fail closed |
| Key rotated | New downloads need the new key; existing zips still play |
| Old `.batsave` on a newer zip | Same compatibility as opening a newer Editor; no extra migration in this pass |

The shared key is never listed on the page, in the version document, or in the game.

## Out of scope

- Per-person keys or automatic emailing of the family key  
- In-game license / key check to *play*  
- Auto-updating or patching the player  
- Steam, itch.io, macOS, WebGL  
- Git-tag CI Unity builds  
- Public changelog beyond the version number in the notice  
- Marketing trailers or a full store page beyond Download + Request a Key  
- Changing cloud-save invite policy or deploying the API VM (unless already done independently)

## Architecture boundaries

| Unit | Does | Depends on |
|------|------|------------|
| Marketing site Download UI | Key entry, Request-a-Key form, version display | Sidecar endpoints |
| Playtest sidecar | Key verify, zip serve, version document, mail | SMTP, key hash, zip on disk |
| Publish script (your PC) | Zip + upload + version write | SSH to `edge-proxy` (`192.168.0.35`) and replace zip + version on disk, then the sidecar serves the new files |
| Unity `PlaytestUpdateClient` | Fetch version; compare semver | Public version URL |
| Unity Feedback UI | Draft + POST | Sidecar Feedback URL |
| Account / `SyncCoordinator` | Unchanged | Cloud API if present |

Editor Play may run the same update/feedback code; a failed version fetch must stay silent so local work is unaffected.

## Success

- A tester with the key can download and play without Unity.  
- Feature commits that are not published do not change that zip.  
- After a successful publish, running production builds show the new version in the notice.  
- Request-a-Key and Feedback arrive as email without sending testers the key automatically.  
- Cloud-save API down does not block download, play, local save, version notice (if the site is up), or the Feedback *attempt* (send fails closed if SMTP/sidecar is down).
