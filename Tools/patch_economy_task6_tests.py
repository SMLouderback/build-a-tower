from pathlib import Path

root = Path(__file__).resolve().parents[1]

# --- EconomySystemTests ---
p = root / "Assets/Tests/EditMode/EconomySystemTests.cs"
text = p.read_text(encoding="utf-8")

old_office = """        RoomTypeSO Office(int baseIncome)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = \"office\";
            so.category = RoomCategory.Office;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.QuarterlyRent;
            so.baseIncome = baseIncome;
            return so;
        }"""
new_office = """        // Mid office 9x1: rentEq 120*9*1*1 = 1080; upkeep Mid 25% = 270
        RoomTypeSO Office(int baseIncome = 0)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = OfficeLuxury.MidStandardId;
            so.category = RoomCategory.Office;
            so.luxuryBand = LuxuryBand.Mid;
            so.size = new Vector2Int(9, 1);
            so.allowAboveGround = true;
            so.incomeModel = IncomeModel.QuarterlyRent;
            so.baseIncome = baseIncome; // unused by sim money
            return so;
        }

        const int MidOfficeIncome = 1080;
        const int MidOfficeUpkeep = 270;"""
assert old_office in text, "office helper missing"
text = text.replace(old_office, new_office)

old_condo = """        RoomTypeSO Condo(int baseIncome)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = \"condo\";
            so.category = RoomCategory.Condo;
            so.size = Vector2Int.one;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = baseIncome;
            return so;
        }"""
new_condo = """        // Condo base 1x1 Mid: rentEq 160*0.90 = 144
        RoomTypeSO Condo(int baseIncome = 0)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = CondoLuxury.BaseId;
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Base;
            so.size = Vector2Int.one;
            so.incomeModel = IncomeModel.UpfrontSale;
            so.baseIncome = baseIncome; // unused by sim money
            return so;
        }

        const int CondoBaseSale = 144;"""
assert old_condo in text, "condo helper missing"
text = text.replace(old_condo, new_condo)

replacements = [
(
"""            economy.OnNewDay(grid, agents, wallet);

            Assert.AreEqual(103_000, wallet.Balance);
            Assert.AreEqual(3000, economy.LastIncome);
            Assert.AreEqual(0, economy.LastExpense);
            Assert.AreEqual(3000, economy.LastNet);
            Assert.AreEqual(3000, economy.GetLastRoomIncome(office));
            Assert.AreEqual(0, economy.GetLastRoomExpense(office));
            Assert.AreEqual(3000, economy.GetLastRoomNet(office));
            Assert.AreEqual(3000, office.LifetimeIncome);
            Assert.AreEqual(0, office.LifetimeExpense);
        }""",
"""            economy.OnNewDay(grid, agents, wallet);

            Assert.AreEqual(100_000 + MidOfficeIncome - MidOfficeUpkeep, wallet.Balance);
            Assert.AreEqual(MidOfficeIncome, economy.LastIncome);
            Assert.AreEqual(MidOfficeUpkeep, economy.LastExpense);
            Assert.AreEqual(MidOfficeIncome - MidOfficeUpkeep, economy.LastNet);
            Assert.AreEqual(MidOfficeIncome, economy.GetLastRoomIncome(office));
            Assert.AreEqual(MidOfficeUpkeep, economy.GetLastRoomExpense(office));
            Assert.AreEqual(MidOfficeIncome - MidOfficeUpkeep, economy.GetLastRoomNet(office));
            Assert.AreEqual(MidOfficeIncome, office.LifetimeIncome);
            Assert.AreEqual(MidOfficeUpkeep, office.LifetimeExpense);
        }"""
),
(
"""            Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
            Assert.IsFalse(economy.TrySellCondo(condoRoom, wallet));

            Assert.AreEqual(150_000, wallet.Balance);
            Assert.IsTrue(condoRoom.CondoSold);
            Assert.AreEqual(150_000, economy.GetLastRoomIncome(condoRoom));
            Assert.AreEqual(150_000, condoRoom.LifetimeIncome);
            Assert.IsTrue(economy.HasRecordedEconomyEvent);
        }""",
"""            Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
            Assert.IsFalse(economy.TrySellCondo(condoRoom, wallet));

            Assert.AreEqual(CondoBaseSale, wallet.Balance);
            Assert.IsTrue(condoRoom.CondoSold);
            Assert.AreEqual(CondoBaseSale, economy.GetLastRoomIncome(condoRoom));
            Assert.AreEqual(CondoBaseSale, condoRoom.LifetimeIncome);
            Assert.IsTrue(economy.HasRecordedEconomyEvent);
        }"""
),
(
"""            economy.OnNewDay(grid, agents, wallet, currentStars: 3);

            Assert.AreEqual(3900, economy.LastIncome);
            Assert.AreEqual(103_900, wallet.Balance);
        }""",
"""            economy.OnNewDay(grid, agents, wallet, currentStars: 3);

            // High tier 1.3x → 1404 income; upkeep still 270
            Assert.AreEqual(1404, economy.LastIncome);
            Assert.AreEqual(100_000 + 1404 - MidOfficeUpkeep, wallet.Balance);
        }"""
),
(
"""            Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
            Assert.AreEqual(105_000, wallet.Balance);
        }""",
"""            Assert.IsTrue(economy.TrySellCondo(condoRoom, wallet));
            // Low tier 0.7x → Round(144*0.7)=101
            Assert.AreEqual(101, wallet.Balance);
        }"""
),
(
"""        [TestCase(28, 14)]
        [TestCase(65, 33)]
        [TestCase(100, 50)]
        public void Shop_upkeep_rounds_half_pay_cap_away_from_zero(int cap, int expected)
        {
            Assert.AreEqual(expected, ShopDemandBalance.DailyUpkeep(FastFoodShop(cap)));
        }""",
"""        [Test]
        public void Shop_upkeep_uses_vpsf_period_upkeep()
        {
            // tier1 Mid: rentEq 110*0.70=77; upkeep 25% → 19
            Assert.AreEqual(19, ShopDemandBalance.DailyUpkeep(FastFoodShop()));
        }"""
),
(
"""            Assert.AreEqual(25, economy.GetLastRoomExpense(shop));
            Assert.AreEqual(0, shop.ShopRevenueYesterday);
            Assert.AreEqual(25, shop.ShopUpkeepYesterday);
            Assert.AreEqual(-25, shop.ShopNetYesterday);
            Assert.AreEqual(25, shop.LifetimeExpense);
            Assert.AreEqual(99_975, wallet.Balance);
        }""",
"""            Assert.AreEqual(19, economy.GetLastRoomExpense(shop));
            Assert.AreEqual(0, shop.ShopRevenueYesterday);
            Assert.AreEqual(19, shop.ShopUpkeepYesterday);
            Assert.AreEqual(-19, shop.ShopNetYesterday);
            Assert.AreEqual(19, shop.LifetimeExpense);
            Assert.AreEqual(99_981, wallet.Balance);
        }"""
),
(
"""            Assert.AreEqual(0, shop.ShopRevenueYesterday);
            Assert.AreEqual(25, shop.ShopUpkeepYesterday);
            Assert.AreEqual(-25, shop.ShopNetYesterday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(99_975, wallet.Balance);
        }""",
"""            Assert.AreEqual(0, shop.ShopRevenueYesterday);
            Assert.AreEqual(19, shop.ShopUpkeepYesterday);
            Assert.AreEqual(-19, shop.ShopNetYesterday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(99_981, wallet.Balance);
        }"""
),
(
"""            Assert.AreEqual(120, economy.LastIncome);
            Assert.AreEqual(20, economy.LastExpense);
            Assert.AreEqual(100_100, wallet.Balance);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(120, shop.LifetimeIncome);
            Assert.AreEqual(20, shop.LifetimeExpense);
            Assert.AreEqual(120, economy.GetLastRoomIncome(shop));
            Assert.AreEqual(120, shop.ShopRevenueYesterday);
            Assert.AreEqual(20, shop.ShopUpkeepYesterday);
            Assert.AreEqual(100, shop.ShopNetYesterday);
            Assert.IsTrue(economy.HasRecordedEconomyEvent);
        }""",
"""            Assert.AreEqual(120, economy.LastIncome);
            Assert.AreEqual(19, economy.LastExpense);
            Assert.AreEqual(100_101, wallet.Balance);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(120, shop.LifetimeIncome);
            Assert.AreEqual(19, shop.LifetimeExpense);
            Assert.AreEqual(120, economy.GetLastRoomIncome(shop));
            Assert.AreEqual(120, shop.ShopRevenueYesterday);
            Assert.AreEqual(19, shop.ShopUpkeepYesterday);
            Assert.AreEqual(101, shop.ShopNetYesterday);
            Assert.IsTrue(economy.HasRecordedEconomyEvent);
        }"""
),
(
"""                Assert.AreEqual(BuildEconomy.ApplyIncome(101), shop.ShopRevenueYesterday);
                Assert.AreEqual(81, shop.ShopRevenueYesterday);
                Assert.AreEqual(100_061, wallet.Balance);
            }""",
"""                Assert.AreEqual(BuildEconomy.ApplyIncome(101), shop.ShopRevenueYesterday);
                Assert.AreEqual(81, shop.ShopRevenueYesterday);
                // Hard upkeep Round(77*0.25*0.8)=15
                Assert.AreEqual(100_000 + 81 - 15, wallet.Balance);
            }"""
),
(
"""            // Max at 0★ → 10% demand; seed 7 first roll is ~0.38 so income is skipped.
            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(100_000, wallet.Balance);
        }""",
"""            // Max at 0★ → 10% demand; seed 7 first roll is ~0.38 so income is skipped.
            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(MidOfficeUpkeep, economy.LastExpense);
            Assert.AreEqual(100_000 - MidOfficeUpkeep, wallet.Balance);
        }"""
),
(
"""            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(100_000, wallet.Balance);
            Assert.AreEqual(38, office.Condition);

            office.Condition = 0;
            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(0, economy.LastIncome);
            Assert.IsTrue(office.IsBroken);
        }""",
"""            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(MidOfficeUpkeep, economy.LastExpense);
            Assert.AreEqual(100_000 - MidOfficeUpkeep, wallet.Balance);
            Assert.AreEqual(38, office.Condition);

            office.Condition = 0;
            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(0, economy.LastIncome);
            Assert.IsTrue(office.IsBroken);
        }"""
),
(
"""            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(20, economy.LastExpense);
            Assert.AreEqual(99_980, wallet.Balance);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(0, shop.ShopRevenueYesterday);
            Assert.AreEqual(20, shop.ShopUpkeepYesterday);
        }""",
"""            Assert.AreEqual(0, economy.LastIncome);
            Assert.AreEqual(19, economy.LastExpense);
            Assert.AreEqual(99_981, wallet.Balance);
            Assert.AreEqual(0, shop.VisitsToday);
            Assert.AreEqual(0, shop.ShopEarningsToday);
            Assert.AreEqual(0, shop.ShopRevenueYesterday);
            Assert.AreEqual(19, shop.ShopUpkeepYesterday);
        }"""
),
(
"""            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(3000, economy.LastNet);
            Assert.AreEqual(3000f, economy.AverageDailyProfit);

            // Second midnight with no occupants → LastNet 0; average falls to 1500.
            economy.OnNewDay(grid, new List<Agent>(), wallet);
            Assert.AreEqual(0, economy.LastNet);
            Assert.AreEqual(1500f, economy.AverageDailyProfit);
        }""",
"""            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(MidOfficeIncome - MidOfficeUpkeep, economy.LastNet);
            Assert.AreEqual((float)(MidOfficeIncome - MidOfficeUpkeep), economy.AverageDailyProfit);

            // Second midnight vacant: upkeep only.
            economy.OnNewDay(grid, new List<Agent>(), wallet);
            Assert.AreEqual(-MidOfficeUpkeep, economy.LastNet);
            Assert.AreEqual(
                (MidOfficeIncome - MidOfficeUpkeep - MidOfficeUpkeep) / 2f,
                economy.AverageDailyProfit);
        }"""
),
]
for old, new in replacements:
    assert old in text, f"missing block:\n{old[:80]}"
    text = text.replace(old, new)

p.write_text(text, encoding="utf-8")
print("patched", p.name)

# --- RoomEconomyFormatTests ---
p = root / "Assets/Tests/EditMode/RoomEconomyFormatTests.cs"
p.write_text("""using BuildATower;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class RoomEconomyFormatTests
    {
        [SetUp]
        public void SetUp() => GameSession.ResetForTests();

        static RoomTypeSO MidOffice()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = OfficeLuxury.MidStandardId;
            type.category = RoomCategory.Office;
            type.luxuryBand = LuxuryBand.Mid;
            type.size = new Vector2Int(4, 1);
            type.incomeModel = IncomeModel.QuarterlyRent;
            return type;
        }

        static RoomTypeSO CondoBase()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = CondoLuxury.BaseId;
            type.category = RoomCategory.Condo;
            type.luxuryBand = LuxuryBand.Base;
            type.size = Vector2Int.one;
            type.incomeModel = IncomeModel.UpfrontSale;
            return type;
        }

        static RoomTypeSO FastFood()
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = "shop_food_fast";
            type.category = RoomCategory.Commercial;
            type.buildFamily = BuildFamily.Shops;
            type.buildSubgroup = BuildSubgroup.Food;
            type.size = Vector2Int.one;
            type.incomeModel = IncomeModel.TrafficVariable;
            return type;
        }

        [Test]
        public void Recurring_room_shows_daily_income()
        {
            var office = MidOffice();
            // rentEq 480; build 8640; income 480
            Assert.AreEqual("Cost: $8,640", RoomEconomyFormat.CostLine(office));
            StringAssert.Contains("$480 / day", RoomEconomyFormat.IncomeLine(office));
            Assert.AreEqual("$8.6k · $480/d", RoomEconomyFormat.ButtonTag(office));
        }

        [Test]
        public void Upfront_sale_room_shows_one_time_income()
        {
            var condo = CondoBase();
            // rentEq 144; build 2592; sale 144
            StringAssert.Contains("$144 once", RoomEconomyFormat.IncomeLine(condo));
            Assert.AreEqual("$2.6k · $144 once", RoomEconomyFormat.ButtonTag(condo));
            StringAssert.Contains("Upkeep:", RoomEconomyFormat.UpkeepLine(condo));
        }

        [Test]
        public void Elevator_shows_per_floor_cost_and_upkeep()
        {
            var elevator = ScriptableObject.CreateInstance<RoomTypeSO>();
            elevator.id = "elevator_normal";
            elevator.isElevatorShaft = true;
            elevator.category = RoomCategory.Transit;
            elevator.size = new Vector2Int(1, 2);

            var unit = BuildEconomy.UnitBuildCost(elevator);
            Assert.AreEqual($"Cost: ${unit:N0} / floor", RoomEconomyFormat.CostLine(elevator));
            StringAssert.Contains($"${EconomySystem.ElevatorDailyUpkeep:N0} / day", RoomEconomyFormat.UpkeepLine(elevator));
            StringAssert.Contains("/fl", RoomEconomyFormat.ButtonTag(elevator));
        }

        [Test]
        public void Format_shows_per_visit_and_visits_today()
        {
            var shop = FastFood();
            var instance = new RoomInstance(7, shop, Vector2Int.zero, Vector2Int.one);
            instance.RecordVisit();
            instance.RecordShopSpend(25);
            instance.RecordVisit();
            instance.RecordShopSpend(40);

            StringAssert.Contains("/ visit", RoomEconomyFormat.IncomeLine(shop));
            StringAssert.Contains("spent dollars at midnight", RoomEconomyFormat.IncomeLine(shop));

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(lines, "Visits today: 2");
            CollectionAssert.Contains(lines, "Earnings today: $65");
            StringAssert.Contains("/visit", RoomEconomyFormat.ButtonTag(shop));
        }

        [Test]
        public void Selected_unit_shows_tier_scaled_income()
        {
            var office = MidOffice();
            var instance = new RoomInstance(9, office, Vector2Int.zero, office.size);
            instance.PriceTier = PricePricing.TierHigh;

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(lines, "Income: $624 / day occupied");
        }

        [Test]
        public void Selected_shop_lines_show_pool_upkeep_and_negative_net()
        {
            var shop = FastFood();
            shop.requiredStars = 0;
            var instance = new RoomInstance(10, shop, Vector2Int.zero, Vector2Int.one);
            instance.ArchiveShopDay(creditedRevenue: 10, upkeep: 19);
            var demand = new ShopDemandSystem();
            demand.BeginDay(new List<Agent>(), stars: 0, climateMultiplier: 1f);

            var lines = RoomEconomyFormat.SelectedUnitLines(
                instance,
                null,
                null,
                demand,
                openShopCountInPool: 1);

            CollectionAssert.Contains(lines, "Demand: Food / Budget");
            CollectionAssert.Contains(lines, "Visits yesterday: 0");
            CollectionAssert.Contains(lines, "Yesterday revenue: $10");
            CollectionAssert.Contains(lines, "Daily upkeep: $19");
            CollectionAssert.Contains(lines, "Yesterday net: -$9");
            CollectionAssert.Contains(lines, "Competition: Underserved");

            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Newly_built_shop_shows_known_daily_upkeep_before_first_rollover()
        {
            var shop = FastFood();
            shop.buildSubgroup = BuildSubgroup.Retail;
            var instance = new RoomInstance(11, shop, Vector2Int.zero, Vector2Int.one);

            var lines = RoomEconomyFormat.SelectedUnitLines(instance, null, null);

            CollectionAssert.Contains(lines, "Daily upkeep: $19");
            CollectionAssert.Contains(lines, "Yesterday net: $0");

            Object.DestroyImmediate(shop);
        }

        [Test]
        public void Selected_condo_reports_sale_state()
        {
            var condo = CondoBase();
            var instance = new RoomInstance(8, condo, Vector2Int.zero, Vector2Int.one);

            var beforeSale = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(beforeSale, "Status: For sale — no payout yet");

            var buyer = new Agent(1, AgentRole.CondoResident, instance, Vector2Int.zero);
            var duringMove = RoomEconomyFormat.SelectedUnitLines(
                instance,
                new List<Agent> { buyer },
                new EconomySystem());
            CollectionAssert.Contains(duringMove, "Status: Buyer moving in — no payout yet");

            instance.CondoSold = true;
            var afterSale = RoomEconomyFormat.SelectedUnitLines(instance, null, new EconomySystem());
            CollectionAssert.Contains(afterSale, "Status: Sold");
        }

        [TestCase(500, "$500")]
        [TestCase(3000, "$3k")]
        [TestCase(4500, "$4.5k")]
        [TestCase(150_000, "$150k")]
        [TestCase(2_000_000, "$2M")]
        public void Abbreviate_shortens_dollar_amounts(int dollars, string expected)
        {
            Assert.AreEqual(expected, RoomEconomyFormat.Abbreviate(dollars));
        }
    }
}
""", encoding="utf-8")
print("rewrote RoomEconomyFormatTests")

# --- ShopVisitRules PayPerVisit ---
p = root / "Assets/Tests/EditMode/ShopVisitRulesTests.cs"
text = p.read_text(encoding="utf-8")
old = """        [Test]
        public void PayPerVisit_returns_base_income()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.baseIncome = 40;
            Assert.AreEqual(40, ShopVisitRules.PayPerVisit(so));
            Assert.AreEqual(0, ShopVisitRules.PayPerVisit(null));
        }"""
new = """        [Test]
        public void PayPerVisit_returns_vpsf_period_income()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = \"shop_food_fast\";
            so.category = RoomCategory.Commercial;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.size = Vector2Int.one;
            // tier1 Mid: 110 * 0.70 = 77
            Assert.AreEqual(77, ShopVisitRules.PayPerVisit(so));
            Assert.AreEqual(0, ShopVisitRules.PayPerVisit(null));
        }"""
assert old in text
text = text.replace(old, new)
p.write_text(text, encoding="utf-8")
print("patched ShopVisitRulesTests")

# --- VpsfCatalog unknown ---
p = root / "Assets/Tests/EditMode/VpsfCatalogTests.cs"
text = p.read_text(encoding="utf-8")
old = """        [Test]
        public void TryIdentity_unknown_id_returns_false()
        {
            var so = Room(\"shop_fast_food\", RoomCategory.Commercial, LuxuryBand.None);
            Assert.IsFalse(VpsfCatalog.TryIdentity(so, out _, out _, out _));
        }"""
new = """        [Test]
        public void TryIdentity_unknown_id_returns_false()
        {
            var so = Room(\"totally_unknown_room_xyz\", RoomCategory.Structure, LuxuryBand.None);
            Assert.IsFalse(VpsfCatalog.TryIdentity(so, out _, out _, out _));
        }

        [Test]
        public void TryIdentity_maps_shops_and_infrastructure()
        {
            var shop = Room(\"shop_food_fast\", RoomCategory.Commercial, LuxuryBand.None);
            Assert.IsTrue(VpsfCatalog.TryIdentity(shop, out var shopFamily, out var shopTier, out _));
            Assert.AreEqual(EconomicFamily.Shops, shopFamily);
            Assert.AreEqual(1, shopTier);

            var lobby = Room(\"lobby\", RoomCategory.Structure, LuxuryBand.None);
            lobby.isLobby = true;
            Assert.IsTrue(VpsfCatalog.TryIdentity(lobby, out var lobbyFamily, out _, out _));
            Assert.AreEqual(EconomicFamily.Infrastructure, lobbyFamily);
        }"""
assert old in text
text = text.replace(old, new)
p.write_text(text, encoding="utf-8")
print("patched VpsfCatalogTests")

# --- EconomicBalancingManager unknown ---
p = root / "Assets/Tests/EditMode/EconomicBalancingManagerTests.cs"
text = p.read_text(encoding="utf-8")
old = """        [Test]
        public void Unknown_or_null_type_returns_zero()
        {
            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.id = \"shop_fast_food\";
            shop.category = RoomCategory.Commercial;
            shop.size = new Vector2Int(2, 1);

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(shop, GameDifficulty.Normal));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodIncome(
                shop, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodUpkeep(shop, GameDifficulty.Normal, 1f));

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(null, GameDifficulty.Normal));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodIncome(
                null, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodUpkeep(null, GameDifficulty.Normal, 1f));
        }"""
new = """        [Test]
        public void Unknown_or_null_type_returns_zero()
        {
            var unknown = ScriptableObject.CreateInstance<RoomTypeSO>();
            unknown.id = \"totally_unknown_room_xyz\";
            unknown.category = RoomCategory.Structure;
            unknown.size = new Vector2Int(2, 1);

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(unknown, GameDifficulty.Normal));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodIncome(
                unknown, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodUpkeep(unknown, GameDifficulty.Normal, 1f));

            Assert.AreEqual(0, EconomicBalancingManager.BuildCost(null, GameDifficulty.Normal));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodIncome(
                null, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(0, EconomicBalancingManager.PeriodUpkeep(null, GameDifficulty.Normal, 1f));
        }

        [Test]
        public void Shop_identity_returns_non_zero_money()
        {
            var shop = ScriptableObject.CreateInstance<RoomTypeSO>();
            shop.id = \"shop_food_fast\";
            shop.category = RoomCategory.Commercial;
            shop.size = new Vector2Int(2, 1);
            // tier1 Mid: 110*2*0.70 = 154 income; build 154*18 = 2772
            Assert.AreEqual(2772, EconomicBalancingManager.BuildCost(shop, GameDifficulty.Normal));
            Assert.AreEqual(154, EconomicBalancingManager.PeriodIncome(
                shop, PricePricing.TierNormal, GameDifficulty.Normal, 1f, 1f, 1f));
            Assert.AreEqual(39, EconomicBalancingManager.PeriodUpkeep(shop, GameDifficulty.Normal, 1f));
        }"""
assert old in text
text = text.replace(old, new)
p.write_text(text, encoding="utf-8")
print("patched EconomicBalancingManagerTests")

# --- BuildGraceRefund condo ---
p = root / "Assets/Tests/EditMode/BuildGraceRefundTests.cs"
text = p.read_text(encoding="utf-8")
old = """        [Test]
        public void Condo_sale_increments_LifetimeIncome()
        {
            var condo = ScriptableObject.CreateInstance<RoomTypeSO>();
            condo.incomeModel = IncomeModel.UpfrontSale;
            condo.baseIncome = 150_000;
            var room = new RoomInstance(1, condo, Vector2Int.zero, Vector2Int.one);
            var economy = new EconomySystem();
            var wallet = new FundsWallet(0);
            Assert.IsTrue(economy.TrySellCondo(room, wallet));
            Assert.AreEqual(150_000, room.LifetimeIncome);
        }"""
new = """        [Test]
        public void Condo_sale_increments_LifetimeIncome()
        {
            var condo = ScriptableObject.CreateInstance<RoomTypeSO>();
            condo.id = CondoLuxury.BaseId;
            condo.category = RoomCategory.Condo;
            condo.luxuryBand = LuxuryBand.Base;
            condo.incomeModel = IncomeModel.UpfrontSale;
            condo.size = Vector2Int.one;
            var room = new RoomInstance(1, condo, Vector2Int.zero, Vector2Int.one);
            var economy = new EconomySystem();
            var wallet = new FundsWallet(0);
            Assert.IsTrue(economy.TrySellCondo(room, wallet));
            Assert.AreEqual(144, room.LifetimeIncome);
        }"""
assert old in text
text = text.replace(old, new)
p.write_text(text, encoding="utf-8")
print("patched BuildGraceRefundTests")

# --- AgentSystemTests condo wallet ---
p = root / "Assets/Tests/EditMode/AgentSystemTests.cs"
text = p.read_text(encoding="utf-8")
if "Assert.AreEqual(condo.Type.baseIncome, wallet.Balance);" in text:
    text = text.replace(
        "Assert.AreEqual(condo.Type.baseIncome, wallet.Balance);",
        "Assert.AreEqual(\n                EconomicBalancingManager.PeriodIncome(\n                    condo.Type, PricePricing.TierNormal, GameSession.Difficulty, 1f, 1f, 1f),\n                wallet.Balance);"
    )
    p.write_text(text, encoding="utf-8")
    print("patched AgentSystemTests")
else:
    print("AgentSystemTests already ok or missing assert")

print("done")
