from pathlib import Path

p = Path(__file__).resolve().parents[1] / "Assets/Tests/EditMode/CommercialVisitTests.cs"
t = p.read_text(encoding="utf-8")

t = t.replace(
    "Assert.That(shop.ShopEarningsToday, Is.InRange(1, 40));",
    "Assert.That(shop.ShopEarningsToday, Is.InRange(1, ShopVisitRules.PayPerVisit(shop.Type)));",
)

old = """            Assert.AreEqual(65, economy.LastIncome);
            Assert.AreEqual(20, economy.LastExpense);
            Assert.AreEqual(45, wallet.Balance);
            Assert.AreEqual(65, shop.LifetimeIncome);
            Assert.AreEqual(20, shop.LifetimeExpense);
            Assert.AreEqual(65, economy.GetLastRoomIncome(shop));
            Assert.AreEqual(20, economy.GetLastRoomExpense(shop));
            Assert.AreEqual(65, shop.ShopRevenueYesterday);
            Assert.AreEqual(20, shop.ShopUpkeepYesterday);
            Assert.AreEqual(45, shop.ShopNetYesterday);"""
new = """            Assert.AreEqual(65, economy.LastIncome);
            Assert.AreEqual(19, economy.LastExpense);
            Assert.AreEqual(46, wallet.Balance);
            Assert.AreEqual(65, shop.LifetimeIncome);
            Assert.AreEqual(19, shop.LifetimeExpense);
            Assert.AreEqual(65, economy.GetLastRoomIncome(shop));
            Assert.AreEqual(19, economy.GetLastRoomExpense(shop));
            Assert.AreEqual(65, shop.ShopRevenueYesterday);
            Assert.AreEqual(19, shop.ShopUpkeepYesterday);
            Assert.AreEqual(46, shop.ShopNetYesterday);"""
assert old in t, "shop earnings midnight block missing"
t = t.replace(old, new)

t = t.replace(
    """            fast.Type.baseIncome = 20;
            restaurant.Type.baseIncome = 20;
""",
    "",
)

p.write_text(t, encoding="utf-8")
print("patched CommercialVisitTests")

p = Path(__file__).resolve().parents[1] / "Assets/Tests/EditMode/ShopVisitRulesTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    """            // tier1 Mid: 110 * 0.70 = 77
            Assert.AreEqual(77, ShopVisitRules.PayPerVisit(so));""",
    """            // tier1 Mid period 77 * VisitPriceOfPeriodIncome 0.5 → 39
            Assert.AreEqual(39, ShopVisitRules.PayPerVisit(so));""",
)
p.write_text(t, encoding="utf-8")
print("patched ShopVisitRulesTests")
