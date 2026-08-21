using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// FIFO star promote/demote modals with pause restore and promote-only fireworks.
    /// </summary>
    public sealed class StarCelebrationController : MonoBehaviour
    {
        [SerializeField] TowerSimulation simulation;
        [SerializeField] TowerHudController hud;
        [SerializeField] BuildController build;

        readonly Queue<StarChangeEvent> _queue = new();
        bool _showing;
        StarChangeEvent _current;
        StarCelebrationPauseGate.SpeedSnapshot _snapshot;
        Texture2D _whiteTex;

        /// <summary>True while a modal is open or events remain (including wait for Esc pause).</summary>
        public bool IsActive => _showing || _queue.Count > 0;

        /// <summary>True only while the celebration modal is on screen (Esc must stay Continue-only).</summary>
        public bool IsModalOpen => _showing;

        public void Bind(TowerSimulation sim, TowerHudController hudController, BuildController buildController)
        {
            if (sim != null) simulation = sim;
            if (hudController != null) hud = hudController;
            if (buildController != null) build = buildController;
        }

        public void Enqueue(IReadOnlyList<StarChangeEvent> events)
        {
            if (events == null || events.Count == 0) return;
            for (var i = 0; i < events.Count; i++)
                _queue.Enqueue(events[i]);
            TryBeginNext();
        }

        public void ClearQueue()
        {
            if (_showing)
            {
                StarFireworks.Stop();
                StarCelebrationPauseGate.Apply(simulation, _snapshot);
                _showing = false;
            }

            _queue.Clear();
        }

        void Awake()
        {
            ResolveRefs();
        }

        void OnDestroy()
        {
            ClearQueue();
            if (_whiteTex != null)
            {
                Destroy(_whiteTex);
                _whiteTex = null;
            }
        }

        void Update()
        {
            if (_showing) return;
            if (_queue.Count == 0) return;
            TryBeginNext();
        }

        void OnGUI()
        {
            if (!_showing) return;

            EnsureWhiteTex();
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _whiteTex);
            GUI.color = prevColor;

            var promote = _current.Kind == StarChangeKind.Promoted;
            var panelColor = promote
                ? new Color(0.72f, 0.42f, 0.18f, 0.95f)
                : new Color(0.28f, 0.30f, 0.34f, 0.95f);

            var panelW = 380f;
            var panelH = 168f;
            var panel = new Rect(
                (Screen.width - panelW) * 0.5f,
                (Screen.height - panelH) * 0.5f,
                panelW,
                panelH);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = panelColor;
            GUI.Box(panel, GUIContent.none);
            GUI.backgroundColor = prevBg;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            titleStyle.normal.textColor = promote
                ? new Color(1f, 0.93f, 0.75f)
                : new Color(0.82f, 0.84f, 0.88f);

            var bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            bodyStyle.normal.textColor = promote
                ? new Color(1f, 0.96f, 0.88f)
                : new Color(0.75f, 0.76f, 0.80f);

            var title = promote
                ? $"{_current.Stars}\u2605"
                : $"Demoted to {_current.Stars}\u2605";
            var body = promote
                ? "Your tower earned another star!"
                : "Quarterly review\u2026";

            var cx = panel.x + 20f;
            var cy = panel.y + 18f;
            var inner = panelW - 40f;
            GUI.Label(new Rect(cx, cy, inner, 40f), title, titleStyle);
            cy += 48f;
            GUI.Label(new Rect(cx, cy, inner, 40f), body, bodyStyle);
            cy += 48f;

            if (GUI.Button(new Rect(cx, cy, inner, 34f), "Continue"))
                OnContinue();
        }

        void TryBeginNext()
        {
            if (_showing) return;
            if (_queue.Count == 0) return;
            ResolveRefs();
            if (hud != null && hud.IsEscPauseOpen)
                return;

            _current = _queue.Peek();
            _showing = true;

            if (simulation?.Clock != null)
                _snapshot = StarCelebrationPauseGate.Capture(simulation.Clock);
            else
                _snapshot = new StarCelebrationPauseGate.SpeedSnapshot(1f, paused: false);

            simulation?.SetSpeedPreset(_snapshot.MinutesPerRealSecond, paused: true);

            if (_current.Kind == StarChangeKind.Promoted)
                StarFireworks.Play(transform, ResolveTowerTopWorld());
        }

        void OnContinue()
        {
            if (!_showing) return;

            StarFireworks.Stop();
            StarCelebrationPauseGate.Apply(simulation, _snapshot);
            if (_queue.Count > 0)
                _queue.Dequeue();
            _showing = false;

            TryBeginNext();
        }

        Vector3 ResolveTowerTopWorld()
        {
            var grid = build?.Grid;
            if (grid == null || grid.Rooms == null || grid.Rooms.Count == 0)
                return new Vector3(0f, 8f, 0f);

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            foreach (var room in grid.Rooms)
            {
                if (room == null) continue;
                minX = Mathf.Min(minX, room.Origin.x);
                maxX = Mathf.Max(maxX, room.Origin.x + room.Size.x);
                maxY = Mathf.Max(maxY, room.Origin.y + room.Size.y);
            }

            if (minX > maxX || maxY == float.MinValue)
                return new Vector3(0f, 8f, 0f);

            return new Vector3((minX + maxX) * 0.5f, maxY + 1.25f, 0f);
        }

        void ResolveRefs()
        {
            if (simulation == null)
                simulation = GetComponent<TowerSimulation>() ?? FindAnyObjectByType<TowerSimulation>();
            if (build == null)
                build = simulation != null
                    ? simulation.GetComponent<BuildController>()
                    : FindAnyObjectByType<BuildController>();
            if (build == null)
                build = FindAnyObjectByType<BuildController>();
            if (hud == null)
                hud = FindAnyObjectByType<TowerHudController>();
        }

        void EnsureWhiteTex()
        {
            if (_whiteTex != null) return;
            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply(false, true);
        }
    }
}
