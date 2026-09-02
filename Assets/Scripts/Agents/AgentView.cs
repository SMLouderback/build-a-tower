using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class AgentView : MonoBehaviour
    {
        const float WalkFrameSeconds = 0.12f;
        const float MoveEpsilon = 0.002f;
        public const int DefaultSortingOrder = 20;
        /// <summary>Waiting or walking on an elevator shaft cell (in front of shaft occluder).</summary>
        public const int ElevatorFrontSortingOrder = 24;

        readonly List<SpriteRenderer> _renderers = new();
        readonly List<Agent> _bound = new();
        readonly List<AgentVisualState> _states = new();
        Sprite _dot;

        struct AgentVisualState
        {
            public float LastX;
            public float LastY;
            public float WalkTimer;
            public int FrameIndex;
            public bool FlipX;
            public string SheetKey;
        }

        public void Sync(IReadOnlyList<Agent> agents) => Sync(agents, null);

        public void Sync(IReadOnlyList<Agent> agents, TowerGrid grid)
        {
            EnsureDotSprite();
            while (_renderers.Count < agents.Count)
            {
                var go = new GameObject("AgentSprite");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = DefaultSortingOrder;
                _renderers.Add(sr);
                _bound.Add(null);
                _states.Add(default);
            }

            var dt = Time.deltaTime;
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
                if (!ShouldRenderSprite(agent) || IsHiddenBehindElevatorShaft(agent, grid))
                {
                    sr.enabled = false;
                    continue;
                }

                sr.enabled = true;
                sr.sortingOrder = SortingOrderFor(agent, grid);

                var position = agent.Phase == AgentPhase.Riding
                    ? new Vector2(agent.WorldPosition.x, agent.Cell.y + 0.5f)
                    : agent.WorldPosition;
                var floorY = agent.Phase == AgentPhase.Riding
                    ? position.y
                    : agent.Cell.y;

                var state = _states[i];
                var deltaX = position.x - state.LastX;
                var deltaY = position.y - state.LastY;
                var moving = Mathf.Abs(deltaX) + Mathf.Abs(deltaY) > MoveEpsilon;

                UpdateWalk(ref state, moving, dt);
                state.FlipX = ShouldFlipX(deltaX, state.FlipX, moving);
                state.LastX = position.x;
                state.LastY = position.y;

                var wealth = agent.Wealth;
                if (agent.Role == AgentRole.StreetVisitor)
                    wealth = WealthBand.Street;

                var sheetKey = AgentSpriteArt.ResolveSheetKey(agent.Role, agent.Gender, wealth);
                var frame = !string.IsNullOrEmpty(sheetKey)
                    ? AgentSpriteArt.GetWalkFrame(sheetKey, state.FrameIndex)
                    : null;

                if (frame != null)
                {
                    sr.sprite = frame;
                    sr.color = Color.white;
                    sr.flipX = state.FlipX;
                    var scale = AgentSpriteArt.ScaleForTargetHeight(frame);
                    sr.transform.localScale = new Vector3(scale, scale, 1f);
                    var footLift = AgentSpriteArt.FootLiftFromPivot(frame) * scale;
                    sr.transform.position = new Vector3(
                        position.x,
                        floorY - footLift - AgentSpriteArt.ExtraFootSinkWorld,
                        0f);
                }
                else
                {
                    sr.sprite = _dot;
                    sr.color = ColorFor(agent.Role);
                    sr.flipX = false;
                    sr.transform.localScale = Vector3.one * 0.35f;
                    sr.transform.position = new Vector3(position.x, floorY, 0f);
                }

                state.SheetKey = sheetKey;
                _states[i] = state;
            }
        }

        public static bool IsHiddenBehindElevatorShaft(Agent agent, TowerGrid grid)
        {
            if (agent == null || grid == null) return false;
            if (!grid.IsElevatorCoveringCell(agent.Cell)) return false;
            if (IsVisibleOnElevatorShaft(agent, grid)) return false;
            return agent.Phase is AgentPhase.AtHome
                or AgentPhase.Working
                or AgentPhase.Staying
                or AgentPhase.VisitingShop;
        }

        static bool IsVisibleOnElevatorShaft(Agent agent, TowerGrid grid)
        {
            if (agent.Phase == AgentPhase.WaitingAtElevator) return true;
            return agent.Phase == AgentPhase.Moving && grid.IsElevatorCoveringCell(agent.Cell);
        }

        public static bool ShouldRenderSprite(Agent agent) =>
            agent != null &&
            agent.Visible &&
            agent.Phase != AgentPhase.Riding;

        /// <summary>
        /// Agents in rooms under a shaft stay behind the opaque foreground layer;
        /// passengers queueing on the shaft column render in front of it.
        /// </summary>
        public static int SortingOrderFor(Agent agent, TowerGrid grid)
        {
            if (agent == null) return DefaultSortingOrder;
            if (grid == null) return DefaultSortingOrder;
            if (agent.Phase != AgentPhase.WaitingAtElevator && agent.Phase != AgentPhase.Moving)
                return DefaultSortingOrder;
            if (!grid.TryGetRoomAt(agent.Cell, out var room) ||
                room?.Type == null ||
                !room.Type.isElevatorShaft)
                return DefaultSortingOrder;
            return ElevatorFrontSortingOrder;
        }

        public static int PickWalkFrame(float walkTimer, bool moving) =>
            moving
                ? Mathf.FloorToInt(walkTimer / WalkFrameSeconds) % AgentSpriteArt.WalkFrameCount
                : AgentSpriteArt.IdleFrameIndex;

        public static bool ShouldFlipX(float deltaX, bool previousFlip, bool moving)
        {
            if (moving && Mathf.Abs(deltaX) > MoveEpsilon)
                return deltaX < 0f;
            return previousFlip;
        }

        static void UpdateWalk(ref AgentVisualState state, bool moving, float dt)
        {
            if (moving)
            {
                state.WalkTimer += dt;
                state.FrameIndex = PickWalkFrame(state.WalkTimer, true);
                return;
            }

            state.WalkTimer = 0f;
            state.FrameIndex = AgentSpriteArt.IdleFrameIndex;
        }

        static Color ColorFor(AgentRole role) =>
            role switch
            {
                AgentRole.OfficeWorker => new Color(0.2f, 0.45f, 0.95f, 1f),
                AgentRole.HotelGuest => new Color(0.70f, 0.40f, 0.88f, 1f),
                AgentRole.CondoResident => new Color(0.3f, 0.75f, 0.4f, 1f),
                AgentRole.StreetVisitor => new Color(0.95f, 0.55f, 0.15f, 1f),
                AgentRole.EventVisitor => new Color(0.92f, 0.22f, 0.62f, 1f),
                AgentRole.Maid => new Color(0.15f, 0.82f, 0.78f, 1f),
                AgentRole.Handyman => new Color(0.72f, 0.38f, 0.18f, 1f),
                AgentRole.Security => Color.white,
                AgentRole.Criminal => new Color(0.75f, 0.1f, 0.15f, 1f),
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
