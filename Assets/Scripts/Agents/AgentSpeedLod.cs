using UnityEngine;

namespace BuildATower
{
    public static class AgentSpeedLod
    {
        public const float HighSpeedThreshold = 10f;
        public const float FadeDurationSeconds = 0.6f;
        public const float MaxStaggerDelaySeconds = 1.4f;
        public const int MaxNewFadesPerFrame = 12;

        public static bool IsHighSpeed(float minutesPerRealSecond) =>
            minutesPerRealSecond >= HighSpeedThreshold;

        public static bool IsIdleInRoom(AgentPhase phase) =>
            phase is AgentPhase.AtHome
                or AgentPhase.Working
                or AgentPhase.Staying
                or AgentPhase.VisitingShop;

        public static bool NeedsLodHide(Agent agent) =>
            AgentView.ShouldRenderSprite(agent) &&
            !IsIdleInRoom(agent.Phase);

        public static bool TryBeginFade(ref int startedThisFrame)
        {
            if (startedThisFrame >= MaxNewFadesPerFrame) return false;
            startedThisFrame++;
            return true;
        }

        public static float RandomStaggerDelay(System.Random rng) =>
            (float)(rng.NextDouble() * MaxStaggerDelaySeconds);

        public static float TargetAlpha(
            bool highSpeed,
            bool needsLodHide,
            bool isIdleInRoom)
        {
            if (isIdleInRoom) return 1f;
            if (!needsLodHide) return 1f;
            return highSpeed ? 0f : 1f;
        }

        public static float StepAlpha(float current, float target, float dt)
        {
            if (FadeDurationSeconds <= 0f) return target;
            var step = dt / FadeDurationSeconds;
            if (current < target) return Mathf.Min(target, current + step);
            if (current > target) return Mathf.Max(target, current - step);
            return current;
        }
    }
}
