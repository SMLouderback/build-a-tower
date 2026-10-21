using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HealthRoomCatalogTests
    {
        [Test]
        public void ServicePublicRestroom_resource_matches_catalog()
        {
            AssertHealthUtilityRoom(
                "Rooms/ServicePublicRestroom",
                "service_public_restroom",
                "Public Restroom",
                stars: 0,
                size: new Vector2Int(3, 1),
                buildCost: 18000,
                noiseOutput: 0.10f,
                allowAboveGround: true,
                allowBasement: false,
                requiresHousekeeping: true);
        }

        [Test]
        public void ServiceOfficeRestroom_resource_matches_catalog()
        {
            AssertHealthUtilityRoom(
                "Rooms/ServiceOfficeRestroom",
                "service_office_restroom",
                "Office Restroom",
                stars: 0,
                size: new Vector2Int(3, 1),
                buildCost: 22000,
                noiseOutput: 0.10f,
                allowAboveGround: true,
                allowBasement: false,
                requiresHousekeeping: true);
        }

        [Test]
        public void ServiceTrash_resource_matches_catalog()
        {
            AssertHealthUtilityRoom(
                "Rooms/ServiceTrash",
                "service_trash",
                "Trash Collection",
                stars: 1,
                size: new Vector2Int(6, 1),
                buildCost: 48000,
                noiseOutput: 0.40f,
                allowAboveGround: true,
                allowBasement: true,
                requiresHousekeeping: false);
        }

        [Test]
        public void ServiceClinic_resource_matches_catalog()
        {
            AssertHealthUtilityRoom(
                "Rooms/ServiceClinic",
                "service_clinic",
                "Clinic",
                stars: 2,
                size: new Vector2Int(6, 1),
                buildCost: 85000,
                noiseOutput: 0.15f,
                allowAboveGround: true,
                allowBasement: true,
                requiresHousekeeping: true);
        }

        [Test]
        public void Midnight_debits_clinic_doctor_wages()
        {
            var grid = new TowerGrid();
            grid.TryPlaceLobby(Lobby(), 0, 8, 0, out _);
            var clinic = Resources.Load<RoomTypeSO>("Rooms/ServiceClinic");
            Assert.IsNotNull(clinic);
            Assert.IsTrue(grid.TryPlace(clinic, new Vector2Int(0, 1), out var room));
            room.SetStaffedWorkers(2);
            var wallet = new FundsWallet(100_000);
            var economy = new EconomySystem();

            economy.OnNewDay(grid, new List<Agent>(), wallet);

            const int expectedWages = 2 * EconomySystem.DoctorWagePerDay;
            Assert.AreEqual(expectedWages, economy.LastWageExpense);
            Assert.AreEqual(expectedWages, economy.LastExpense);
            Assert.AreEqual(100_000 - expectedWages, wallet.Balance);
            Assert.AreEqual(expectedWages, economy.GetLastRoomExpense(room));
            Assert.AreEqual(expectedWages, room.LifetimeExpense);
        }

        [Test]
        public void ApplyAutoHireOnPlace_clinic_hires_one_public_restroom_stays_zero()
        {
            var clinic = Resources.Load<RoomTypeSO>("Rooms/ServiceClinic");
            var restroom = Resources.Load<RoomTypeSO>("Rooms/ServicePublicRestroom");
            Assert.IsNotNull(clinic);
            Assert.IsNotNull(restroom);

            var clinicRoom = new RoomInstance(1, clinic, Vector2Int.zero, clinic.size);
            var restroomRoom = new RoomInstance(2, restroom, Vector2Int.zero, restroom.size);

            BuildController.ApplyAutoHireOnPlace(clinicRoom);
            BuildController.ApplyAutoHireOnPlace(restroomRoom);

            Assert.AreEqual(1, clinicRoom.StaffedWorkers);
            Assert.AreEqual(0, restroomRoom.StaffedWorkers);
        }

        static void AssertHealthUtilityRoom(
            string resourcePath,
            string id,
            string displayName,
            int stars,
            Vector2Int size,
            int buildCost,
            float noiseOutput,
            bool allowAboveGround,
            bool allowBasement,
            bool requiresHousekeeping)
        {
            var room = Resources.Load<RoomTypeSO>(resourcePath);
            Assert.IsNotNull(room, $"{resourcePath} should load from Resources");
            Assert.AreEqual(id, room.id);
            Assert.AreEqual(displayName, room.displayName);
            Assert.AreEqual(stars, room.requiredStars);
            Assert.AreEqual(size, room.size);
            Assert.AreEqual(buildCost, room.buildCost);
            Assert.AreEqual(requiresHousekeeping, room.requiresHousekeeping);
            Assert.AreEqual(noiseOutput, room.noiseOutput, 0.001f);
            Assert.AreEqual(allowAboveGround, room.allowAboveGround);
            Assert.AreEqual(allowBasement, room.allowBasement);
            Assert.AreEqual(RoomCategory.Service, room.category);
            Assert.AreEqual(BuildFamily.Utility, room.buildFamily);
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
    }
}
