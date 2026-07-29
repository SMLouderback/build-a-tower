using System.Collections.Generic;

namespace BuildATower
{
    public sealed class EconomySystem
    {
        public const int ElevatorDailyUpkeep = 10_000;

        public int LastIncome { get; private set; }
        public int LastExpense { get; private set; }
        public int LastNet { get; private set; }

        public void OnNewDay(TowerGrid grid, IReadOnlyList<Agent> agents, FundsWallet wallet)
        {
            LastIncome = 0;
            LastExpense = 0;

            foreach (var room in grid.Rooms)
            {
                if (room.Type.isElevatorShaft)
                    LastExpense += ElevatorDailyUpkeep;

                if (!IsRecurringIncomeRoom(room) || !HasHomeAgent(room, agents))
                    continue;

                LastIncome += room.Type.baseIncome;
            }

            wallet.Add(LastIncome);
            wallet.Subtract(LastExpense);
            LastNet = LastIncome - LastExpense;
        }

        public bool TrySellCondo(RoomInstance room, FundsWallet wallet)
        {
            if (room == null ||
                room.Type == null ||
                room.Type.incomeModel != IncomeModel.UpfrontSale ||
                room.CondoSold)
                return false;

            wallet.Add(room.Type.baseIncome);
            room.CondoSold = true;
            return true;
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
