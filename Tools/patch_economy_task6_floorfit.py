from pathlib import Path

root = Path(__file__).resolve().parents[1]

p = root / "Assets/Tests/EditMode/EconomySystemTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    "const int MidOfficeIncome = 1080;",
    "const int MidOfficeIncome = 810; // 1080 * street Mid fit 0.75",
)
t = t.replace(
    """            // High tier 1.3x → 1404 income; upkeep still 270
            Assert.AreEqual(1404, economy.LastIncome);
            Assert.AreEqual(100_000 + 1404 - MidOfficeUpkeep, wallet.Balance);""",
    """            // High tier 1.3x * street fit 0.75 → 1053; upkeep still 270
            Assert.AreEqual(1053, economy.LastIncome);
            Assert.AreEqual(100_000 + 1053 - MidOfficeUpkeep, wallet.Balance);""",
)
t = t.replace(
    "const int CondoBaseSale = 144;",
    "const int CondoBaseSale = 108; // 144 * street Mid fit 0.75",
)
t = t.replace(
    """            // Low tier 0.7x → Round(144*0.7)=101
            Assert.AreEqual(101, wallet.Balance);""",
    """            // Low tier 0.7x * street fit 0.75 → Round(75.6)=76
            Assert.AreEqual(76, wallet.Balance);""",
)
p.write_text(t, encoding="utf-8")
print("EconomySystemTests floor-fit")

p = root / "Assets/Tests/EditMode/RoomEconomyFormatTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    'CollectionAssert.Contains(lines, "Income: $624 / day occupied");',
    'CollectionAssert.Contains(lines, "Income: $468 / day occupied"); // 624 * street Mid fit 0.75',
)
p.write_text(t, encoding="utf-8")
print("RoomEconomyFormatTests floor-fit")

p = root / "Assets/Tests/EditMode/BuildGraceRefundTests.cs"
t = p.read_text(encoding="utf-8")
t = t.replace(
    "Assert.AreEqual(144, room.LifetimeIncome);",
    "Assert.AreEqual(108, room.LifetimeIncome); // street Mid fit",
)
p.write_text(t, encoding="utf-8")
print("BuildGraceRefundTests floor-fit")
