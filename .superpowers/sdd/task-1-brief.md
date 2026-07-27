### Task 1: Bootstrap Unity 2D project in repo root

**Files:**
- Create: Unity project files under `Build-A-Tower/` (`Assets/`, `Packages/`, `ProjectSettings/`)
- Create: `Build-A-Tower/.gitignore` (Unity template)
- Keep: existing `docs/`, `README.md`, `.git/`

**Interfaces:**
- Consumes: none
- Produces: openable Unity project at repo root with 2D Tilemap package

- [ ] **Step 1: Add Unity `.gitignore`**

Create `Build-A-Tower/.gitignore` with standard Unity ignores (`[Ll]ibrary/`, `[Tt]emp/`, `[Oo]bj/`, `[Bb]uild/`, `[Ll]ogs/`, `.vs/`, `*.csproj`, `*.sln`, etc.). Keep `Assets/`, `Packages/`, `ProjectSettings/` tracked.

- [ ] **Step 2: Create the Unity project into the repo folder**

Preferred: Unity Hub â†’ New Project â†’ **2D (Built-in Render Pipeline)** or **2D URP**, version **6000.4.7f1**, location parent `Escape`, name `Build-A-Tower` â€” if Hub refuses non-empty folder, create in a temp folder and copy `Assets`, `Packages`, `ProjectSettings` into the existing repo (do not overwrite `docs/` or `.git/`).

CLI alternative (empty Assets only):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe" `
  -batchmode -quit -createProject "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower-UnityTmp"
```

Then merge generated folders into `Build-A-Tower` and delete the temp project.

- [ ] **Step 3: Ensure 2D Tilemap packages**

In `Packages/manifest.json`, ensure at least:

```json
"com.unity.feature.2d": "2.0.1",
"com.unity.ugui": "2.0.0",
"com.unity.modules.uielements": "1.0.0"
```

(Exact versions may differ by Unity 6 template â€” use Hub template defaults if present. Must include Tilemap support.)

- [ ] **Step 4: Open project once and save empty scene path**

Open the project in Unity Editor, create folder `Assets/Scenes`, save scene as `Assets/Scenes/TowerSandbox.unity`, set it as the first enabled scene in Build Settings.

- [ ] **Step 5: Commit**

```powershell
cd "c:\OldPC\Importaint Docs\Work\Steve\Escape\Build-A-Tower"
git add .gitignore Packages ProjectSettings Assets
git commit -m "chore: bootstrap Unity 2D project for Build-A-Tower"
```

---

