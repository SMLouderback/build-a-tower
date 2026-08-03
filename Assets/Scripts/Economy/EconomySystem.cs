using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class EconomySystem
    {
        public const int ElevatorDailyUpkeep = 3_000;
        public const int MaidWagePerDay = 200;
        public const int HandymanWagePerDay = 300;
        public const int SecurityGuardWagePerDay = 250;
        public const int ResearchWagePerDay = 350;
        public const string ResearchId = "service_research";

        const string HousekeepingId = "service_housekeeping";
        const string MaintenanceId = "service_maintenance";
        const string SecurityId = "service_security";

        readonly Dictionary<int, int> _lastIncomeByRoom = new();
        readonly Dictionary<int, int> _lastExpenseByRoom = new();
        System.Random _rng;
        int _midnightCount;
        long _netSum;

        public int LastIncome { get; private set; }
        public int LastExpense { get; private set; }
        public int LastWageExpense { get; private set; }
        public int LastResearchBurn { get; private set; }
        public int LastNet { get; private set; }
        /// <summary>Running average of <see cref="LastNet"/> across completed midnights.</summary>
        public float AverageDailyProfit { get; private set; }
        public bool HasRecordedEconomyEvent { get; private set; }

        public EconomySystem(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public void OnNewDay(
            TowerGrid grid,
            IReadOnlyList<Agent> agents,
            FundsWallet wallet,
            int currentStars = 0,
            int climateOffset = 0,
            ResearchSystem research = null,
            float climateSpendMult = 1f,
            ConferenceSystem conference = null)
        {
            LastIncome = 0;
            LastExpense = 0;
            LastWageExpense = 0;
            LastResearchBurn = 0;
            _lastIncomeByRoom.Clear();
            _lastExpenseByRoom.Clear();

            foreach (var room in grid.Rooms)
                RoomConditionRules.ApplyMidnightDecay(room);

            foreach (var room in grid.Rooms)
            {
                if (room.Type.isElevatorShaft)
                {
                    LastExpense += ElevatorDailyUpkeep;
                    _lastExpenseByRoom[room.InstanceId] = ElevatorDailyUpkeep;
                    room.RecordLifetimeExpense(ElevatorDailyUpkeep);
                }

                var incomeBlocked = RoomConditionRules.IncomePaused(room) || room.IsBroken;

                if (!incomeBlocked &&
                    IsRecurringIncomeRoom(room) &&
                    HasHomeAgent(room, agents) &&
                    PassesDemand(room, currentStars, climateOffset))
                {
                    var amount = PricePricing.ScaledIncome(room.Type.baseIncome, room.PriceTier);
                    LastIncome += amount;
                    _lastIncomeByRoom[room.InstanceId] = amount;
                    room.RecordLifetimeIncome(amount);
                }

                if (ShopVisitRules.IsShop(room.Type))
                {
                    if (!incomeBlocked && room.ShopEarningsToday > 0)
                    {
                        var amount = room.ShopEarningsToday;
                        LastIncome += amount;
                        if (_lastIncomeByRoom.TryGetValue(room.InstanceId, out var existing))
                            _lastIncomeByRoom[room.InstanceId] = existing + amount;
                        else
                            _lastIncomeByRoom[room.InstanceId] = amount;
                        room.RecordLifetimeIncome(amount);
                    }

                    room.ResetVisitsToday();
                }

                var wage = WageForRoom(room);
                if (wage > 0)
                {
                    LastWageExpense += wage;
                    LastExpense += wage;
                    if (_lastExpenseByRoom.TryGetValue(room.InstanceId, out var existingExpense))
                        _lastExpenseByRoom[room.InstanceId] = existingExpense + wage;
                    else
                        _lastExpenseByRoom[room.InstanceId] = wage;
                    room.RecordLifetimeExpense(wage);
                }
            }

            if (conference != null)
            {
                var officeWorkers = CountOfficeWorkers(agents);
                var meetings = conference.ComputeDailyMeetings(
                    grid,
                    officeWorkers,
                    currentStars,
                    climateSpendMult);
                if (meetings > 0)
                    LastIncome += meetings;
            }

            wallet.Add(LastIncome);
            wallet.Subtract(LastExpense);

            var burn = ComputeResearchBurn(grid, research, climateSpendMult);
            if (burn > 0)
            {
                if (wallet.TrySpend(burn))
                {
                    LastResearchBurn = burn;
                    LastExpense += burn;
                }
                else
                {
                    research?.Pause();
                    LastResearchBurn = 0;
                }
            }

            LastNet = LastIncome - LastExpense;
            _midnightCount++;
            _netSum += LastNet;
            AverageDailyProfit = (float)_netSum / _midnightCount;
            if (LastIncome > 0 || LastExpense > 0)
                HasRecordedEconomyEvent = true;
        }

        public static int CountNonBrokenResearchLabs(TowerGrid grid)
        {
            if (grid == null) return 0;
            var count = 0;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type?.id == ResearchId && !room.IsBroken)
                    count++;
            }

            return count;
        }

        public static int CountResearcherPool(TowerGrid grid)
        {
            if (grid == null) return 0;
            var total = 0;
            foreach (var room in grid.Rooms)
            {
                if (room?.Type?.id != ResearchId || room.IsBroken)
                    continue;
                total += room.StaffedWorkers;
            }

            return total;
        }

        static int ComputeResearchBurn(TowerGrid grid, ResearchSystem research, float climateSpendMult)
        {
            var labs = CountNonBrokenResearchLabs(grid);
            if (labs <= 0)
                return 0;

            var burn = ResearchCatalog.IdlePerLabPerDay * labs;
            if (research != null && research.IsRunning && !research.IsPaused)
                burn += ResearchCatalog.ActivePerDay;

            return (int)Math.Round(burn * climateSpendMult);
        }

        public bool TrySellCondo(RoomInstance room, FundsWallet wallet)
        {
            if (room == null ||
                room.Type == null ||
                room.Type.incomeModel != IncomeModel.UpfrontSale ||
                room.CondoSold)
                return false;

            var amount = PricePricing.ScaledIncome(room.Type.baseIncome, room.PriceTier);
            wallet.Add(amount);
            room.CondoSold = true;
            room.RecordLifetimeIncome(amount);
            _lastIncomeByRoom[room.InstanceId] = amount;
            HasRecordedEconomyEvent = true;
            return true;
        }

        public bool PassesDemand(RoomInstance room, int currentStars, int climateOffset = 0)
        {
            var chance = PricePricing.DemandChance(room.PriceTier, currentStars, climateOffset);
            if (chance >= 1f) return true;
            if (chance <= 0f) return false;
            return _rng.NextDouble() < chance;
        }

        public int GetLastRoomIncome(RoomInstance room) =>
            room != null && _lastIncomeByRoom.TryGetValue(room.InstanceId, out var value) ? value : 0;

        public int GetLastRoomExpense(RoomInstance room) =>
            room != null && _lastExpenseByRoom.TryGetValue(room.InstanceId, out var value) ? value : 0;

        public int GetLastRoomNet(RoomInstance room) =>
            GetLastRoomIncome(room) - GetLastRoomExpense(room);

        static int WageForRoom(RoomInstance room)
        {
            if (room?.Type?.id == null || room.StaffedWorkers <= 0)
                return 0;
            return room.Type.id switch
            {
                HousekeepingId => room.StaffedWorkers * MaidWagePerDay,
                MaintenanceId => room.StaffedWorkers * HandymanWagePerDay,
                SecurityId => room.StaffedWorkers * SecurityGuardWagePerDay,
                ResearchId => room.StaffedWorkers * ResearchWagePerDay,
                _ => 0
            };
        }

        static bool IsRecurringIncomeRoom(RoomInstance room)
        {
            return room.Type != null &&
                   room.Type.baseIncome > 0 &&
                   (room.Type.incomeModel == IncomeModel.QuarterlyRent ||
                    room.Type.incomeModel == IncomeModel.NightlyRate);
        }

        static bool HasHomeAgent(RoomInstance room, IReadOnlyList<Agent> agents)
        {
            foreach (var agent in agents)
            {
                if (agent.HomeRoom == room)
                    return true;
            }

            return false;
        }

        public static int CountOfficeWorkers(IReadOnlyList<Agent> agents)
        {
            if (agents == null)
                return 0;
            var count = 0;
            foreach (var agent in agents)
            {
                if (agent != null && agent.Role == AgentRole.OfficeWorker)
                    count++;
            }

            return count;
        }
    }
}
