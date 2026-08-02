using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public enum TransitLegKind
    {
        Walk,
        Stairs,
        Elevator
    }

    public sealed class TransitLeg
    {
        public TransitLegKind Kind;
        public List<Vector2Int> Cells;
        public int ElevatorX;
        public int EntryFloor;
        public int ExitFloor;
    }

    public sealed class TransitRouter
    {
        readonly StairsPathfinder _stairs;
        readonly ElevatorSystem _elevators;
        float _waitWeightScale = 1f;

        public ElevatorSystem Elevators => _elevators;

        public TransitRouter(StairsPathfinder stairs, ElevatorSystem elevators)
        {
            _stairs = stairs;
            _elevators = elevators;
        }

        public void SetWaitWeightScale(float waitWeightScale) =>
            _waitWeightScale = waitWeightScale > 0f ? waitWeightScale : 1f;

        public void Rebuild(TowerGrid grid)
        {
            _stairs.Rebuild(grid);
            _elevators.SyncFromGrid(grid);
        }

        /// <summary>
        /// Pathfinder walks for a shaft candidate (start→entry, exit→goal).
        /// Returns false when either walk fails — same gate as elevator planning / wait rescoring.
        /// </summary>
        public bool TryShaftWalkPaths(
            Vector2Int start,
            Vector2Int goal,
            ElevatorShaftRuntime shaft,
            out List<Vector2Int> toShaft,
            out List<Vector2Int> fromShaft)
        {
            toShaft = null;
            fromShaft = null;
            if (shaft == null)
                return false;

            var entry = new Vector2Int(shaft.X, start.y);
            var exit = new Vector2Int(shaft.X, goal.y);
            if (!_stairs.TryFindPath(start, entry, out toShaft) || toShaft == null)
                return false;
            if (!_stairs.TryFindPath(exit, goal, out fromShaft) || fromShaft == null)
                return false;
            return true;
        }

        public bool TryPlanTrip(
            Vector2Int start,
            Vector2Int goal,
            out List<TransitLeg> legs)
        {
            legs = new List<TransitLeg>();
            if (start == goal)
            {
                legs.Add(new TransitLeg
                {
                    Kind = TransitLegKind.Walk,
                    Cells = new List<Vector2Int> { start }
                });
                return true;
            }

            if (start.y == goal.y)
            {
                if (!_stairs.TryFindPath(start, goal, out var walk) || walk == null)
                    return false;

                legs.Add(new TransitLeg
                {
                    Kind = TransitLegKind.Walk,
                    Cells = walk
                });
                return true;
            }

            var floorSpan = Mathf.Abs(goal.y - start.y);
            if (floorSpan <= StairsPathfinder.MaxStairsFloorSpan &&
                _stairs.TryFindPath(start, goal, out var stairsPath) &&
                stairsPath != null &&
                stairsPath.Count > 0)
            {
                legs.Add(new TransitLeg
                {
                    Kind = TransitLegKind.Stairs,
                    Cells = stairsPath
                });
                return true;
            }

            ElevatorShaftRuntime best = null;
            var bestScore = float.MaxValue;
            var bestExitWalk = int.MaxValue;
            var bestEntryWalk = int.MaxValue;
            List<Vector2Int> bestToShaft = null;
            List<Vector2Int> bestFromShaft = null;

            var direction = goal.y >= start.y ? ElevatorDirection.Up : ElevatorDirection.Down;
            foreach (var shaft in _elevators.GetServingShafts(start.y, goal.y))
            {
                if (!TryShaftWalkPaths(start, goal, shaft, out var toShaft, out var fromShaft))
                    continue;

                var walkCost = toShaft.Count + fromShaft.Count;
                var wait = _elevators.EstimateWaitMinutes(shaft, start.y, direction);
                var score = ElevatorRouting.Score(walkCost, wait, _waitWeightScale);

                var betterScore = score < bestScore - 1e-3f;
                var nearTie = Mathf.Abs(score - bestScore) <= 1e-3f;
                var betterTieBreak = nearTie && (
                    fromShaft.Count < bestExitWalk ||
                    (fromShaft.Count == bestExitWalk && toShaft.Count < bestEntryWalk) ||
                    (fromShaft.Count == bestExitWalk &&
                     toShaft.Count == bestEntryWalk &&
                     (best == null || shaft.X < best.X)));

                if (!betterScore && !betterTieBreak)
                    continue;

                best = shaft;
                bestScore = score;
                bestExitWalk = fromShaft.Count;
                bestEntryWalk = toShaft.Count;
                bestToShaft = toShaft;
                bestFromShaft = fromShaft;
            }

            if (best == null || bestToShaft == null || bestFromShaft == null)
                return false;

            var bestEntry = new Vector2Int(best.X, start.y);
            var bestExit = new Vector2Int(best.X, goal.y);
            legs.Add(new TransitLeg
            {
                Kind = TransitLegKind.Walk,
                Cells = bestToShaft
            });
            legs.Add(new TransitLeg
            {
                Kind = TransitLegKind.Elevator,
                ElevatorX = best.X,
                EntryFloor = start.y,
                ExitFloor = goal.y,
                Cells = new List<Vector2Int> { bestEntry, bestExit }
            });
            legs.Add(new TransitLeg
            {
                Kind = TransitLegKind.Walk,
                Cells = bestFromShaft
            });
            return true;
        }
    }
}
