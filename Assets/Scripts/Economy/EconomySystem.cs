using System.Collections.Generic;

namespace BuildATower
{
    public sealed class EconomySystem
    {
        public const int ElevatorDailyUpkeep = 3_000;
        public const int MaidWagePerDay = 200;
        public const int HandymanWagePerDay = 300;

        const string HousekeepingId = "service_housekeeping";
        const string MaintenanceId = "service_maintenance";

        readonly Dictionary<int, int> _lastIncomeByRoom = new();
        readonly Dictionary<int, int> _lastExpenseByRoom = new();
        System.Random _rng;

        public int LastIncome { get; private set; }
        public int LastExpense { get; private set; }
        public int LastWageExpense { get; private set; }
        public int LastNet { get; private set; }
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
            int climateOffset = 0)
        {
            LastIncome = 0;
            LastExpense = 0;
            LastWageExpense = 0;
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

            wallet.Add(LastIncome);
            wallet.Subtract(LastExpense);
            LastNet = LastIncome - LastExpense;
            if (LastIncome > 0 || LastExpense > 0)
                HasRecordedEconomyEvent = true;
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
    }
}
