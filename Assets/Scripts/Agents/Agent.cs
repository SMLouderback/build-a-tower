using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class Agent
    {
        public int Id { get; }
        public AgentRole Role { get; }
        public RoomInstance HomeRoom { get; }
        public Vector2Int Cell { get; set; }
        public Vector2 WorldPosition { get; set; }
        public AgentPhase Phase { get; set; }
        public float Stress { get; set; }
        public bool Visible { get; set; }

        public int ArrivalMinute { get; set; }
        public int WorkMinutes { get; set; }
        public int WorkedMinutes { get; set; }
        public int CheckInDay { get; set; } = -1;
        public bool CheckedOutToday { get; set; }

        public List<Vector2Int> Path { get; set; }
        public int PathIndex { get; set; }
        public Vector2Int? GoalCell { get; set; }
        public AgentPhase PhaseAfterMove { get; set; }
        public List<TransitLeg> TripLegs { get; set; }
        public int TripLegIndex { get; set; }
        public int ElevatorDestFloor { get; set; }
        public float ElevatorWaitMinutes { get; set; }

        public Agent(int id, AgentRole role, RoomInstance homeRoom, Vector2Int cell)
        {
            Id = id;
            Role = role;
            HomeRoom = homeRoom;
            Cell = cell;
            WorldPosition = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            Phase = AgentPhase.Outside;
            Visible = false;
            Path = new List<Vector2Int>();
            TripLegs = new List<TransitLeg>();
        }
    }
}
