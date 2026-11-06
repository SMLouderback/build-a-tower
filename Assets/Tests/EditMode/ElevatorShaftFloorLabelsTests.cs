using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class ElevatorShaftFloorLabelsTests
    {
        ElevatorShaftFloorLabels _labels;
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _labels = new ElevatorShaftFloorLabels();
            _root = new GameObject("ElevatorShaftFloorLabelsTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        RoomTypeSO Lobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO SkyLobby()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "sky_lobby";
            so.isSkyLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        RoomTypeSO NormalElevator() =>
            RoomTypeSO.CreateRuntimeElevator(
                "elevator_normal", "Elevator", ElevatorShaftKind.Normal, requiredStars: 0);

        RoomTypeSO ExpressElevator() =>
            RoomTypeSO.CreateRuntimeElevator(
                "elevator_express", "Express", ElevatorShaftKind.Express, requiredStars: 3);

        static void BuildSupportBand(TowerGrid grid, int minX, int maxX, int topFloorInclusive)
        {
            for (var y = 1; y <= topFloorInclusive; y++)
            for (var x = minX; x <= maxX; x++)
                Assert.IsTrue(grid.TryPlaceScaffold(new Vector2Int(x, y), out _));
        }

        static TextMesh[] EnabledLabels(Transform parent) =>
            parent.GetComponentsInChildren<TextMesh>(true)
                .Where(tm => tm.gameObject.activeInHierarchy)
                .ToArray();

        [Test]
        public void Sync_creates_one_label_per_floor_centered()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(NormalElevator(), new Vector2Int(0, 0), out var shaftRoom));
            Assert.IsTrue(grid.TryExtendElevator(shaftRoom, 0, 4, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            Assert.AreEqual(1, elevators.Shafts.Count);
            Assert.AreEqual(0, elevators.Shafts[0].MinFloor);
            Assert.AreEqual(4, elevators.Shafts[0].MaxFloor);

            _labels.Sync(grid, elevators, _root.transform);

            var enabled = EnabledLabels(_root.transform);
            Assert.AreEqual(5, enabled.Length);

            var lobby = enabled.Single(tm => Mathf.Approximately(tm.transform.position.y, 0.5f));
            Assert.AreEqual("L", lobby.text);
            Assert.AreEqual(ElevatorShaftFloorLabels.ActiveColor, lobby.color);
            Assert.AreEqual(0.5f, lobby.transform.position.x, 0.001f);

            var mesh = lobby.GetComponent<MeshRenderer>();
            Assert.IsNotNull(mesh);
            Assert.AreEqual(ElevatorShaftFloorLabels.SortingOrder, mesh.sortingOrder);
        }

        [Test]
        public void Sync_express_two_wide_uses_one_centered_label_per_floor()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 6);

            Assert.IsTrue(grid.TryPlace(ExpressElevator(), new Vector2Int(2, 1), out var shaftRoom));
            Assert.IsTrue(grid.TryExtendElevator(shaftRoom, 1, 5, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            var shaft = elevators.Shafts.Single(s => s.Kind == ElevatorShaftKind.Express);
            Assert.AreEqual(2, shaft.Width);
            Assert.AreEqual(1, shaft.MinFloor);
            Assert.AreEqual(5, shaft.MaxFloor);

            _labels.Sync(grid, elevators, _root.transform);

            var enabled = EnabledLabels(_root.transform);
            var floorCount = shaft.MaxFloor - shaft.MinFloor + 1;
            Assert.AreEqual(floorCount, enabled.Length);

            var expectedX = shaft.X + shaft.Width * 0.5f;
            foreach (var label in enabled)
                Assert.AreEqual(expectedX, label.transform.position.x, 0.001f);
        }

        [Test]
        public void Sync_mutes_express_floors_not_served()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 10, 0, out _);
            BuildSupportBand(grid, 0, 10, 10);
            Assert.IsTrue(grid.TryPlaceSkyLobby(SkyLobby(), 0, 10, 5, out _));

            Assert.IsTrue(grid.TryPlace(ExpressElevator(), new Vector2Int(2, 0), out var shaftRoom));
            Assert.IsTrue(grid.TryExtendElevator(shaftRoom, 0, 8, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            var shaft = elevators.Shafts.Single(s => s.Kind == ElevatorShaftKind.Express);
            Assert.IsTrue(shaft.Serves(0));
            Assert.IsTrue(shaft.Serves(5));
            Assert.IsFalse(shaft.Serves(3));

            _labels.Sync(grid, elevators, _root.transform);

            var enabled = EnabledLabels(_root.transform);
            var lobby = enabled.Single(tm => Mathf.Approximately(tm.transform.position.y, 0.5f));
            var mid = enabled.Single(tm => Mathf.Approximately(tm.transform.position.y, 3.5f));
            var sky = enabled.Single(tm => Mathf.Approximately(tm.transform.position.y, 5.5f));

            Assert.AreEqual("L", lobby.text);
            Assert.AreEqual(ElevatorShaftFloorLabels.ActiveColor, lobby.color);
            Assert.AreEqual("3", mid.text);
            Assert.AreEqual(ElevatorShaftFloorLabels.MutedColor, mid.color);
            Assert.AreEqual("SL5", sky.text);
            Assert.AreEqual(ElevatorShaftFloorLabels.ActiveColor, sky.color);
        }

        [Test]
        public void Sync_disables_unused_pool_entries_and_Clear_hides_all()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _);
            Assert.IsTrue(grid.TryPlace(NormalElevator(), new Vector2Int(3, 0), out var tall));
            Assert.IsTrue(grid.TryExtendElevator(tall, 0, 4, out _));

            var elevators = new ElevatorSystem();
            elevators.SyncFromGrid(grid);
            _labels.Sync(grid, elevators, _root.transform);
            Assert.AreEqual(5, EnabledLabels(_root.transform).Length);

            Assert.IsTrue(grid.TryDemolishAt(new Vector2Int(3, 0), out _));
            Assert.IsTrue(grid.TryPlace(NormalElevator(), new Vector2Int(3, 0), out _));
            elevators.SyncFromGrid(grid);
            _labels.Sync(grid, elevators, _root.transform);

            Assert.AreEqual(2, EnabledLabels(_root.transform).Length);
            Assert.GreaterOrEqual(
                _root.GetComponentsInChildren<TextMesh>(true).Length,
                5);

            _labels.Clear();
            Assert.AreEqual(0, EnabledLabels(_root.transform).Length);
        }
    }
}
