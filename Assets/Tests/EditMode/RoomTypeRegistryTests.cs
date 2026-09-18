using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public sealed class RoomTypeRegistryTests
    {
        [Test]
        public void Registry_resolves_scene_generated_and_resource_types_by_id()
        {
            var lobby = CreateType("lobby");
            var sky = CreateType("sky_lobby");
            var grid = new TowerGrid();

            var registry = RoomTypeRegistry.CreateForTests(
                new[] { lobby, sky, grid.ScaffoldingType });

            Assert.IsTrue(registry.TryResolve("lobby", out var foundLobby));
            Assert.AreSame(lobby, foundLobby);
            Assert.IsTrue(registry.TryResolve("sky_lobby", out _));
            Assert.IsTrue(registry.TryResolve("scaffolding", out _));
        }

        [Test]
        public void Registry_rejects_conflicting_duplicate_ids()
        {
            var first = CreateType("duplicate");
            var second = CreateType("duplicate");
            second.buildCost = first.buildCost + 1;

            var exception = Assert.Throws<InvalidOperationException>(
                () => RoomTypeRegistry.CreateForTests(new[] { first, second }));

            StringAssert.Contains("duplicate", exception.Message);
        }

        [Test]
        public void Registry_rejects_blank_ids()
        {
            Assert.Throws<InvalidOperationException>(
                () => RoomTypeRegistry.CreateForTests(new[] { CreateType("  ") }));
        }

        [Test]
        public void Registry_returns_false_for_blank_and_unknown_ids()
        {
            var registry = RoomTypeRegistry.CreateForTests(new[] { CreateType("known") });

            Assert.IsFalse(registry.TryResolve(null, out _));
            Assert.IsFalse(registry.TryResolve(string.Empty, out _));
            Assert.IsFalse(registry.TryResolve("  ", out _));
            Assert.IsFalse(registry.TryResolve("unknown", out _));
            Assert.IsFalse(registry.TryResolve("Known", out _), "IDs must be case-sensitive.");
        }

        [Test]
        public void Create_registers_resource_scaffolding_and_runtime_elevator_types()
        {
            var registry = RoomTypeRegistry.Create(null, new TowerGrid());

            Assert.IsTrue(registry.TryResolve("service_housekeeping", out _));
            Assert.IsTrue(registry.TryResolve("scaffolding", out _));
            Assert.IsTrue(registry.TryResolve("elevator_normal", out var normal));
            Assert.AreEqual(1, normal.requiredStars);
            Assert.IsTrue(registry.TryResolve("elevator_express", out var express));
            Assert.AreEqual("Express Elevator", express.displayName);
            Assert.AreEqual(ElevatorShaftKind.Express, express.ResolvedElevatorKind());
            Assert.AreEqual(3, express.requiredStars);
            Assert.AreEqual(12000, express.buildCost);
            Assert.IsTrue(registry.TryResolve("elevator_service", out var service));
            Assert.AreEqual("Service Elevator", service.displayName);
            Assert.AreEqual(ElevatorShaftKind.Service, service.ResolvedElevatorKind());
            Assert.AreEqual(4, service.requiredStars);
            Assert.AreEqual(9000, service.buildCost);
        }

        [Test]
        public void Create_registers_hud_catalog_types_missing_from_resources()
        {
            var office = CreateType("office_catalog_only");
            var go = new GameObject("catalog-registry");
            try
            {
                var hud = go.AddComponent<TowerHudController>();
                var build = go.AddComponent<BuildController>();
                var placeable = (List<RoomTypeSO>)typeof(TowerHudController)
                    .GetField("placeableRooms", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(hud);
                placeable.Add(office);
                typeof(BuildController)
                    .GetField("hud", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(build, hud);

                var registry = RoomTypeRegistry.Create(build, new TowerGrid());

                Assert.IsTrue(registry.TryResolve("office_catalog_only", out var found));
                Assert.AreSame(office, found);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(office);
            }
        }

        [Test]
        public void Registry_allows_repeated_references_and_equivalent_distinct_objects()
        {
            var repeated = CreateType("repeated");
            var firstEquivalent = CreateType("equivalent");
            firstEquivalent.displayName = "Equivalent Boutique";
            firstEquivalent.category = RoomCategory.Commercial;
            firstEquivalent.buildCost = 2345;
            firstEquivalent.noiseOutput = 0.25f;
            var secondEquivalent = UnityEngine.Object.Instantiate(firstEquivalent);
            secondEquivalent.displayName = "Equivalent Store";
            secondEquivalent.placeholderColor = Color.magenta;

            var registry = RoomTypeRegistry.CreateForTests(
                new[] { repeated, repeated, firstEquivalent, secondEquivalent });

            Assert.IsTrue(registry.TryResolve("repeated", out var foundRepeated));
            Assert.AreSame(repeated, foundRepeated);
            Assert.IsTrue(registry.TryResolve("equivalent", out var foundEquivalent));
            Assert.AreSame(firstEquivalent, foundEquivalent);
        }

        [Test]
        public void Registry_rejects_duplicate_ids_with_different_resolved_subgroups()
        {
            var food = CreateType("duplicate_shop");
            food.displayName = "Duplicate Restaurant";
            food.category = RoomCategory.Commercial;
            var retail = UnityEngine.Object.Instantiate(food);
            retail.displayName = "Duplicate Boutique";

            Assert.AreEqual(BuildSubgroup.Food, food.ResolvedBuildSubgroup());
            Assert.AreEqual(BuildSubgroup.Retail, retail.ResolvedBuildSubgroup());
            var exception = Assert.Throws<InvalidOperationException>(
                () => RoomTypeRegistry.CreateForTests(new[] { food, retail }));

            StringAssert.Contains("duplicate_shop", exception.Message);
        }

        static RoomTypeSO CreateType(string id)
        {
            var type = ScriptableObject.CreateInstance<RoomTypeSO>();
            type.id = id;
            return type;
        }
    }
}
