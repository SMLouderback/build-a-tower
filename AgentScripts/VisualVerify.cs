// Outside Assets/ — compiled in-memory via `unity command run_script` (no domain reload).
// Entry points for the Build-A-Tower visual verify loop.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BuildATower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VisualVerify
{
    const string DefaultScene = "Assets/Scenes/TowerSandbox.unity";
    const string DefaultOutDir = "Temp/visual-verify";

    /// <summary>Open TowerSandbox (edit mode). Args: [scenePath]</summary>
    public static string OpenSandbox(string scenePath = null)
    {
        if (EditorApplication.isPlaying)
            return "FAIL: stop play mode before open_scene (call editor_stop first)";

        var path = string.IsNullOrWhiteSpace(scenePath) ? DefaultScene : scenePath.Trim();
        if (!File.Exists(path) && !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
            return $"FAIL: scene not found: {path}";

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return $"OK: opened {path}";
    }

    /// <summary>
    /// While in Play Mode: force stars, top up wallet, place lobby + rooms, frame camera.
    /// Prefers <c>Temp/visual-verify/current-scenario.json</c> (written by the orchestrator)
    /// so PowerShell/CLI quoting of --args is unnecessary. Optional <paramref name="encoded"/>
    /// still accepts a JSON path or pipe encoding.
    /// </summary>
    public static string PlaceScenario(string encoded = null)
    {
        if (!EditorApplication.isPlaying)
            return "FAIL: enter play mode first (unity command editor_play)";

        if (string.IsNullOrWhiteSpace(encoded))
        {
            var drops = new[]
            {
                Path.Combine("Temp", "visual-verify", "current-scenario.json"),
                Path.Combine("Assets", "Temp", "visual-verify", "current-scenario.json"),
            };
            encoded = null;
            foreach (var drop in drops)
            {
                if (File.Exists(drop))
                {
                    encoded = Path.GetFullPath(drop);
                    break;
                }
            }

            if (encoded == null)
                return "FAIL: no encoded scenario and current-scenario.json missing under Temp/ or Assets/Temp/";
        }

        if (!TryParseEncoded(encoded, out var stars, out var funds, out var lobbyMin, out var lobbyMax,
                out var placements, out var label, out var parseError))
            return $"FAIL: {parseError}";

        var build = UnityEngine.Object.FindAnyObjectByType<BuildController>();
        if (build == null)
            return "FAIL: no BuildController in play mode (is TowerSandbox loaded?)";

        var sim = UnityEngine.Object.FindAnyObjectByType<TowerSimulation>();
        if (sim == null)
            return "FAIL: no TowerSimulation";

        // Wipe to a clean tower without leaving Play Mode.
        build.InitializeTower();
        sim = UnityEngine.Object.FindAnyObjectByType<TowerSimulation>();
        if (sim?.Stars != null)
            sim.Stars.ForceStars(stars);

        if (funds > 0)
            build.Wallet.RestoreBalance(funds);

        if (!build.TryPlaceLobby(lobbyMin, lobbyMax))
            return $"FAIL: TryPlaceLobby({lobbyMin},{lobbyMax}) rejected";

        var placed = new List<string>();
        var failed = new List<string>();
        foreach (var p in placements)
        {
            var type = ResolveRoomType(build, p.TypeId);
            if (type == null)
            {
                failed.Add($"{p.TypeId}@({p.X},{p.Y}): unknown type");
                continue;
            }

            if (sim.Stars != null && !sim.Stars.CanBuild(type))
            {
                failed.Add($"{p.TypeId}@({p.X},{p.Y}): needs {type.requiredStars}★ (have {sim.Stars.CurrentStars})");
                continue;
            }

            build.SetRoomType(type);
            if (!build.TryPlaceSelected(new Vector2Int(p.X, p.Y)))
            {
                failed.Add($"{p.TypeId}@({p.X},{p.Y}): CanPlace/TryPlace rejected");
                continue;
            }

            placed.Add($"{p.TypeId}@({p.X},{p.Y})");
        }

        FrameCameraOnGrid(build);
        // Nudge a repaint so dollhouse overlays settle before capture.
        build.GetType(); // keep build alive for analyzer
        var view = UnityEngine.Object.FindAnyObjectByType<TilemapTowerView>();
        if (view != null && build.Grid != null)
        {
            foreach (var room in build.Grid.Rooms)
                view.PaintRoom(room);
        }

        var sb = new StringBuilder();
        sb.Append("OK:");
        sb.Append(" label=").Append(label);
        sb.Append(" stars=").Append(sim.Stars?.CurrentStars ?? stars);
        sb.Append(" funds=").Append(build.Wallet.Balance);
        sb.Append(" placed=[").Append(string.Join(", ", placed)).Append(']');
        if (failed.Count > 0)
            sb.Append(" FAILED=[").Append(string.Join("; ", failed)).Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Write a small sidecar JSON next to the latest screenshot under Temp/visual-verify/latest/.
    /// </summary>
    public static string WriteManifest(string note = "")
    {
        // Prefer Assets/Temp (where capture_game_view writes) then project Temp/.
        var latestDir = Path.Combine("Assets", "Temp", "visual-verify", "latest");
        if (!Directory.Exists(latestDir))
            latestDir = Path.Combine("Temp", "visual-verify", "latest");
        Directory.CreateDirectory(latestDir);
        var screenshotPath = Path.Combine(latestDir, "game.png");
        var build = UnityEngine.Object.FindAnyObjectByType<BuildController>();
        var sim = UnityEngine.Object.FindAnyObjectByType<TowerSimulation>();
        var dir = latestDir;

        var rooms = new List<string>();
        if (build?.Grid != null)
        {
            foreach (var room in build.Grid.Rooms)
            {
                if (room?.Type == null) continue;
                rooms.Add(
                    $"{{\"id\":\"{Escape(room.Type.id)}\",\"x\":{room.Origin.x},\"y\":{room.Origin.y},\"w\":{room.Size.x},\"h\":{room.Size.y}}}");
            }
        }

        var json = new StringBuilder();
        json.Append('{');
        json.Append("\"screenshot\":\"").Append(Escape(screenshotPath)).Append("\",");
        json.Append("\"note\":\"").Append(Escape(note ?? "")).Append("\",");
        json.Append("\"stars\":").Append(sim?.Stars?.CurrentStars ?? -1).Append(',');
        json.Append("\"funds\":").Append(build?.Wallet?.Balance ?? -1).Append(',');
        json.Append("\"playMode\":").Append(EditorApplication.isPlaying ? "true" : "false").Append(',');
        json.Append("\"rooms\":[").Append(string.Join(",", rooms)).Append(']');
        json.Append('}');

        var manifestPath = Path.ChangeExtension(screenshotPath, ".manifest.json");
        if (string.IsNullOrEmpty(Path.GetExtension(screenshotPath)))
            manifestPath = screenshotPath + ".manifest.json";
        File.WriteAllText(manifestPath, json.ToString());
        return $"OK: {manifestPath}";
    }

    static void FrameCameraOnGrid(BuildController build)
    {
        var cam = Camera.main;
        if (cam == null || build?.Grid == null)
            return;

        var rooms = build.Grid.Rooms.Where(r => r?.Type != null && !r.Type.isScaffolding).ToList();
        if (rooms.Count == 0)
            return;

        var minX = rooms.Min(r => r.Origin.x);
        var maxX = rooms.Max(r => r.Origin.x + r.Size.x);
        var minY = rooms.Min(r => r.Origin.y);
        var maxY = rooms.Max(r => r.Origin.y + r.Size.y);
        var cx = (minX + maxX) * 0.5f;
        var cy = (minY + maxY) * 0.5f;
        var spanX = Math.Max(8f, maxX - minX + 4f);
        var spanY = Math.Max(6f, maxY - minY + 4f);
        var halfH = Math.Max(spanY * 0.55f, spanX * 0.35f);
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Clamp(halfH, 6f, 40f);
        var p = cam.transform.position;
        cam.transform.position = new Vector3(cx, cy, p.z);
    }

    static RoomTypeSO ResolveRoomType(BuildController build, string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        var registry = RoomTypeRegistry.Create(build, build.Grid);
        if (registry.TryResolve(typeId, out var typed))
            return typed;

        // Fallback: scan all loaded RoomTypeSO assets (tests do this too).
        return Resources.FindObjectsOfTypeAll<RoomTypeSO>()
            .FirstOrDefault(r => r != null && r.id == typeId);
    }

    static bool TryParseEncoded(
        string encoded,
        out int stars,
        out int funds,
        out int lobbyMin,
        out int lobbyMax,
        out List<Placement> placements,
        out string label,
        out string error)
    {
        stars = 0;
        funds = 5_000_000;
        lobbyMin = 0;
        lobbyMax = 20;
        placements = new List<Placement>();
        label = "scenario";
        error = null;

        if (string.IsNullOrWhiteSpace(encoded))
        {
            error = "empty scenario encoding";
            return false;
        }

        // If it looks like a file path, load it.
        var trimmed = encoded.Trim();
        if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(trimmed))
            return TryParseJsonFile(trimmed, out stars, out funds, out lobbyMin, out lobbyMax,
                out placements, out label, out error);

        // Pipe encoding: stars|funds|lobbyMin,lobbyMax|id:x,y;id:x,y|label
        var parts = trimmed.Split('|');
        if (parts.Length < 4)
        {
            error =
                "expected stars|funds|lobbyMin,lobbyMax|roomId:x,y;...|label  OR a .json scenario path";
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out stars))
        {
            error = "bad stars";
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out funds))
        {
            error = "bad funds";
            return false;
        }

        var lobbyBits = parts[2].Split(',');
        if (lobbyBits.Length != 2 ||
            !int.TryParse(lobbyBits[0], out lobbyMin) ||
            !int.TryParse(lobbyBits[1], out lobbyMax))
        {
            error = "bad lobbyMin,lobbyMax";
            return false;
        }

        foreach (var token in parts[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = token.LastIndexOf(':');
            if (colon <= 0)
            {
                error = $"bad placement '{token}'";
                return false;
            }

            var id = token.Substring(0, colon).Trim();
            var xy = token.Substring(colon + 1).Split(',');
            if (xy.Length != 2 ||
                !int.TryParse(xy[0], out var x) ||
                !int.TryParse(xy[1], out var y))
            {
                error = $"bad placement coords '{token}'";
                return false;
            }

            placements.Add(new Placement(id, x, y));
        }

        if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
            label = parts[4].Trim();

        return true;
    }

    static bool TryParseJsonFile(
        string path,
        out int stars,
        out int funds,
        out int lobbyMin,
        out int lobbyMax,
        out List<Placement> placements,
        out string label,
        out string error)
    {
        // Minimal hand-rolled parse so we don't need Newtonsoft in the in-memory compile.
        stars = 0;
        funds = 5_000_000;
        lobbyMin = 0;
        lobbyMax = 20;
        placements = new List<Placement>();
        label = Path.GetFileNameWithoutExtension(path);
        error = null;

        var text = File.ReadAllText(path);
        stars = ReadInt(text, "stars", 0);
        funds = ReadInt(text, "funds", 5_000_000);
        lobbyMin = ReadInt(text, "lobbyMin", 0);
        lobbyMax = ReadInt(text, "lobbyMax", 20);
        var labelVal = ReadString(text, "label");
        if (!string.IsNullOrEmpty(labelVal))
            label = labelVal;

        // placements: [{"type":"office_micro","x":0,"y":1}, ...]
        var idx = text.IndexOf("\"placements\"", StringComparison.Ordinal);
        if (idx < 0)
        {
            error = "json missing placements";
            return false;
        }

        var arrStart = text.IndexOf('[', idx);
        var arrEnd = text.IndexOf(']', arrStart);
        if (arrStart < 0 || arrEnd < 0)
        {
            error = "json placements array malformed";
            return false;
        }

        var arr = text.Substring(arrStart + 1, arrEnd - arrStart - 1);
        foreach (var chunk in arr.Split(new[] { '{' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!chunk.Contains("\"type\"") && !chunk.Contains("\"id\""))
                continue;
            var type = ReadString("{" + chunk, "type");
            if (string.IsNullOrEmpty(type))
                type = ReadString("{" + chunk, "id");
            var x = ReadInt("{" + chunk, "x", int.MinValue);
            var y = ReadInt("{" + chunk, "y", int.MinValue);
            if (string.IsNullOrEmpty(type) || x == int.MinValue || y == int.MinValue)
                continue;
            placements.Add(new Placement(type, x, y));
        }

        return true;
    }

    static int ReadInt(string json, string key, int fallback)
    {
        var token = $"\"{key}\"";
        var i = json.IndexOf(token, StringComparison.Ordinal);
        if (i < 0) return fallback;
        var colon = json.IndexOf(':', i + token.Length);
        if (colon < 0) return fallback;
        var j = colon + 1;
        while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
        var k = j;
        if (k < json.Length && json[k] == '-') k++;
        while (k < json.Length && char.IsDigit(json[k])) k++;
        if (k == j) return fallback;
        return int.TryParse(json.Substring(j, k - j), NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var v)
            ? v
            : fallback;
    }

    static string ReadString(string json, string key)
    {
        var token = $"\"{key}\"";
        var i = json.IndexOf(token, StringComparison.Ordinal);
        if (i < 0) return null;
        var colon = json.IndexOf(':', i + token.Length);
        if (colon < 0) return null;
        var q1 = json.IndexOf('"', colon + 1);
        if (q1 < 0) return null;
        var q2 = json.IndexOf('"', q1 + 1);
        if (q2 < 0) return null;
        return json.Substring(q1 + 1, q2 - q1 - 1);
    }

    static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    readonly struct Placement
    {
        public readonly string TypeId;
        public readonly int X;
        public readonly int Y;

        public Placement(string typeId, int x, int y)
        {
            TypeId = typeId;
            X = x;
            Y = y;
        }
    }
}
