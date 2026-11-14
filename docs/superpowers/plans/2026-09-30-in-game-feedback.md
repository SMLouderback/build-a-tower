# In-Game Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Feedback on the main menu and pause menu so playtesters can email Escape Gmail (optional contact for replies) or open Facebook.

**Architecture:** Reuse live `POST /playtest/feedback`. Add a small Unity `PlaytestFeedbackClient` + UIToolkit main-menu panel and IMGUI pause panel. Facebook is `Application.OpenURL` only. No new server work; SMTP already configured.

**Tech Stack:** Unity 6000.4.7f1, C#, `HttpClient` (same pattern as `AccountClient`), UIToolkit main menu, IMGUI pause, NUnit EditMode

## Global Constraints

- Feedback URL: `https://escapeproductions.biz/playtest/feedback`
- Facebook URL: `https://www.facebook.com/EScapeMProd`
- Note required; Name and Email optional; version from `Application.version`
- Copy: “Optional — only if you want a reply.” / “Write a note first.” / “Enter a valid email or leave it blank.” / “Thanks — sent.” / “Couldn't send.” / “Too many tries. Wait a minute.”
- No download key for Feedback; no mailto fallback; no update-notice work in this plan
- Unity: prefer Pipeline `unity command` when Editor is open; else batchmode EditMode
- TDD: failing EditMode tests before implementation

---

### Task 1: PlaytestFeedbackClient

**Files:**
- Create: `Assets/Scripts/Playtest/PlaytestConfig.cs`
- Create: `Assets/Scripts/Playtest/PlaytestFeedbackClient.cs`
- Create: `Assets/Tests/EditMode/PlaytestFeedbackClientTests.cs`
- Test: EditMode filter `PlaytestFeedbackClientTests`

**Interfaces:**
- Consumes: `POST /playtest/feedback` JSON `{ message, name, email, version }` → 204 / 400 / 429 / 5xx
- Produces:
  - `PlaytestConfig` with `BaseUrl` default `https://escapeproductions.biz`
  - `PlaytestFeedbackDraft` with `Message`, `Name`, `Email`, `Version`
  - `PlaytestFeedbackSendKind`: `Sent`, `RejectedEmpty`, `RejectedEmail`, `FailedKeepDraft`, `RateLimited`
  - `PlaytestFeedbackClient.SendAsync(PlaytestFeedbackDraft, CancellationToken)` → result with `Kind`
  - Local email check: if email non-empty after trim, must contain `@` with text on both sides and a `.` after `@` (same spirit as server `EmailValidation`; keep client check simple)

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public async Task Empty_note_does_not_post()
{
    var posted = false;
    var handler = new StubHandler(_ => { posted = true; return new HttpResponseMessage(HttpStatusCode.NoContent); });
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz/")
    });
    var result = await client.SendAsync(new PlaytestFeedbackDraft { Message = "  ", Version = "1.0.1" });
    Assert.AreEqual(PlaytestFeedbackSendKind.RejectedEmpty, result.Kind);
    Assert.IsFalse(posted);
}

[Test]
public async Task Invalid_email_does_not_post()
{
    var posted = false;
    var handler = new StubHandler(_ => { posted = true; return new HttpResponseMessage(HttpStatusCode.NoContent); });
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz/")
    });
    var result = await client.SendAsync(new PlaytestFeedbackDraft
    {
        Message = "Hi",
        Email = "not-an-email",
        Version = "1.0.1"
    });
    Assert.AreEqual(PlaytestFeedbackSendKind.RejectedEmail, result.Kind);
    Assert.IsFalse(posted);
}

[Test]
public async Task Success_posts_json_and_returns_Sent()
{
    HttpRequestMessage seen = null;
    var handler = new StubHandler(req =>
    {
        seen = req;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    });
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz/")
    });
    var result = await client.SendAsync(new PlaytestFeedbackDraft
    {
        Message = "Stairs feel slow",
        Name = "Pat",
        Email = "pat@example.com",
        Version = "1.0.1"
    });
    Assert.AreEqual(PlaytestFeedbackSendKind.Sent, result.Kind);
    Assert.IsNotNull(seen);
    Assert.AreEqual(HttpMethod.Post, seen.Method);
    Assert.AreEqual("/playtest/feedback", seen.RequestUri.AbsolutePath);
}

[Test]
public async Task Offline_keeps_draft_message()
{
    var handler = new StubHandler(_ => throw new HttpRequestException("offline"));
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz/")
    });
    var draft = new PlaytestFeedbackDraft { Message = "Hello", Version = "1.0.1" };
    var result = await client.SendAsync(draft);
    Assert.AreEqual(PlaytestFeedbackSendKind.FailedKeepDraft, result.Kind);
    Assert.AreEqual("Hello", draft.Message);
}

[Test]
public async Task Rate_limit_maps_to_RateLimited()
{
    var handler = new StubHandler(_ => new HttpResponseMessage((HttpStatusCode)429));
    var client = new PlaytestFeedbackClient(new PlaytestConfig(), new HttpClient(handler)
    {
        BaseAddress = new Uri("https://escapeproductions.biz/")
    });
    var result = await client.SendAsync(new PlaytestFeedbackDraft { Message = "Hi", Version = "1.0.1" });
    Assert.AreEqual(PlaytestFeedbackSendKind.RateLimited, result.Kind);
}
```

Include a tiny `StubHandler : HttpMessageHandler` in the test file (same idea as AccountClient tests if present).

- [ ] **Step 2: Run EditMode filter `PlaytestFeedbackClientTests` — expect FAIL (types missing)**

- [ ] **Step 3: Implement config + client**

`PlaytestConfig.BaseUrl` default `https://escapeproductions.biz`.  
`SendAsync`: trim message; empty → `RejectedEmpty`; if email trimmed non-empty and fails simple validation → `RejectedEmail`; else POST JSON; 204/200 → `Sent`; 429 → `RateLimited`; else / exception → `FailedKeepDraft`. Never clear draft fields inside the client.

- [ ] **Step 4: Re-run EditMode — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Playtest Assets/Tests/EditMode/PlaytestFeedbackClientTests.cs
git commit -m "feat: add PlaytestFeedbackClient for in-game feedback"
```

---

### Task 2: Main menu Feedback panel

**Files:**
- Modify: `Assets/Scripts/UI/MainMenu.uxml`
- Modify: `Assets/Scripts/UI/MainMenu.uss` (only if needed for multiline / status)
- Modify: `Assets/Scripts/UI/MainMenuController.cs`
- Create: `Assets/Tests/EditMode/PlaytestMenuFlowTests.cs`
- Test: EditMode filter `PlaytestMenuFlowTests`

**Interfaces:**
- Consumes: `PlaytestFeedbackClient.SendAsync`, `Application.version`, `Application.OpenURL`
- Produces: `btn-feedback` on `panel-root` (after Account, before Contact); `panel-feedback` with note / name / email / status / Send / Facebook / Back; public methods if useful for tests: `ShowFeedbackPanel()`, `TrySendFeedback()` or drive via Bind + queries

- [ ] **Step 1: Write failing menu flow tests**

```csharp
[Test]
public void Feedback_button_opens_panel()
{
    CreateBoundMenu(out var root);
    Assert.IsNotNull(root.Q<Button>("btn-feedback"));
    var panel = root.Q("panel-feedback");
    Assert.IsTrue(panel.ClassListContains("hidden"));
    // Call controller.ShowFeedbackPanel() or simulate click registration path
    // Assert panel visible, panel-root hidden
}

[Test]
public async Task Empty_send_shows_write_a_note_first()
{
    // Bind menu with injectable client stub that fails if called
    // Clear note, invoke Send path
    // Assert status text contains "Write a note first"
}
```

Reuse `CreateBoundMenu` pattern from `MainMenuLoadFlowTests` (clone `MainMenu.uxml`, `Bind`). Inject a fake/stub client via a test seam on `MainMenuController` (e.g. `FeedbackClient` property or `ConfigureFeedback(PlaytestFeedbackClient)`).

- [ ] **Step 2: Run filter — expect FAIL**

- [ ] **Step 3: Implement UXML + controller**

UXML `panel-feedback` (hidden by default):
- Title Feedback
- Multiline note field `feedback-note`
- Name `feedback-name`, Email `feedback-email`
- Hint label: Optional — only if you want a reply.
- Status `feedback-status`
- Buttons: `btn-feedback-send`, `btn-feedback-facebook` (Message on Facebook), `btn-feedback-back`

Wire ShowOnly to include `_panelFeedback`. Send sets version from `Application.version`, maps result kinds to status copy. Facebook: `Application.OpenURL("https://www.facebook.com/EScapeMProd")`. Success clears note only.

- [ ] **Step 4: Re-run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/MainMenu.uxml Assets/Scripts/UI/MainMenu.uss Assets/Scripts/UI/MainMenuController.cs Assets/Tests/EditMode/PlaytestMenuFlowTests.cs
git commit -m "feat: add main menu Feedback panel"
```

---

### Task 3: Pause menu Feedback

**Files:**
- Modify: `Assets/Scripts/UI/TowerHudController.cs`
- Modify: `Assets/Tests/EditMode/PlaytestMenuFlowTests.cs` (or add pause-focused tests in same file)
- Test: EditMode filter covering pause Feedback open + empty reject copy

**Interfaces:**
- Consumes: same client; Esc back from Feedback → Paused (like Account)
- Produces: pause root button **Feedback** between Account and Resume; `PauseUiState.Feedback` panel with note/name/email/Send/Facebook/Back

- [ ] **Step 1: Write failing pause tests**

Assert via reflection/helpers already used in `MainMenuLoadFlowTests` (`SetPauseUi` / `GetPauseUi`) or public test hooks: opening Feedback sets state; empty send does not call network (inject client). Prefer a small public `OpenPauseFeedbackForTests()` / `PauseFeedbackStatusText` if reflection is brittle.

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement pause UI**

Add `PauseUiState.Feedback`. Increase `panelH` for Feedback and for the extra root button. Draw fields with `GUI.TextField` / `GUI.TextArea`. Same status strings as main menu. Esc from Feedback → Paused (extend existing Esc handling next to Account).

- [ ] **Step 4: Re-run — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/TowerHudController.cs Assets/Tests/EditMode/PlaytestMenuFlowTests.cs
git commit -m "feat: add pause menu Feedback panel"
```

---

### Task 4: Manual smoke + publish note

**Files:** none required (optional README one-liner if Feedback is undocumented)

- [ ] **Step 1: In Editor Play Mode** — main menu Feedback Send with a real note + optional email; confirm Escape Gmail. Pause Feedback Facebook opens the page.

- [ ] **Step 2: If publishing playtest zip** — bump version, Windows build, `PlaytestApi/deploy/publish.ps1` (separate deliberate publish; not required to merge the feature).

- [ ] **Step 3: Commit only if docs changed**

---

## Self-review

- Spec decisions covered: pause + main menu, typed send, Facebook, optional contact, no mailto fallback, no update notice.
- Types consistent: `PlaytestFeedbackSendKind`, draft fields, URL paths.
- Server unchanged; client-only plan.
