from pathlib import Path

root = Path(__file__).resolve().parents[1]

p = root / "Assets/Tests/EditMode/EconomySystemTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    "economy.OnNewDay(grid, agents, wallet);\n\n            Assert.AreEqual(100_000 + MidOfficeIncome - MidOfficeUpkeep, wallet.Balance);",
    "economy.OnNewDay(grid, agents, wallet, currentStars: 1);\n\n            Assert.AreEqual(100_000 + MidOfficeIncome - MidOfficeUpkeep, wallet.Balance);",
)
t = t.replace(
    """            economy.OnNewDay(grid, agents, wallet);
            Assert.AreEqual(MidOfficeIncome - MidOfficeUpkeep, economy.LastNet);
            Assert.AreEqual((float)(MidOfficeIncome - MidOfficeUpkeep), economy.AverageDailyProfit);

            // Second midnight vacant: upkeep only.
            economy.OnNewDay(grid, new List<Agent>(), wallet);""",
    """            economy.OnNewDay(grid, agents, wallet, currentStars: 1);
            Assert.AreEqual(MidOfficeIncome - MidOfficeUpkeep, economy.LastNet);
            Assert.AreEqual((float)(MidOfficeIncome - MidOfficeUpkeep), economy.AverageDailyProfit);

            // Second midnight vacant: upkeep only.
            economy.OnNewDay(grid, new List<Agent>(), wallet, currentStars: 1);""",
)
p.write_text(t, encoding="utf-8")
print("EconomySystemTests")

p = root / "Assets/Tests/EditMode/ShopVisitRulesTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    "Assert.AreEqual(39, ShopVisitRules.PayPerVisit(so));",
    "Assert.AreEqual(27, ShopVisitRules.PayPerVisit(so)); // Round(77*0.35)",
)
t = t.replace(
    "Assert.AreEqual(38, ShopVisitRules.PayPerVisit(so)); // Round(77*0.5) banker's",
    "Assert.AreEqual(27, ShopVisitRules.PayPerVisit(so)); // Round(77*0.35)",
)
# also handle comment form from earlier patch
t = t.replace(
    """            // tier1 Mid period 77 * VisitPriceOfPeriodIncome 0.5 → 39
            Assert.AreEqual(39, ShopVisitRules.PayPerVisit(so));""",
    """            // tier1 Mid period 77 * VisitPriceOfPeriodIncome 0.35 → 27
            Assert.AreEqual(27, ShopVisitRules.PayPerVisit(so));""",
)
if "Assert.AreEqual(27, ShopVisitRules.PayPerVisit(so));" not in t:
    # last resort replace any PayPerVisit assert in that test
    import re
    t2, n = re.subn(
        r"Assert\.AreEqual\(\d+, ShopVisitRules\.PayPerVisit\(so\)\);",
        "Assert.AreEqual(27, ShopVisitRules.PayPerVisit(so));",
        t,
        count=1,
    )
    assert n == 1, "paypervisit assert missing"
    t = t2
p.write_text(t, encoding="utf-8")
print("ShopVisitRulesTests")

p = root / "Assets/Tests/EditMode/AgentSystemTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    "}, currentStars: 0, averageCrime: 0f);",
    "}, currentStars: 1, averageCrime: 0f);",
)
old = """            Assert.AreEqual(
                EconomicBalancingManager.PeriodIncome(
                    condo.Type, PricePricing.TierNormal, GameSession.Difficulty, 1f, 1f, 1f),
                wallet.Balance);"""
new = """            Assert.AreEqual(
                EconomicBalancingManager.PeriodIncome(
                    condo.Type,
                    PricePricing.TierNormal,
                    GameSession.Difficulty,
                    1f,
                    1f,
                    EconomySystem.FloorFitFor(condo, currentStars: 1)),
                wallet.Balance);"""
if old in t:
    t = t.replace(old, new)
elif "EconomySystem.FloorFitFor(condo" not in t:
    raise SystemExit("condo assert missing")
p.write_text(t, encoding="utf-8")
print("AgentSystemTests")

p = root / "Assets/Tests/EditMode/CommercialVisitTests.cs"
t = p.read_text(encoding="utf-8")
old = """        public void TryBeginCommercialTrip_skips_restaurant_when_remaining_below_price()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithRestaurant();
            PlaceAgentWorkingAtOffice(agent);
            agent.DisposableRemaining = 30;
            agent.DisposableDayIndex = clock.DayIndex;

            Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreEqual(-1, agent.CommercialTripDay);
            Assert.IsNull(agent.VisitTarget);
            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.AreEqual(30, agent.DisposableRemaining);
        }"""
new = """        public void TryBeginCommercialTrip_skips_restaurant_when_remaining_below_price()
        {
            var (grid, shop, agents, agent, clock) = SetupOfficeWithRestaurant();
            PlaceAgentWorkingAtOffice(agent);
            shop.Type.requiredStars = 5; // premium VPSF visit list above remaining
            agent.DisposableRemaining = 30;
            agent.DisposableDayIndex = clock.DayIndex;

            Assert.IsFalse(agents.TryBeginCommercialTrip(agent, grid, clock, AgentPhase.Working));
            Assert.AreEqual(-1, agent.CommercialTripDay);
            Assert.IsNull(agent.VisitTarget);
            Assert.AreEqual(0, shop.ConcurrentVisitors);
            Assert.AreEqual(30, agent.DisposableRemaining);
        }"""
assert old in t or "shop.Type.requiredStars = 5" in t
if old in t:
    t = t.replace(old, new)

needle = """            Assert.IsTrue(grid.TryPlace(Restaurant(), new Vector2Int(9, 1), out _));
            Assert.IsTrue(grid.TryPlace(Retail(), new Vector2Int(10, 1), out var retail));"""
repl = """            Assert.IsTrue(grid.TryPlace(Restaurant(), new Vector2Int(9, 1), out var restaurant));
            restaurant.Type.requiredStars = 5;
            Assert.IsTrue(grid.TryPlace(Retail(), new Vector2Int(10, 1), out var retail));"""
if needle in t:
    t = t.replace(needle, repl)

# Street comment cleanup
t = t.replace(
    """            // Fast Food (4) + Restaurant (6) = 10 slots so the street cap (8) is the binding limit.
            // Price floors at Street band min ($20) so affordability never blocks the spawn-cap test.
""",
    """            // Fast Food (4) + Restaurant (6) = 10 slots so the street cap (8) is the binding limit.
""",
)

p.write_text(t, encoding="utf-8")
print("CommercialVisitTests")
print("done")
