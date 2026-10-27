using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AtriumMarketingTests
    {
        [Test]
        public void No_atrium_returns_neutral_marketing_values()
        {
            var grid = BuildGrid();

            Assert.IsFalse(AtriumMarketing.HasAtrium(grid));
            Assert.AreEqual(0f, AtriumMarketing.HotelFillBonusFor(grid));
            Assert.AreEqual(0f, AtriumMarketing.CondoDemandBonusFor(grid));
            Assert.AreEqual(1f, AtriumMarketing.StreetSpawnMultiplierFor(grid));
        }

        [Test]
        public void One_working_atrium_returns_marketing_values()
        {
            var grid = BuildGrid();
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: true), new Vector2Int(0, 1), out _));

            Assert.IsTrue(AtriumMarketing.HasAtrium(grid));
            Assert.AreEqual(0.05f, AtriumMarketing.HotelFillBonusFor(grid));
            Assert.AreEqual(0.05f, AtriumMarketing.CondoDemandBonusFor(grid));
            Assert.AreEqual(1.15f, AtriumMarketing.StreetSpawnMultiplierFor(grid));
        }

        [Test]
        public void Two_atriums_do_not_stack_marketing_values()
        {
            var grid = BuildGrid();
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: true), new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: true), new Vector2Int(8, 1), out _));

            Assert.IsTrue(AtriumMarketing.HasAtrium(grid));
            Assert.AreEqual(0.05f, AtriumMarketing.HotelFillBonusFor(grid));
            Assert.AreEqual(0.05f, AtriumMarketing.CondoDemandBonusFor(grid));
            Assert.AreEqual(1.15f, AtriumMarketing.StreetSpawnMultiplierFor(grid));
        }

        [Test]
        public void Broken_atrium_returns_neutral_marketing_values()
        {
            var grid = BuildGrid();
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: true), new Vector2Int(0, 1), out var atrium));
            atrium.Condition = 0;

            Assert.IsFalse(AtriumMarketing.HasAtrium(grid));
            Assert.AreEqual(0f, AtriumMarketing.HotelFillBonusFor(grid));
            Assert.AreEqual(0f, AtriumMarketing.CondoDemandBonusFor(grid));
            Assert.AreEqual(1f, AtriumMarketing.StreetSpawnMultiplierFor(grid));
        }

        [Test]
        public void Leisure_atrium_id_counts_even_without_flag()
        {
            var grid = BuildGrid();
            Assert.IsTrue(grid.TryPlace(AtriumSo(isAtrium: false), new Vector2Int(0, 1), out _));

            Assert.IsTrue(AtriumMarketing.HasAtrium(grid));
        }

        static TowerGrid BuildGrid()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbySo(), 0, 40, 0, out _));
            return grid;
        }

        static RoomTypeSO LobbySo()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO AtriumSo(bool isAtrium)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "leisure_atrium";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            so.size = new Vector2Int(6, 3);
            so.allowAboveGround = true;
            so.isAtrium = isAtrium;
            return so;
        }
    }
}
