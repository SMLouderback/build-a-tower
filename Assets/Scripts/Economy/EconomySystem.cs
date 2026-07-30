using System.Collections.Generic;

namespace BuildATower
{
    public sealed class EconomySystem
    {
        public const int ElevatorDailyUpkeep = 3_000;

        readonly Dictionary<int, int> _lastIncomeByRoom = new();
        readonly Dictionary<int, int> _lastExpenseByRoom = new();
        System.Random _rng;

        public int LastIncome { get; private set; }
        public int LastExpense { get; private set; }
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
            int currentStars = 0)
        {
            LastIncome = 0;
            LastExpense = 0;
            _lastIncomeByRoom.Clear();
            _lastExpenseByRoom.Clear();

            foreach (var room in grid.Rooms)
            {
                if (room.Type.isElevatorShaft)
                {
                    LastExpense += ElevatorDailyUpkeep;
                    _lastExpenseByRoom[room.InstanceId] = ElevatorDailyUpkeep;
                    room.RecordLifetimeExpense(ElevatorDailyUpkeep);
                }

                if (!IsRecurringIncomeRoom(room) || !HasHomeAgent(room, agents))
                    continue;

                if (!PassesDemand(room, currentStars))
                    continue;

                var amount = PricePricing.ScaledIncome(room.Type.baseIncome, room.PriceTier);
                LastIncome += amount;
                _lastIncomeByRoom[room.InstanceId] = amount;
                room.RecordLifetimeIncome(amount);
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

        public bool PassesDemand(RoomInstance room, int currentStars)
        {
            var chance = PricePricing.DemandChance(room.PriceTier, currentStars);
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
