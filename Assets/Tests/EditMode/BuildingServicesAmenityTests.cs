using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class BuildingServicesAmenityTests
    {
        [TestCase(AgentRole.CondoResident)]
        [TestCase(AgentRole.OfficeWorker)]
        public void Mail_relieves_condo_and_office_stress_once_daily(AgentRole role)
        {
            var grid = NewGrid();
            var home = Place(grid, Home(role), new Vector2Int(0, 1));
            Place(grid, Utility(ParkingStalls.MailId, 4), new Vector2Int(8, 1));
            var agent = new Agent(1, role, home, home.Origin) { Stress = 10f };

            Assert.IsTrue(AmenitySystem.TryApplyDailyRelief(agent, grid, 3));
            Assert.AreEqual(9f, agent.Stress, 0.001f);
            Assert.IsFalse(AmenitySystem.TryApplyDailyRelief(agent, grid, 3));
            Assert.AreEqual(9f, agent.Stress, 0.001f);
        }

        [Test]
        public void Accessible_recycling_relieves_condo_stress_by_two()
        {
            var grid = NewGrid();
            var recycling = Place(grid, Utility(ParkingStalls.RecyclingId, 6), new Vector2Int(0, -1));
            var home = Place(grid, Home(AgentRole.CondoResident), new Vector2Int(8, -1));
            var agent = new Agent(1, AgentRole.CondoResident, home, home.Origin) { Stress = 10f };

            Assert.IsTrue(ParkingStalls.IsVehicleAccessible(grid, recycling));
            Assert.IsTrue(AmenitySystem.TryApplyDailyRelief(agent, grid, 1));
            Assert.AreEqual(8f, agent.Stress, 0.001f);
        }

        [Test]
        public void Inaccessible_recycling_grants_no_relief()
        {
            var grid = NewGrid();
            var upperRamp = Place(grid, Ramp(), new Vector2Int(0, -1));
            Place(grid, Ramp(), new Vector2Int(0, -2));
            var recycling = Place(grid, Utility(ParkingStalls.RecyclingId, 6), new Vector2Int(3, -2));
            var home = Place(grid, Home(AgentRole.CondoResident), new Vector2Int(9, -2));
            var agent = new Agent(1, AgentRole.CondoResident, home, home.Origin) { Stress = 10f };
            upperRamp.Condition = 0;

            Assert.IsFalse(ParkingStalls.IsVehicleAccessible(grid, recycling));
            Assert.IsFalse(AmenitySystem.TryApplyDailyRelief(agent, grid, 1));
            Assert.AreEqual(10f, agent.Stress, 0.001f);
        }

        [Test]
        public void Leisure_and_utility_relief_choose_strongest()
        {
            var grid = NewGrid();
            var home = Place(grid, Home(AgentRole.CondoResident), new Vector2Int(0, 1));
            Place(grid, Utility(ParkingStalls.MailId, 4), new Vector2Int(5, 1));
            Place(grid, Leisure("leisure_spa", 4), new Vector2Int(10, 1));
            var agent = new Agent(1, AgentRole.CondoResident, home, home.Origin) { Stress = 10f };

            Assert.IsTrue(AmenitySystem.TryApplyDailyRelief(agent, grid, 1));
            Assert.AreEqual(5f, agent.Stress, 0.001f);
        }

        [Test]
        public void Accessible_loading_dock_adds_five_percent_to_hotel_bonus()
        {
            var grid = NewGrid();
            var dock = Place(grid, Utility(ParkingStalls.LoadingDockId, 8), new Vector2Int(0, -1));
            var hotel = Place(grid, Home(AgentRole.HotelGuest), new Vector2Int(8, -1));

            Assert.IsTrue(ParkingStalls.IsVehicleAccessible(grid, dock));
            Assert.AreEqual(0.05f, AmenitySystem.LoadingDockDemandBonus(grid, hotel), 0.001f);
            Assert.AreEqual(0.05f, AmenitySystem.HotelDemandBonus(grid, hotel), 0.001f);
        }

        [Test]
        public void Loading_dock_bonus_is_additive_with_spa()
        {
            var grid = NewGrid();
            Place(grid, Utility(ParkingStalls.LoadingDockId, 8), new Vector2Int(0, -1));
            var hotel = Place(grid, Home(AgentRole.HotelGuest), new Vector2Int(8, -1));
            Place(grid, Leisure("leisure_spa", 4), new Vector2Int(12, -1));

            Assert.AreEqual(0.13f, AmenitySystem.HotelDemandBonus(grid, hotel), 0.001f);
        }

        [Test]
        public void Shop_demand_weight_gets_only_accessible_dock_nudge()
        {
            var grid = NewGrid();
            var shop = Place(grid, Shop(), new Vector2Int(8, -1));
            var baseline = ShopVisitRules.DemandWeight(shop, false);
            Place(grid, Utility(ParkingStalls.LoadingDockId, 8), new Vector2Int(0, -1));

            Assert.AreEqual(
                baseline * 1.05f,
                ShopVisitRules.DemandWeight(shop, false, grid),
                0.001f);

            var blockedGrid = NewGrid();
            var upperRamp = Place(blockedGrid, Ramp(), new Vector2Int(0, -1));
            Place(blockedGrid, Ramp(), new Vector2Int(0, -2));
            var blockedDock = Place(
                blockedGrid,
                Utility(ParkingStalls.LoadingDockId, 8),
                new Vector2Int(3, -2));
            var blockedShop = Place(blockedGrid, Shop(), new Vector2Int(11, -2));
            upperRamp.Condition = 0;

            Assert.IsFalse(ParkingStalls.IsVehicleAccessible(blockedGrid, blockedDock));
            Assert.AreEqual(
                ShopVisitRules.DemandWeight(blockedShop, false),
                ShopVisitRules.DemandWeight(blockedShop, false, blockedGrid),
                0.001f);
        }

        static TowerGrid NewGrid()
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(Lobby(), 0, 40, 0, out _));
            return grid;
        }

        static RoomInstance Place(TowerGrid grid, RoomTypeSO type, Vector2Int origin)
        {
            Assert.IsTrue(grid.TryPlace(type, origin, out var room), $"{type.id} at {origin}");
            return room;
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

        static RoomTypeSO Home(AgentRole role)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = role == AgentRole.OfficeWorker ? "office" : role == AgentRole.HotelGuest ? "hotel" : "condo";
            so.category = role == AgentRole.OfficeWorker
                ? RoomCategory.Office
                : role == AgentRole.HotelGuest ? RoomCategory.Hotel : RoomCategory.Condo;
            so.size = new Vector2Int(4, 1);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO Utility(string id, int width)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Service;
            so.buildFamily = BuildFamily.Utility;
            so.size = new Vector2Int(width, 1);
            so.allowAboveGround = id != ParkingStalls.LoadingDockId;
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO Leisure(string id, int width)
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Leisure;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.size = new Vector2Int(width, 1);
            so.allowAboveGround = true;
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO Shop()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "shop_retail";
            so.category = RoomCategory.Commercial;
            so.buildFamily = BuildFamily.Shops;
            so.incomeModel = IncomeModel.TrafficVariable;
            so.size = new Vector2Int(4, 1);
            so.allowBasement = true;
            so.maxOccupants = 4;
            return so;
        }

        static RoomTypeSO Ramp()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ParkingStalls.RampId;
            so.isParkingRamp = true;
            so.size = new Vector2Int(3, 1);
            so.allowBasement = true;
            return so;
        }
    }
}
