using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class MetroTunnelViewTests
    {
        [TearDown]
        public void TearDown()
        {
            var found = Object.FindObjectsByType<MetroTunnelView>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    Object.DestroyImmediate(found[i].gameObject);
            }
        }

        [Test]
        public void TunnelFloorY_uses_lowest_station_origin()
        {
            var stations = new List<RectInt>
            {
                new RectInt(0, -3, 8, 3),
                new RectInt(40, -8, 8, 3),
                new RectInt(80, -5, 8, 3),
            };

            Assert.IsTrue(MetroTunnelView.TryTunnelFloorY(stations, out var y));
            Assert.AreEqual(-8, y);
        }

        [Test]
        public void TunnelFloorY_false_when_no_stations()
        {
            Assert.IsFalse(MetroTunnelView.TryTunnelFloorY(null, out _));
            Assert.IsFalse(MetroTunnelView.TryTunnelFloorY(new List<RectInt>(), out _));
        }

        [Test]
        public void WorldSpan_covers_dirt_world_and_expands_to_wider_grid()
        {
            MetroTunnelView.WorldSpan(0, 20, out var minX, out var maxX);
            Assert.AreEqual(DirtBand.MinX, minX);
            Assert.AreEqual(DirtBand.MaxX, maxX);
            Assert.Greater(maxX - minX + 1, 20);

            MetroTunnelView.WorldSpan(-120, 140, out minX, out maxX);
            Assert.AreEqual(-120, minX);
            Assert.AreEqual(140, maxX);
        }

        [Test]
        public void TrainOffsetX_loops_from_just_off_the_left()
        {
            const float span = 20f;
            const float speed = 4f;
            const float car = 5f;
            Assert.AreEqual(-car, MetroTunnelView.TrainOffsetX(0f, span, speed, car), 0.001f);
            Assert.AreEqual(3f, MetroTunnelView.TrainOffsetX(2f, span, speed, car), 0.001f);
            var period = (span + car) / speed;
            Assert.AreEqual(-car, MetroTunnelView.TrainOffsetX(period, span, speed, car), 0.001f);
        }

        [Test]
        public void Sorting_keeps_tunnel_and_train_behind_dollhouse_rooms()
        {
            Assert.Less(MetroTunnelView.TunnelSortingOrder, RoomDollhouseArt.SortingOrder);
            Assert.Less(MetroTunnelView.TunnelSortingOrder, MetroTunnelView.TrainSortingOrder);
            Assert.LessOrEqual(MetroTunnelView.TrainSortingOrder, RoomDollhouseArt.SortingOrder);
        }

        [Test]
        public void Apply_hides_without_tunnel_and_places_world_strip()
        {
            var go = new GameObject("MetroTunnelViewTest");
            var view = go.AddComponent<MetroTunnelView>();

            view.Apply(hasTunnel: false, floorY: -4, minX: -80, maxX: 100, elapsedSeconds: 0f);
            Assert.IsFalse(view.TunnelVisible);

            view.Apply(hasTunnel: true, floorY: -6, minX: -80, maxX: 100, elapsedSeconds: 0f);
            Assert.IsTrue(view.TunnelVisible);
            Assert.IsTrue(view.TrainVisible);
            Assert.AreEqual(-6f + 0.5f, view.TunnelCenterY, 0.001f);
            Assert.AreEqual(181f, view.TunnelWidthTiles, 0.001f);
            Assert.AreEqual((-80f + 101f) * 0.5f, view.TunnelCenterX, 0.001f);

            var left = MetroTunnelView.TrainOffsetX(
                0f,
                181f,
                MetroTunnelView.TrainTilesPerSecond,
                MetroTunnelView.TrainCarTiles);
            Assert.AreEqual(-80f + left + MetroTunnelView.TrainCarTiles * 0.5f, view.TrainCenterX, 0.02f);
        }

        [Test]
        public void EnsureInScene_creates_a_single_view()
        {
            MetroTunnelView.EnsureInScene();
            MetroTunnelView.EnsureInScene();
            var found = Object.FindObjectsByType<MetroTunnelView>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length);
        }

        [Test]
        public void Metro_dollhouse_maps_and_bytes_exist()
        {
            Assert.AreEqual("metro_station_8x3", RoomDollhouseArt.ResourceLeaf("metro_station"));
            Assert.AreEqual(
                "Art/Dollhouse/metro_station_8x3",
                RoomDollhouseArt.ResourcePath("metro_station"));

            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "metro_station";
            Assert.IsTrue(RoomDollhouseArt.IsMapped(so));

            var dir = Path.Combine(Application.dataPath, "Resources/Art/Dollhouse");
            var bytes = Path.Combine(dir, "metro_station_8x3.bytes");
            var png = Path.Combine(dir, "metro_station_8x3.png");
            Assert.IsTrue(File.Exists(bytes), bytes);
            Assert.IsTrue(File.Exists(png), png);
            Assert.Greater(new FileInfo(bytes).Length, 1024);
        }

        [Test]
        public void Metro_menu_icon_and_tunnel_strip_resolve()
        {
            var room = ScriptableObject.CreateInstance<RoomTypeSO>();
            room.id = "metro_station";
            Assert.AreEqual("transit_metro", TowerHudController.MenuIconIdForRoom(room));

            var office = ScriptableObject.CreateInstance<RoomTypeSO>();
            office.id = "office_micro";
            Assert.AreEqual("office_micro", TowerHudController.MenuIconIdForRoom(office));

            Assert.IsTrue(MenuIconArt.TryGetTexture("transit_metro", out var icon), "transit_metro");
            Assert.IsNotNull(icon);
            Assert.AreEqual(MenuIconArt.OutputPixels, icon.width);
            Assert.AreEqual(MenuIconArt.OutputPixels, icon.height);

            var tunnel = Path.Combine(Application.dataPath, "Resources/Art/World/metro_tunnel.bytes");
            Assert.IsTrue(File.Exists(tunnel), tunnel);
            Assert.Greater(new FileInfo(tunnel).Length, 1024);

            var loaded = Resources.Load<TextAsset>("Art/World/metro_tunnel");
            Assert.IsNotNull(loaded);
            Assert.Greater(loaded.bytes.Length, 32);
        }
    }
}
