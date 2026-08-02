using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class AgentView : MonoBehaviour
    {
        readonly List<SpriteRenderer> _renderers = new();
        readonly List<Agent> _bound = new();
        Sprite _dot;

        public void Sync(IReadOnlyList<Agent> agents)
        {
            EnsureDotSprite();
            while (_renderers.Count < agents.Count)
            {
                var go = new GameObject("AgentDot");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _dot;
                sr.sortingOrder = 20;
                _renderers.Add(sr);
                _bound.Add(null);
            }

            for (var i = 0; i < _renderers.Count; i++)
            {
                var sr = _renderers[i];
                if (i >= agents.Count)
                {
                    sr.enabled = false;
                    continue;
                }

                var agent = agents[i];
                _bound[i] = agent;
                sr.enabled = agent.Visible;
                if (!agent.Visible) continue;
                var position = agent.Phase == AgentPhase.Riding
                    ? new Vector2(agent.WorldPosition.x, agent.Cell.y + 0.5f)
                    : agent.WorldPosition;
                sr.transform.position = new Vector3(position.x, position.y, 0f);
                sr.color = ColorFor(agent.Role);
                sr.transform.localScale = Vector3.one * 0.35f;
            }
        }

        static Color ColorFor(AgentRole role) =>
            role switch
            {
                AgentRole.OfficeWorker => new Color(0.2f, 0.45f, 0.95f, 1f),
                AgentRole.HotelGuest => new Color(0.70f, 0.40f, 0.88f, 1f),
                AgentRole.CondoResident => new Color(0.3f, 0.75f, 0.4f, 1f),
                AgentRole.StreetVisitor => new Color(0.95f, 0.55f, 0.15f, 1f),
                AgentRole.Maid => new Color(0.15f, 0.82f, 0.78f, 1f),
                AgentRole.Handyman => new Color(0.72f, 0.38f, 0.18f, 1f),
                AgentRole.Security => new Color(0.25f, 0.4f, 0.95f, 1f),
                _ => Color.white
            };

        void EnsureDotSprite()
        {
            if (_dot != null) return;
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var dx = x - 3.5f;
                var dy = y - 3.5f;
                tex.SetPixel(x, y, dx * dx + dy * dy <= 12f ? Color.white : Color.clear);
            }

            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _dot = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }
    }
}
