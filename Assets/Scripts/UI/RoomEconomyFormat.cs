using System.Collections.Generic;

namespace BuildATower
{
    /// <summary>
    /// Shared money strings so room buttons and the selected-tool detail agree.
    /// </summary>
    public static class RoomEconomyFormat
    {
        public static string CostLine(RoomTypeSO type)
        {
            if (type == null) return "Cost: —";
            if (type.isElevatorShaft) return $"Cost: ${type.buildCost:N0} / floor";
            if (type.isLobby) return $"Cost: ${type.buildCost:N0} / cell";
            return $"Cost: ${type.buildCost:N0}";
        }

        public static string IncomeLine(RoomTypeSO type, int tier = PricePricing.TierNormal)
        {
            if (type == null) return "Income: —";

            var amount = PricePricing.ScaledIncome(type.baseIncome, tier);
            switch (type.incomeModel)
            {
                case IncomeModel.UpfrontSale when type.baseIncome > 0:
                    return $"Income: ${amount:N0} once";
                case IncomeModel.QuarterlyRent when type.baseIncome > 0:
                case IncomeModel.NightlyRate when type.baseIncome > 0:
                    return $"Income: ${amount:N0} / day occupied";
                case IncomeModel.TrafficVariable:
                    return "Income: Traffic-based (not active yet)";
                default:
                    return "Income: —";
            }
        }

        public static List<string> SelectedUnitLines(
            RoomInstance room,
            IReadOnlyList<Agent> agents,
            EconomySystem economy)
        {
            var lines = new List<string>();
            if (room?.Type == null) return lines;

            var type = room.Type;
            var tier = room.PriceTier;
            lines.Add($"Built cost: ${ConstructionCost(room):N0}");
            lines.Add(IncomeLine(type, tier));

            var upkeep = UpkeepLine(type);
            if (upkeep != null)
                lines.Add(upkeep);

            switch (type.incomeModel)
            {
                case IncomeModel.QuarterlyRent:
                case IncomeModel.NightlyRate:
                    var occupants = CountHomeAgents(room, agents);
                    lines.Add(occupants > 0
                        ? $"Status: Occupied ({occupants})"
                        : "Status: Vacant — no income");
                    break;
                case IncomeModel.UpfrontSale:
                    lines.Add(room.CondoSold
                        ? "Status: Sold"
                        : CountHomeAgents(room, agents) > 0
                            ? "Status: Buyer moving in — no payout yet"
                            : "Status: For sale — no payout yet");
                    break;
                case IncomeModel.TrafficVariable:
                    lines.Add("Status: Traffic income inactive ($0)");
                    break;
                default:
                    lines.Add("Status: Non-revenue unit");
                    break;
            }

            var income = economy?.GetLastRoomIncome(room) ?? 0;
            var expense = economy?.GetLastRoomExpense(room) ?? 0;
            lines.Add($"Last contribution: +${income:N0} / -${expense:N0} = ${income - expense:N0}");
            return lines;
        }

        static int ConstructionCost(RoomInstance room)
        {
            if (room.Type.isElevatorShaft)
                return room.Type.buildCost * room.Size.y;
            if (room.Type.isLobby)
                return room.Type.buildCost * room.Size.x;
            return room.Type.buildCost;
        }

        static int CountHomeAgents(RoomInstance room, IReadOnlyList<Agent> agents)
        {
            if (agents == null) return 0;

            var count = 0;
            foreach (var agent in agents)
            {
                if (agent.HomeRoom == room)
                    count++;
            }

            return count;
        }

        /// <summary>Returns null for room types that carry no recurring upkeep.</summary>
        public static string UpkeepLine(RoomTypeSO type)
        {
            if (type == null || !type.isElevatorShaft) return null;
            return $"Upkeep: ${EconomySystem.ElevatorDailyUpkeep:N0} / day";
        }

        /// <summary>Compact "cost · income" tag for the room grid buttons.</summary>
        public static string ButtonTag(RoomTypeSO type)
        {
            if (type == null) return "—";

            var cost = type.isElevatorShaft
                ? $"{Abbreviate(type.buildCost)}/fl"
                : Abbreviate(type.buildCost);

            if (type.isElevatorShaft)
                return $"{cost} · -{Abbreviate(EconomySystem.ElevatorDailyUpkeep)}/d";

            switch (type.incomeModel)
            {
                case IncomeModel.UpfrontSale when type.baseIncome > 0:
                    return $"{cost} · {Abbreviate(type.baseIncome)} once";
                case IncomeModel.QuarterlyRent when type.baseIncome > 0:
                case IncomeModel.NightlyRate when type.baseIncome > 0:
                    return $"{cost} · {Abbreviate(type.baseIncome)}/d";
                default:
                    return cost;
            }
        }

        public static string Abbreviate(int dollars)
        {
            if (dollars >= 1_000_000)
            {
                var millions = dollars / 1_000_000f;
                return millions % 1f == 0f ? $"${millions:0}M" : $"${millions:0.#}M";
            }

            if (dollars >= 1_000)
            {
                var thousands = dollars / 1_000f;
                return thousands % 1f == 0f ? $"${thousands:0}k" : $"${thousands:0.#}k";
            }

            return $"${dollars:N0}";
        }
    }
}
