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

        public static string IncomeLine(RoomTypeSO type)
        {
            if (type == null) return "Income: —";

            switch (type.incomeModel)
            {
                case IncomeModel.UpfrontSale when type.baseIncome > 0:
                    return $"Income: ${type.baseIncome:N0} once";
                case IncomeModel.QuarterlyRent when type.baseIncome > 0:
                case IncomeModel.NightlyRate when type.baseIncome > 0:
                    return $"Income: ${type.baseIncome:N0} / day occupied";
                default:
                    return "Income: —";
            }
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
