using System.Collections.Generic;

namespace BuildATower
{
    /// <summary>
    /// Shared money strings so room buttons and the selected-tool detail agree.
    /// Sim money comes from <see cref="EconomicBalancingManager"/> / <see cref="BuildEconomy"/>.
    /// </summary>
    public static class RoomEconomyFormat
    {
        public static string CostLine(RoomTypeSO type)
        {
            if (type == null) return "Cost: —";
            var cost = BuildEconomy.UnitBuildCost(type);
            if (type.isElevatorShaft) return $"Cost: ${cost:N0} / floor";
            if (type.isLobby) return $"Cost: ${cost:N0} / cell";
            return $"Cost: ${cost:N0}";
        }

        public static string IncomeLine(
            RoomTypeSO type,
            int tier = PricePricing.TierNormal,
            float climateSpendMult = 1f,
            float pulseMult = 1f,
            float floorFit01 = 1f)
        {
            if (type == null) return "Income: —";

            switch (type.incomeModel)
            {
                case IncomeModel.UpfrontSale:
                case IncomeModel.QuarterlyRent:
                case IncomeModel.NightlyRate:
                {
                    var amount = EconomicBalancingManager.PeriodIncome(
                        type,
                        tier,
                        GameSession.Difficulty,
                        climateSpendMult,
                        pulseMult,
                        floorFit01);
                    if (amount <= 0) return "Income: —";
                    if (type.incomeModel == IncomeModel.UpfrontSale)
                        return $"Income: ${amount:N0} once";
                    return $"Income: ${amount:N0} / day occupied";
                }
                case IncomeModel.TrafficVariable:
                {
                    var visit = ShopVisitRules.PayPerVisit(type);
                    if (visit <= 0) return "Income: —";
                    return $"Income: up to ${visit:N0} / visit (spent dollars at midnight)";
                }
                default:
                    return "Income: —";
            }
        }

        public static List<string> SelectedUnitLines(
            RoomInstance room,
            IReadOnlyList<Agent> agents,
            EconomySystem economy,
            ShopDemandSystem demand = null,
            int openShopCountInPool = 0,
            int currentStars = 0,
            float climateSpendMult = 1f,
            float livingPulseMult = 1f,
            float commercialPulseMult = 1f)
        {
            var lines = new List<string>();
            if (room?.Type == null) return lines;

            var type = room.Type;
            var tier = room.PriceTier;
            var pulse = EconomySystem.PulseFor(type, livingPulseMult, commercialPulseMult);
            var floorFit = EconomySystem.FloorFitFor(room, currentStars);
            lines.Add($"Built cost: ${ConstructionCost(room):N0}");
            lines.Add(IncomeLine(type, tier, climateSpendMult, pulse, floorFit));

            var upkeep = UpkeepLine(type, pulse);
            if (upkeep != null)
                lines.Add(upkeep);

            AppendHotelSelectionLines(lines, type);
            AppendOfficeSelectionLines(lines, type);
            AppendCondoSelectionLines(lines, type);

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
                    if (ShopVisitRules.IsShop(type) && demand != null)
                    {
                        var family = ShopDemandBalance.FamilyFor(type);
                        var demandTier = ShopDemandBalance.TierForShop(type);
                        lines.Add($"Demand: {family} / {demandTier}");
                    }
                    lines.Add($"Visits today: {room.VisitsToday}");
                    lines.Add($"Visits yesterday: {room.VisitsYesterday}");
                    lines.Add($"Avg visits (7d): {room.AverageVisitsLast7Days:0.#}");
                    lines.Add($"Earnings today: ${room.ShopEarningsToday:N0}");
                    lines.Add($"Yesterday revenue: ${room.ShopRevenueYesterday:N0}");
                    var shopUpkeep = EconomicBalancingManager.PeriodUpkeep(
                        type, GameSession.Difficulty, commercialPulseMult);
                    lines.Add($"Daily upkeep: ${shopUpkeep:N0}");
                    lines.Add($"Yesterday net: {SignedMoney(room.ShopNetYesterday)}");
                    if (ShopVisitRules.IsShop(type) && demand != null)
                    {
                        var family = ShopDemandBalance.FamilyFor(type);
                        var demandTier = ShopDemandBalance.TierForShop(type);
                        var pool = demand.Snapshot.Pool(family, demandTier);
                        lines.Add(
                            $"Competition: {ShopDemandFormat.Competition(pool, openShopCountInPool)}");
                    }
                    break;
                default:
                    if (type.id == ConferenceSystem.ConferenceId)
                        lines.Add("Status: Daily meeting venue");
                    else if (type.id == ConferenceSystem.EventHallId)
                        lines.Add("Status: Major-event venue");
                    else
                        lines.Add("Status: Non-revenue unit");
                    break;
            }

            var income = economy?.GetLastRoomIncome(room) ?? 0;
            var expense = economy?.GetLastRoomExpense(room) ?? 0;
            lines.Add($"Last contribution: +${income:N0} / -${expense:N0} = ${income - expense:N0}");
            return lines;
        }

        static string SignedMoney(int amount)
        {
            if (amount > 0) return $"+${amount:N0}";
            if (amount < 0) return $"-${-amount:N0}";
            return "$0";
        }

        static int ConstructionCost(RoomInstance room)
        {
            if (room.Type.isElevatorShaft)
                return BuildEconomy.UnitBuildCost(room.Type) * room.Size.y;
            if (room.Type.isLobby)
                return BuildEconomy.UnitBuildCost(room.Type) * room.Size.x;
            return BuildEconomy.BuildCost(room.Type);
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

        /// <summary>Band + clean minutes for hotel tool/selection details.</summary>
        public static void AppendHotelSelectionLines(List<string> lines, RoomTypeSO type)
        {
            if (lines == null || type == null || type.category != RoomCategory.Hotel)
                return;

            var band = type.luxuryBand == LuxuryBand.None ? LuxuryBand.Base : type.luxuryBand;
            lines.Add($"Band: {band}");
            lines.Add($"Clean: {HotelLuxury.ResolveCleanMinutes(type):0.#} min");
        }

        /// <summary>Band + desks for office tool/selection details.</summary>
        public static void AppendOfficeSelectionLines(List<string> lines, RoomTypeSO type)
        {
            if (lines == null || type == null || type.category != RoomCategory.Office)
                return;

            var band = type.luxuryBand == LuxuryBand.None ? LuxuryBand.Base : type.luxuryBand;
            lines.Add($"Band: {band}");
            lines.Add($"Desks: {System.Math.Max(1, type.maxOccupants)}");
        }

        /// <summary>Band + occupants for condo tool/selection details.</summary>
        public static void AppendCondoSelectionLines(List<string> lines, RoomTypeSO type)
        {
            if (lines == null || type == null || type.category != RoomCategory.Condo)
                return;

            var band = type.luxuryBand == LuxuryBand.None ? LuxuryBand.Base : type.luxuryBand;
            lines.Add($"Band: {band}");
            lines.Add($"Occupants: {System.Math.Max(1, type.maxOccupants)}");
        }

        /// <summary>Returns null for room types that carry no recurring upkeep.</summary>
        public static string UpkeepLine(RoomTypeSO type, float pulseMult = 1f)
        {
            if (type == null) return null;
            if (type.isElevatorShaft)
                return $"Upkeep: ${EconomySystem.ElevatorDailyUpkeep:N0} / day";
            if (ParkingStalls.IsParking(type))
                return $"Upkeep: ${ParkingStalls.ParkingDailyUpkeep:N0} / day";
            if (ParkingStalls.IsValet(type))
                return $"Upkeep: ${ParkingStalls.ValetDailyUpkeep:N0} / day";
            if (ParkingStalls.IsRamp(type))
                return $"Upkeep: ${ParkingStalls.RampDailyUpkeep:N0} / day";

            if (VpsfCatalog.TryIdentity(type, out var family, out _, out _) &&
                VpsfCatalog.IsLivingFamily(family))
            {
                var upkeep = EconomicBalancingManager.PeriodUpkeep(
                    type, GameSession.Difficulty, pulseMult);
                if (upkeep > 0)
                    return $"Upkeep: ${upkeep:N0} / day";
            }

            return null;
        }

        /// <summary>Compact "cost · income" tag for the room grid buttons.</summary>
        public static string ButtonTag(RoomTypeSO type)
        {
            if (type == null) return "—";

            var unitCost = BuildEconomy.UnitBuildCost(type);
            var cost = type.isElevatorShaft
                ? $"{Abbreviate(unitCost)}/fl"
                : Abbreviate(type.isLobby ? unitCost : BuildEconomy.BuildCost(type));

            if (type.isElevatorShaft)
                return $"{cost} · -{Abbreviate(EconomySystem.ElevatorDailyUpkeep)}/d";
            if (ParkingStalls.IsParking(type))
                return $"{cost} · -{Abbreviate(ParkingStalls.ParkingDailyUpkeep)}/d";
            if (ParkingStalls.IsValet(type))
                return $"{cost} · -{Abbreviate(ParkingStalls.ValetDailyUpkeep)}/d";
            if (ParkingStalls.IsRamp(type))
                return $"{cost} · -{Abbreviate(ParkingStalls.RampDailyUpkeep)}/d";

            switch (type.incomeModel)
            {
                case IncomeModel.UpfrontSale:
                case IncomeModel.QuarterlyRent:
                case IncomeModel.NightlyRate:
                {
                    var income = EconomicBalancingManager.PeriodIncome(
                        type,
                        PricePricing.TierNormal,
                        GameSession.Difficulty,
                        climateSpendMult: 1f,
                        pulseMult: 1f,
                        floorFit01: 1f);
                    if (income <= 0) return cost;
                    if (type.incomeModel == IncomeModel.UpfrontSale)
                        return $"{cost} · {Abbreviate(income)} once";
                    return $"{cost} · {Abbreviate(income)}/d";
                }
                case IncomeModel.TrafficVariable:
                {
                    var visit = ShopVisitRules.PayPerVisit(type);
                    if (visit <= 0) return cost;
                    return $"{cost} · {Abbreviate(visit)}/visit";
                }
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

    public static class ShopDemandFormat
    {
        public static string PoolLine(
            ShopDemandFamily family,
            ShopDemandTier tier,
            ShopDemandPoolSnapshot pool)
        {
            var fulfilledNative = pool.Served + pool.SpilledOut;
            var percent = pool.Generated <= 0
                ? 0
                : UnityEngine.Mathf.RoundToInt(fulfilledNative * 100f / pool.Generated);
            return $"{family} / {tier}: {fulfilledNative}/{pool.Generated} served, " +
                   $"spill +{pool.SpilledIn}/-{pool.SpilledOut}, " +
                   $"unmet {pool.Unmet} ({percent}%)";
        }

        public static string Competition(
            ShopDemandPoolSnapshot pool,
            int openShopCountInPool)
        {
            if (pool.Generated > 0 &&
                pool.Unmet * 5 >= pool.Generated)
                return "Underserved";
            if (openShopCountInPool > pool.Generated)
                return "Oversupplied";
            return "Balanced";
        }

        public static int CountOpenShopsInPool(
            IReadOnlyList<RoomInstance> rooms,
            RoomTypeSO selectedType)
        {
            if (rooms == null || !ShopVisitRules.IsShop(selectedType))
                return 0;

            var family = ShopDemandBalance.FamilyFor(selectedType);
            var tier = ShopDemandBalance.TierForShop(selectedType);
            var count = 0;
            foreach (var room in rooms)
            {
                if (room?.Type == null || room.IsBroken || !ShopVisitRules.IsShop(room.Type))
                    continue;
                if (ShopDemandBalance.FamilyFor(room.Type) == family &&
                    ShopDemandBalance.TierForShop(room.Type) == tier)
                    count++;
            }

            return count;
        }
    }
}
