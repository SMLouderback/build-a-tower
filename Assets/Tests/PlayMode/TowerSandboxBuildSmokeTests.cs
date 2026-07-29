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

            var beforeMidnight = build.Wallet.Balance;
            sim.Economy.OnNewDay(build.Grid, sim.Agents.Agents, build.Wallet);
            Assert.That(sim.Economy.LastIncome, Is.GreaterThan(0), "Occupied office should pay rent at midnight.");
            Assert.That(build.Wallet.Balance, Is.GreaterThan(beforeMidnight));

            var afterBuild = build.Wallet.Balance;
            Assert.That(build.TryDemolishAt(new Vector2Int(0, 1)), Is.True);
            Assert.That(build.Wallet.Balance, Is.EqualTo(afterBuild));
        }

        [UnityTest]
        public IEnumerator Elevator_place_extend_and_route_beyond_stairs()
        {
            SceneManager.LoadScene("TowerSandbox", LoadSceneMode.Single);
            yield return null;

            var build = Object.FindAnyObjectByType<BuildController>();
            Assert.That(build, Is.Not.Null, "TowerSandbox must contain a BuildController.");
            var sim = Object.FindAnyObjectByType<TowerSimulation>();
            Assert.That(sim, Is.Not.Null, "TowerSimulation should auto-attach.");

            var elevator = Resources.FindObjectsOfTypeAll<RoomTypeSO>()
                .First(room => room.id == "elevator_normal");

            Assert.That(build.TryPlaceLobby(0, 20), Is.True);

            Assert.That(
                sim.Router.TryPlanTrip(new Vector2Int(5, 0), new Vector2Int(5, 4), out _),
                Is.False,
                "A 4-floor trip should not route before any shaft exists.");

            build.SetRoomType(elevator);
            Assert.That(build.TryPlaceSelected(new Vector2Int(5, 0)), Is.True);
            Assert.That(build.Grid.TryGetRoomAt(new Vector2Int(5, 0), out var shaftCell), Is.True);
            Assert.That(shaftCell.Type.isElevatorShaft, Is.True);

            Assert.That(build.TryExtendElevator(shaftCell, 0, 4), Is.True);
            Assert.That(build.Grid.TryGetRoomAt(new Vector2Int(5, 4), out var top), Is.True);
            Assert.That(top.Type.isElevatorShaft, Is.True);

            yield return null;
            Assert.That(
                sim.Router.TryPlanTrip(new Vector2Int(5, 0), new Vector2Int(5, 4), out var legs),
                Is.True,
                "The extended shaft should let a 4-floor trip route.");
            Assert.That(
                legs.Any(leg => leg.Kind == TransitLegKind.Elevator),
                Is.True,
                "A trip taller than the stairs span must include an elevator leg.");
        }
    }
}
