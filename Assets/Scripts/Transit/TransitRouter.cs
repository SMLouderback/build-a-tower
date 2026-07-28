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

        public TransitRouter(StairsPathfinder stairs, ElevatorSystem elevators)
        {
            _stairs = stairs;
            _elevators = elevators;
        }

        public void Rebuild(TowerGrid grid)
        {
            _stairs.Rebuild(grid);
            _elevators.SyncFromGrid(grid);
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

            var shaft = _elevators.FindServing(start.y, goal.y);
            if (shaft == null)
                return false;

            var entry = new Vector2Int(shaft.X, start.y);
            var exit = new Vector2Int(shaft.X, goal.y);
            if (!_stairs.TryFindPath(start, entry, out var toShaft) || toShaft == null)
                return false;
            if (!_stairs.TryFindPath(exit, goal, out var fromShaft) || fromShaft == null)
                return false;

            legs.Add(new TransitLeg
            {
                Kind = TransitLegKind.Walk,
                Cells = toShaft
            });
            legs.Add(new TransitLeg
            {
                Kind = TransitLegKind.Elevator,
                ElevatorX = shaft.X,
                EntryFloor = start.y,
                ExitFloor = goal.y,
                Cells = new List<Vector2Int> { entry, exit }
            });
            legs.Add(new TransitLeg
            {
                Kind = TransitLegKind.Walk,
                Cells = fromShaft
            });
            return true;
        }
    }
}
