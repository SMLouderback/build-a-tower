using System.Collections.Generic;

namespace BuildATower
{
    /// <summary>
    /// Pure capture helper: Security on the same floor removes one Criminal per guard per tick.
    /// </summary>
    public static class CrimeCapture
    {
        public static int TryCapture(
            IList<Agent> agents,
            CrimeSystem crime,
            out string message)
        {
            message = null;
            if (agents == null || crime == null) return 0;

            var captures = 0;
            for (var g = 0; g < agents.Count; g++)
            {
                var guard = agents[g];
                if (guard == null || guard.Role != AgentRole.Security) continue;
                if (guard.Phase == AgentPhase.Outside) continue;

                for (var i = agents.Count - 1; i >= 0; i--)
                {
                    var criminal = agents[i];
                    if (criminal == null || criminal.Role != AgentRole.Criminal) continue;
                    if (criminal.Phase == AgentPhase.Outside) continue;
                    if (criminal.Cell.y != guard.Cell.y) continue;

                    crime.ApplyCaptureDrop(criminal.Cell.y);
                    message = $"Security captured a criminal on floor {criminal.Cell.y}.";
                    agents.RemoveAt(i);
                    captures++;
                    if (i < g) g--;
                    break;
                }
            }

            return captures;
        }
    }
}
