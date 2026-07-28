using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BuildATower.Tests
{
    public sealed class TowerSandboxBuildSmokeTests
    {
        [UnityTest]
        public IEnumerator Lobby_office_stairs_agents_and_bulldoze()
        {
            SceneManager.LoadScene("TowerSandbox", LoadSceneMode.Single);
            yield return null;

            var build = Object.FindAnyObjectByType<BuildController>();
            Assert.That(build, Is.Not.Null, "TowerSandbox must contain a BuildController.");
            var hud = Object.FindAnyObjectByType<TowerHudController>();
            Assert.That(hud, Is.Not.Null, "TowerSandbox must contain a TowerHudController.");
            var sim = Object.FindAnyObjectByType<TowerSimulation>();
            Assert.That(sim, Is.Not.Null, "TowerSimulation should auto-attach.");

            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null, "TowerSandbox must contain a UIDocument.");
            var toolbar = document.rootVisualElement.Q<VisualElement>("toolbar");
            var initialButtonCount = toolbar.Query<Button>().ToList().Count;
            hud.enabled = false;
            hud.enabled = true;
            Assert.That(toolbar.Query<Button>().ToList().Count, Is.EqualTo(initialButtonCount));

            var office = Resources.FindObjectsOfTypeAll<RoomTypeSO>()
                .First(room => room.id == "office");
            var stairs = Resources.FindObjectsOfTypeAll<RoomTypeSO>()
                .First(room => room.id == "stairs");
            var startingFunds = build.Wallet.Balance;

            Assert.That(build.TryPlaceLobby(0, 20), Is.True);
            var afterLobby = build.Wallet.Balance;
            Assert.That(afterLobby, Is.LessThan(startingFunds));

            build.SetRoomType(office);
            Assert.That(build.TryPlaceSelected(new Vector2Int(0, 1)), Is.True);

            build.SetRoomType(stairs);
            Assert.That(build.TryPlaceSelected(new Vector2Int(0, 0)), Is.True);
            Assert.That(build.Grid.TryGetRoomAt(new Vector2Int(0, 0), out var at), Is.True);
            Assert.That(at.Type.isStairs, Is.True);

            yield return null;
            Assert.That(sim.Agents.Agents.Count, Is.GreaterThan(0));
            Assert.That(sim.Clock, Is.Not.Null);
            Assert.That(sim.Pathfinder.TryFindPath(new Vector2Int(5, 0), new Vector2Int(0, 1), out _), Is.True);

            var afterBuild = build.Wallet.Balance;
            Assert.That(build.TryDemolishAt(new Vector2Int(0, 1)), Is.True);
            Assert.That(build.Wallet.Balance, Is.EqualTo(afterBuild));
        }
    }
}
