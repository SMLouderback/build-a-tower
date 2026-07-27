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
        public IEnumerator Lobby_office_and_bulldoze_update_scene_state_and_funds()
        {
            SceneManager.LoadScene("TowerSandbox", LoadSceneMode.Single);
            yield return null;

            var build = Object.FindAnyObjectByType<BuildController>();
            Assert.That(build, Is.Not.Null, "TowerSandbox must contain a BuildController.");
            var hud = Object.FindAnyObjectByType<TowerHudController>();
            Assert.That(hud, Is.Not.Null, "TowerSandbox must contain a TowerHudController.");

            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null, "TowerSandbox must contain a UIDocument.");
            var toolbar = document.rootVisualElement.Q<VisualElement>("toolbar");
            var initialButtonCount = toolbar.Query<Button>().ToList().Count;
            hud.enabled = false;
            hud.enabled = true;
            Assert.That(toolbar.Query<Button>().ToList().Count, Is.EqualTo(initialButtonCount));

            var office = Resources.FindObjectsOfTypeAll<RoomTypeSO>()
                .Single(room => room.id == "office");
            var startingFunds = build.Wallet.Balance;

            Assert.That(build.TryPlaceLobby(0, 10), Is.True);
            var afterLobby = build.Wallet.Balance;
            Assert.That(afterLobby, Is.LessThan(startingFunds));

            build.SetRoomType(office);
            var officeCell = new Vector2Int(0, 2);
            Assert.That(build.TryPlaceSelected(officeCell), Is.True);
            Assert.That(build.Wallet.Balance, Is.EqualTo(afterLobby - office.buildCost));
            Assert.That(build.Grid.TryGetRoomAt(officeCell, out _), Is.True);

            var afterOffice = build.Wallet.Balance;
            Assert.That(build.TryDemolishAt(officeCell), Is.True);
            Assert.That(build.Wallet.Balance, Is.EqualTo(afterOffice));
            Assert.That(build.Grid.TryGetRoomAt(officeCell, out _), Is.False);
        }
    }
}
