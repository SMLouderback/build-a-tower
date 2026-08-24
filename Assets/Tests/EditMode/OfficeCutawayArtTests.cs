using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class OfficeCutawayArtTests
    {
        [TearDown]
        public void TearDown() => OfficeCutawayArt.ResetForTests();

        [Test]
        public void ClampArtVariant_maps_to_one_or_two()
        {
            Assert.AreEqual(1, OfficeCutawayArt.ClampArtVariant(0));
            Assert.AreEqual(1, OfficeCutawayArt.ClampArtVariant(1));
            Assert.AreEqual(2, OfficeCutawayArt.ClampArtVariant(2));
            Assert.AreEqual(1, OfficeCutawayArt.ClampArtVariant(99));
        }

        [Test]
        public void RollArtVariant_returns_one_or_two()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < 20; i++)
                seen.Add(OfficeCutawayArt.RollArtVariant());
            Assert.That(seen, Is.SubsetOf(new[] { 1, 2 }));
            Assert.IsNotEmpty(seen);
        }

        [Test]
        public void Place_office_assigns_art_variant_one_or_two()
        {
            OfficeCutawayArt.RollArtVariantForTests = () => 2;
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Office(), new Vector2Int(0, 1), out var room));
            Assert.AreEqual(2, room.ArtVariant);
        }

        [Test]
        public void AssignArtVariantIfUnset_keeps_existing_variant()
        {
            var room = new RoomInstance(1, Office(), new Vector2Int(0, 1), new Vector2Int(9, 1))
            {
                ArtVariant = 2
            };
            OfficeCutawayArt.RollArtVariantForTests = () => 1;
            OfficeCutawayArt.AssignArtVariantIfUnset(room);
            Assert.AreEqual(2, room.ArtVariant);
        }

        [Test]
        public void Place_lobby_leaves_art_variant_unset()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 20, 0, out var lobby));
            Assert.AreEqual(0, lobby.ArtVariant);
        }

        [Test]
        public void Place_retail_leaves_art_variant_unset()
        {
            var grid = GridWithLobby();
            Assert.IsTrue(grid.TryPlace(Retail(), new Vector2Int(0, 1), out var room));
            Assert.AreEqual(0, room.ArtVariant);
        }

        static TowerGrid GridWithLobby()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            return grid;
        }

        static RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO Office()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office";
            so.category = RoomCategory.Office;
            so.allowAboveGround = true;
            so.size = new Vector2Int(9, 1);
            return so;
        }

        static RoomTypeSO Retail()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "retail";
            so.category = RoomCategory.Commercial;
            so.allowAboveGround = true;
            so.size = new Vector2Int(16, 1);
            return so;
        }
    }
}
