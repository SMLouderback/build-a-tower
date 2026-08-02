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
            out List<TransitLeg> legs) =>
            TryPlanTrip(start, goal, agentStress: 0f, out legs);

        public bool TryPlanTrip(
            Vector2Int start,
            Vector2Int goal,
            float agentStress,
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

            var bestScore = float.MaxValue;
            var bestExitWalk = int.MaxValue;
            var bestEntryWalk = int.MaxValue;
            var bestShaftX = int.MaxValue;
            List<TransitLeg> bestLegs = null;
            var hadFullElevatorCandidate = false;

            // Full elevator: shaft serves both start and goal.
            foreach (var shaft in _elevators.GetServingShafts(start.y, goal.y))
            {
                if (!TryShaftWalkPaths(start, goal, shaft, out var toShaft, out var fromShaft))
                    continue;

                hadFullElevatorCandidate = true;
                var walkCost = toShaft.Count + fromShaft.Count;
                var direction = goal.y >= start.y ? ElevatorDirection.Up : ElevatorDirection.Down;
                var wait = _elevators.EstimateWaitMinutes(shaft, start.y, direction);
                var score = ElevatorRouting.Score(walkCost, wait, _waitWeightScale) +
                            ElevatorRouting.StairsOverCapPenalty(0);

                if (!IsBetterCandidate(
                        score, fromShaft.Count, toShaft.Count, shaft.X,
                        bestScore, bestExitWalk, bestEntryWalk, bestShaftX))
                    continue;

                bestScore = score;
                bestExitWalk = fromShaft.Count;
                bestEntryWalk = toShaft.Count;
                bestShaftX = shaft.X;
                bestLegs = BuildFullElevatorLegs(shaft, start.y, goal.y, toShaft, fromShaft);
            }

            // Hybrid: start on shaft, exit closest to goal, then stairs.
            foreach (var shaft in _elevators.GetShaftsServingFloor(start.y))
            {
                var exitFloor = ElevatorSystem.ClosestFloorOnShaft(shaft, goal.y);
                if (exitFloor == goal.y)
                    continue; // covered by full elevator

                var entryFloor = start.y;
                if (entryFloor == exitFloor)
                    continue;

                var entryCell = new Vector2Int(shaft.X, entryFloor);
                var exitCell = new Vector2Int(shaft.X, exitFloor);
                if (!_stairs.TryFindPath(start, entryCell, out var toShaft) || toShaft == null)
                    continue;
                if (!_stairs.TryFindPath(exitCell, goal, -1, out var stairsAfter) ||
                    stairsAfter == null ||
                    stairsAfter.Count == 0)
                    continue;

                var stairSpan = Mathf.Abs(exitFloor - goal.y);
                if (!IsAffordableStairSpan(stairSpan, agentStress))
                    continue;

                var walkCost = toShaft.Count + stairsAfter.Count;
                var direction = exitFloor >= entryFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
                var wait = _elevators.EstimateWaitMinutes(shaft, entryFloor, direction);
                var score = ElevatorRouting.Score(walkCost, wait, _waitWeightScale) +
                            ElevatorRouting.StairsOverCapPenalty(stairSpan);

                if (!IsBetterCandidate(
                        score, stairsAfter.Count, toShaft.Count, shaft.X,
                        bestScore, bestExitWalk, bestEntryWalk, bestShaftX))
                    continue;

                bestScore = score;
                bestExitWalk = stairsAfter.Count;
                bestEntryWalk = toShaft.Count;
                bestShaftX = shaft.X;
                bestLegs = BuildHybridLegs(
                    shaft,
                    entryFloor,
                    exitFloor,
                    toShaft,
                    stairsAfter,
                    leadingIsStairs: false);
            }

            // Hybrid reverse: stairs (or walk) to shaft entry, elevator, optional stairs after.
            foreach (var shaft in _elevators.Shafts)
            {
                if (shaft.InMaintenance)
                    continue;

                var entryFloor = ElevatorSystem.ClosestFloorOnShaft(shaft, start.y);
                if (entryFloor == start.y)
                    continue; // start-on-shaft hybrids / full elevator cover this

                var exitFloor = ElevatorSystem.ClosestFloorOnShaft(shaft, goal.y);
                if (entryFloor == exitFloor)
                    continue;

                var entryCell = new Vector2Int(shaft.X, entryFloor);
                var exitCell = new Vector2Int(shaft.X, exitFloor);
                if (!_stairs.TryFindPath(start, entryCell, -1, out var toEntry) ||
                    toEntry == null ||
                    toEntry.Count == 0)
                    continue;

                List<Vector2Int> afterExit;
                if (exitFloor == goal.y)
                {
                    if (!_stairs.TryFindPath(exitCell, goal, out afterExit) || afterExit == null)
                        continue;
                }
                else
                {
                    if (!_stairs.TryFindPath(exitCell, goal, -1, out afterExit) ||
                        afterExit == null ||
                        afterExit.Count == 0)
                        continue;
                }

                var stairSpan = Mathf.Abs(start.y - entryFloor) + Mathf.Abs(exitFloor - goal.y);
                if (!IsAffordableStairSpan(stairSpan, agentStress))
                    continue;

                var walkCost = toEntry.Count + afterExit.Count;
                var direction = exitFloor >= entryFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
                var wait = _elevators.EstimateWaitMinutes(shaft, entryFloor, direction);
                var score = ElevatorRouting.Score(walkCost, wait, _waitWeightScale) +
                            ElevatorRouting.StairsOverCapPenalty(stairSpan);

                if (!IsBetterCandidate(
                        score, afterExit.Count, toEntry.Count, shaft.X,
                        bestScore, bestExitWalk, bestEntryWalk, bestShaftX))
                    continue;

                bestScore = score;
                bestExitWalk = afterExit.Count;
                bestEntryWalk = toEntry.Count;
                bestShaftX = shaft.X;
                bestLegs = BuildHybridLegs(
                    shaft,
                    entryFloor,
                    exitFloor,
                    toEntry,
                    afterExit,
                    leadingIsStairs: true);
            }

            // Over-cap pure stairs only when no full-elevator candidate exists.
            if (!hadFullElevatorCandidate &&
                _stairs.TryFindPath(start, goal, -1, out var pureStairs) &&
                pureStairs != null &&
                pureStairs.Count > 0)
            {
                var stairSpan = Mathf.Abs(goal.y - start.y);
                if (IsAffordableStairSpan(stairSpan, agentStress))
                {
                    var score = ElevatorRouting.Score(pureStairs.Count, 0f, _waitWeightScale) +
                                ElevatorRouting.StairsOverCapPenalty(stairSpan);
                    if (IsBetterCandidate(
                            score, pureStairs.Count, 0, int.MaxValue,
                            bestScore, bestExitWalk, bestEntryWalk, bestShaftX))
                    {
                        bestScore = score;
                        bestExitWalk = pureStairs.Count;
                        bestEntryWalk = 0;
                        bestShaftX = int.MaxValue;
                        bestLegs = new List<TransitLeg>
                        {
                            new TransitLeg
                            {
                                Kind = TransitLegKind.Stairs,
                                Cells = pureStairs
                            }
                        };
                    }
                }
            }

            if (bestLegs == null)
                return false;

            legs = bestLegs;
            return true;
        }

        static bool IsAffordableStairSpan(int stairFloorSpan, float agentStress)
        {
            var overCap = Mathf.Max(0, stairFloorSpan - ElevatorRouting.StairsComfortFloorSpan);
            return overCap <= ElevatorRouting.MaxAffordableOverCapFloors(agentStress);
        }

        static bool IsBetterCandidate(
            float score,
            int exitWalk,
            int entryWalk,
            int shaftX,
            float bestScore,
            int bestExitWalk,
            int bestEntryWalk,
            int bestShaftX)
        {
            var betterScore = score < bestScore - 1e-3f;
            var nearTie = Mathf.Abs(score - bestScore) <= 1e-3f;
            var betterTieBreak = nearTie && (
                exitWalk < bestExitWalk ||
                (exitWalk == bestExitWalk && entryWalk < bestEntryWalk) ||
                (exitWalk == bestExitWalk &&
                 entryWalk == bestEntryWalk &&
                 shaftX < bestShaftX));
            return betterScore || betterTieBreak;
        }

        static List<TransitLeg> BuildFullElevatorLegs(
            ElevatorShaftRuntime shaft,
            int entryFloor,
            int exitFloor,
            List<Vector2Int> toShaft,
            List<Vector2Int> fromShaft)
        {
            var entry = new Vector2Int(shaft.X, entryFloor);
            var exit = new Vector2Int(shaft.X, exitFloor);
            return new List<TransitLeg>
            {
                new TransitLeg
                {
                    Kind = TransitLegKind.Walk,
                    Cells = toShaft
                },
                new TransitLeg
                {
                    Kind = TransitLegKind.Elevator,
                    ElevatorX = shaft.X,
                    EntryFloor = entryFloor,
                    ExitFloor = exitFloor,
                    Cells = new List<Vector2Int> { entry, exit }
                },
                new TransitLeg
                {
                    Kind = TransitLegKind.Walk,
                    Cells = fromShaft
                }
            };
        }

        static List<TransitLeg> BuildHybridLegs(
            ElevatorShaftRuntime shaft,
            int entryFloor,
            int exitFloor,
            List<Vector2Int> toEntry,
            List<Vector2Int> afterExit,
            bool leadingIsStairs)
        {
            var entry = new Vector2Int(shaft.X, entryFloor);
            var exit = new Vector2Int(shaft.X, exitFloor);
            var legs = new List<TransitLeg>
            {
                new TransitLeg
                {
                    Kind = leadingIsStairs || PathChangesFloor(toEntry)
                        ? TransitLegKind.Stairs
                        : TransitLegKind.Walk,
                    Cells = toEntry
                },
                new TransitLeg
                {
                    Kind = TransitLegKind.Elevator,
                    ElevatorX = shaft.X,
                    EntryFloor = entryFloor,
                    ExitFloor = exitFloor,
                    Cells = new List<Vector2Int> { entry, exit }
                }
            };

            if (afterExit != null && afterExit.Count > 0)
            {
                legs.Add(new TransitLeg
                {
                    Kind = PathChangesFloor(afterExit)
                        ? TransitLegKind.Stairs
                        : TransitLegKind.Walk,
                    Cells = afterExit
                });
            }

            return legs;
        }

        static bool PathChangesFloor(List<Vector2Int> path)
        {
            if (path == null || path.Count == 0)
                return false;
            var y0 = path[0].y;
            for (var i = 1; i < path.Count; i++)
            {
                if (path[i].y != y0)
                    return true;
            }

            return false;
        }
    }
}
