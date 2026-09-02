using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class Agent
    {
        public int Id { get; }
        public AgentRole Role { get; }
        public AgentGender Gender { get; set; }
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
        /// <summary>
        /// Minute of day to start check-in (clamped to the hotel 4:00–7:00 PM window).
        /// </summary>
        public int CheckInMinute { get; set; } = 16 * 60;
        /// <summary>
        /// Minute of day to start checkout the morning after check-in
        /// (clamped to the hotel 6:00–11:00 window).
        /// </summary>
        public int CheckoutMinute { get; set; } = 11 * 60;
        public bool HasMovedIn { get; set; }

        /// <summary>Occupant slot within <see cref="HomeRoom"/> for distinct home cells.</summary>
        public int HomeSlot { get; set; }

        public List<Vector2Int> Path { get; set; }
        public int PathIndex { get; set; }
        public Vector2Int? GoalCell { get; set; }
        public AgentPhase PhaseAfterMove { get; set; }
        public List<TransitLeg> TripLegs { get; set; }
        public int TripLegIndex { get; set; }
        public int ElevatorDestFloor { get; set; }
        public int ElevatorEntryFloor { get; set; }

        /// <summary>Floors already crossed on the current Stairs leg (for comfort/over-cap).</summary>
        public int StairsFloorsCrossedThisLeg { get; set; }

        /// <summary>
        /// Stairs room <see cref="RoomInstance.InstanceId"/> currently occupied, or 0 if none.
        /// </summary>
        public int StairsOccupancyRoomId { get; set; }

        /// <summary>
        /// Shaft the agent is committed to (room instance id), or 0 when none.
        /// Resolved by id so maintenance mode cannot orphan a waiter or rider.
        /// </summary>
        public int ElevatorShaftId { get; set; }

        /// <summary>+1 queues to the right of the shaft, -1 to the left.</summary>
        public int ElevatorQueueSide { get; set; } = 1;
        public float ElevatorWaitMinutes { get; set; }

        /// <summary>
        /// Absolute game minutes when this agent last switched elevator shafts while waiting.
        /// </summary>
        public float LastElevatorSwitchTotalMinutes { get; set; } = float.NegativeInfinity;

        /// <summary>
        /// Absolute game minutes when the next waiting re-score is allowed.
        /// </summary>
        public float NextElevatorRescoreTotalMinutes { get; set; }

        /// <summary>
        /// Game minutes spent <see cref="AgentSystem.IsMovementStuck"/> with a goal
        /// (empty/exhausted path while Moving). Reset when movement resumes.
        /// </summary>
        public float PathStuckMinutes { get; set; }

        public int CommercialTripDay { get; set; } = -1;
        public RoomInstance VisitTarget { get; set; }
        public float VisitDwellRemaining { get; set; }
        public AgentPhase PhaseAfterVisit { get; set; }
        public Vector2Int? ReturnCell { get; set; }

        /// <summary>
        /// Rolled wealth band for hotel guests (and event hotel visitors).
        /// Street default means “unset” for other roles — disposable uses <see cref="AgentWealth.ResolveBand"/>.
        /// </summary>
        public WealthBand Wealth { get; set; }

        /// <summary>Remaining disposable cash for commercial spend today.</summary>
        public int DisposableRemaining { get; set; }

        /// <summary>Day index last used to refill disposable income, or -1.</summary>
        public int DisposableDayIndex { get; set; } = -1;

        /// <summary>Day index when low-condition home stress was last applied, or -1.</summary>
        public int LowConditionStressDay { get; set; } = -1;

        /// <summary>Day index when floor-crime stress was last applied, or -1.</summary>
        public int CrimeStressDay { get; set; } = -1;

        /// <summary>Hotel/room currently claimed for cleaning or repair, if any.</summary>
        public RoomInstance ServiceTarget { get; set; }

        /// <summary>Game minutes remaining for the active maid/handyman job.</summary>
        public float ServiceWorkRemaining { get; set; }

        /// <summary>Maid-minutes of clean-pool progress to apply when the current shift finishes.</summary>
        public float ServiceCleanProgress { get; set; }

        /// <summary>
        /// Game minutes spent traveling to <see cref="ServiceTarget"/> without entering Working.
        /// Reset when a new claim starts or work begins.
        /// </summary>
        public float ServiceTravelMinutes { get; set; }

        /// <summary>Total remaining life for a Criminal before they leave via lobby.</summary>
        public float CriminalDwellRemaining { get; set; }

        /// <summary>Condo resident employment assignment, or <see cref="CondoJobKind.None"/>.</summary>
        public CondoJobKind JobKind { get; set; }

        /// <summary>Office room claimed as a condo workplace (in-tower jobs only).</summary>
        public RoomInstance WorkplaceRoom { get; set; }

        /// <summary>Desk index within <see cref="WorkplaceRoom"/> (matches OfficeWorker HomeSlot layout).</summary>
        public int WorkplaceSlot { get; set; }

        /// <summary>True when this agent entered the tower via underground parking.</summary>
        public bool ArrivedViaParking { get; set; }

        /// <summary>Parking room holding a claimed stall, if any.</summary>
        public RoomInstance ParkingRoom { get; set; }

        /// <summary>Stall index within <see cref="ParkingRoom"/>.</summary>
        public int ParkingSlot { get; set; }

        /// <summary>One-way commute duration in game minutes (outside jobs).</summary>
        public int CommuteOneWayMinutes { get; set; }

        /// <summary>Minute of day when the condo leaves home for work.</summary>
        public int LeaveHomeMinute { get; set; }

        /// <summary>Remaining outside dwell for external commute/work simulation.</summary>
        public float OutsideDwellRemaining { get; set; }

        /// <summary>Current phase of an outside condo work cycle.</summary>
        public CondoOutsidePhase OutsideWorkPhase { get; set; }

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
