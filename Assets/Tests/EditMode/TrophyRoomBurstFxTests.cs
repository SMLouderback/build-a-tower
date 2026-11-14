using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class TrophyRoomBurstFxTests
    {
        [TearDown]
        public void TearDown() => TrophyRoomBurstFx.ResetForTests();

        [Test]
        public void Play_requests_one_burst_wave_per_metal_up_call()
        {
            TrophyRoomBurstFx.Play(new[] { new Vector2(100f, 200f) }, TrophyMetal.Gold);

            Assert.AreEqual(1, TrophyRoomBurstFx.BurstsRequested);
            Assert.AreEqual(1, TrophyRoomBurstFx.LastBurstAnchorCount);
            Assert.IsTrue(TrophyRoomBurstFx.IsPlaying);
        }

        [Test]
        public void HandleTrophyUpgrade_uses_every_placed_trophy_room_anchor()
        {
            var grid = GridWithTwoTrophyRooms();

            TrophyRoomBurstFx.HandleTrophyUpgrade(grid, null, ChallengeSubject.Stars, TrophyMetal.Silver);

            Assert.AreEqual(1, TrophyRoomBurstFx.BurstsRequested);
            Assert.AreEqual(2, TrophyRoomBurstFx.LastBurstAnchorCount);
        }

        [Test]
        public void TryClaim_raises_OnTrophyUpgraded_once_when_metal_rises()
        {
            var def = new ChallengeDef(
                "pop_bronze",
                "Pop",
                "Pop",
                ChallengeSubject.Population,
                TrophyMetal.Bronze,
                ChallengeKind.Milestone,
                ChallengeCondition.Threshold(ChallengeMetric.Population, 1),
                cash: 50);
            var rewards = new RewardsSystem(catalog: new[] { def });
            var wallet = new FundsWallet(0);
            rewards.Ledger.EnqueuePending(def.Id);

            var events = 0;
            rewards.OnTrophyUpgraded += (_, _) => events++;

            Assert.IsTrue(rewards.TryClaim(def.Id, wallet, def).MetalRaised);
            Assert.AreEqual(1, events);

            rewards.Ledger.EnqueuePending(def.Id);
            Assert.IsFalse(rewards.TryClaim(def.Id, wallet, def).MetalRaised);
            Assert.AreEqual(1, events);
        }

        static TowerGrid GridWithTwoTrophyRooms()
        {
            var grid = new TowerGrid();
            var lobby = ScriptableObject.CreateInstance<RoomTypeSO>();
            lobby.id = "lobby";
            lobby.isLobby = true;
            lobby.allowAboveGround = true;
            lobby.size = Vector2Int.one;
            Assert.IsTrue(grid.TryPlaceLobby(lobby, 0, 12, TowerGrid.LobbyFloor, out _));

            var trophy = Resources.Load<RoomTypeSO>("Rooms/TrophyRoom");
            Assert.IsNotNull(trophy);
            Assert.IsTrue(grid.TryPlace(trophy, new Vector2Int(0, 1), out _));
            Assert.IsTrue(grid.TryPlace(trophy, new Vector2Int(7, 1), out _));
            return grid;
        }
    }
}
