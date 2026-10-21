using System.Collections.Generic;
using System.Linq;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class HealthTickTests
    {
        [Test]
        public void Illness_40_for_360_minutes_marks_the_hotel_not_the_office_on_that_floor()
        {
            var health = new HealthSystem();
            var office = Room(1, OfficeType(), new Vector2Int(0, 3));
            var hotel = Room(2, HotelType(), new Vector2Int(10, 3));
            var rooms = new List<RoomInstance> { office, hotel };
            health.SetIllness(3, HealthSystem.IllnessMarkThreshold);

            Assert.IsFalse(health.TryMarkSickRoom(3, 359f, rooms));
            Assert.IsFalse(hotel.Sick);

            Assert.IsTrue(health.TryMarkSickRoom(3, 1f, rooms));
            Assert.IsTrue(hotel.Sick);
            Assert.IsFalse(office.Sick);
        }

        [Test]
        public void Office_only_floor_marks_the_office()
        {
            var health = new HealthSystem();
            var office = Room(1, OfficeType(), new Vector2Int(0, 4));
            health.SetIllness(4, 55f);

            Assert.IsTrue(health.TryMarkSickRoom(4, HealthSystem.IllnessMarkMinutes, new List<RoomInstance> { office }));
            Assert.IsTrue(office.Sick);
        }

        [Test]
        public void Mark_timer_resets_below_threshold_and_returns_when_no_room_is_eligible()
        {
            var health = new HealthSystem();
            var hotel = Room(1, HotelType(), new Vector2Int(0, 2));
            var rooms = new List<RoomInstance> { hotel };

            health.SetIllness(2, 45f);
            Assert.IsFalse(health.AdvanceSickMarkTimer(2, 300f));
            health.SetIllness(2, 10f);
            Assert.IsFalse(health.AdvanceSickMarkTimer(2, 100f));
            health.SetIllness(2, 45f);
            Assert.IsFalse(health.AdvanceSickMarkTimer(2, 300f), "Timer restarted from zero after dipping below 40.");

            hotel.SetSick(true);
            Assert.IsFalse(health.TryMarkSickRoom(2, 60f, rooms), "No eligible room.");
            hotel.SetSick(false);
            Assert.IsTrue(health.TryMarkSickRoom(2, 0.01f, rooms), "Minutes were put back when nothing could be marked.");
        }

        [Test]
        public void ChooseRoomToSicken_skips_sick_and_broken_and_ties_to_lowest_instance_id()
        {
            var health = new HealthSystem();
            var hotelB = Room(9, HotelType(), new Vector2Int(0, 1));
            var hotelA = Room(5, HotelType(), new Vector2Int(5, 1));
            var broken = Room(2, HotelType(), new Vector2Int(10, 1));
            broken.Condition = 0;
            var sick = Room(3, HotelType(), new Vector2Int(15, 1));
            sick.SetSick(true);
            var otherFloor = Room(1, HotelType(), new Vector2Int(0, 2));
            var rooms = new List<RoomInstance> { hotelB, hotelA, broken, sick, otherFloor };

            Assert.AreSame(hotelA, health.ChooseRoomToSicken(rooms, 1));
            Assert.IsNull(health.ChooseRoomToSicken(new List<RoomInstance> { broken, sick }, 1));
        }

        [Test]
        public void Health_number_helpers_match_the_brief()
        {
            Assert.AreEqual(0f, HealthSystem.GuestStressGain(0f));
            Assert.AreEqual(6f, HealthSystem.GuestStressGain(50f), 0.0001f);
            Assert.AreEqual(12f, HealthSystem.GuestStressGain(100f), 0.0001f);
            Assert.AreEqual(0f, HealthSystem.SickShare(3, 0));
            Assert.AreEqual(0.25f, HealthSystem.SickShare(1, 4), 0.0001f);

            var sickOffice = Room(2, OfficeType(), new Vector2Int(0, 0));
            sickOffice.SetSick(true);
            var rooms = new List<RoomInstance>
            {
                Room(1, HotelType(), new Vector2Int(0, 0)),
                sickOffice,
                Room(3, CondoType(), new Vector2Int(0, 0)),
                Room(4, ClinicType(), new Vector2Int(0, 0)),
            };
            Assert.AreEqual(3, HealthSystem.CountTenants(rooms, out var sick));
            Assert.AreEqual(1, sick);

            var health = new HealthSystem();
            health.SetIllness(1, 10f);
            health.ApplyDoctorCure(1);
            Assert.AreEqual(0f, health.GetIllness(1), 0.0001f);
        }

        [Test]
        public void Clinic_staff_spawn_doctors_who_are_not_population()
        {
            var grid = Tower(out var hotel, out _, out var clinic);
            clinic.SetStaffedWorkers(2);
            var agents = CreateAgents(grid);
            agents.SetHealth(new HealthSystem());

            agents.SyncHomes(grid);

            Assert.AreEqual(2, agents.Agents.Count(a => a.Role == AgentRole.Doctor));
            Assert.AreEqual(0, agents.Agents.Count(a => a.Role == AgentRole.Maid));
            Assert.IsFalse(agents.Agents.Where(a => a.Role == AgentRole.Doctor).Any(AgentSystem.CountsTowardPopulation));
            Assert.IsNotNull(hotel);
        }

        [Test]
        public void ApplyDoctorCompletion_drops_floor_illness_by_18_and_clears_sick()
        {
            var grid = Tower(out var hotel, out _, out var clinic);
            clinic.SetStaffedWorkers(1);
            var health = new HealthSystem();
            health.SetIllness(hotel.Origin.y, 50f);
            hotel.SetSick(true);
            var agents = CreateAgents(grid);
            agents.SetHealth(health);
            agents.SyncHomes(grid);
            var doctor = agents.Agents.Single(a => a.Role == AgentRole.Doctor);
            doctor.ServiceTarget = hotel;

            agents.ApplyDoctorCompletion(doctor);

            Assert.AreEqual(32f, health.GetIllness(hotel.Origin.y), 0.0001f);
            Assert.IsFalse(hotel.Sick);
            Assert.IsNull(doctor.ServiceTarget);
            Assert.AreEqual(0f, doctor.TreatmentMinutesRemaining);
        }

        [Test]
        public void Quarantine_blocks_new_hotel_office_and_condo_tenancy_and_street_spawn()
        {
            var grid = Tower(out _, out var office, out _);
            var health = new HealthSystem { Quarantined = true };
            var agents = CreateAgents(grid);
            agents.SetHealth(health);

            Assert.IsFalse(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Basic, MarketClimate.Normal, new System.Random(0), out _, out _));
            Assert.IsFalse(agents.TryFindOfficeDeskForWorker(
                grid, WealthBand.Basic, MarketClimate.Normal, new System.Random(0), null, out _, out _));
            Assert.IsFalse(agents.TrySpawnStreetVisitor(grid, new GameClock(1f, 12 * 60)));
            Assert.IsNotNull(office);

            health.Quarantined = false;
            Assert.IsTrue(agents.TryFindHotelRoomForGuest(
                grid, WealthBand.Basic, MarketClimate.Normal, new System.Random(0), out var room, out _));
            Assert.IsNotNull(room);
        }

        [Test]
        public void ApplyCleanResult_Quick_roll_0_sets_hotel_sick_and_redirties_public_restroom_without_sick()
        {
            var hotel = Room(1, HotelType(), new Vector2Int(0, 1));
            var restroom = Room(2, PublicRestroomType(), new Vector2Int(5, 1));
            Assert.IsFalse(restroom.Dirty);

            CleanModeRules.ApplyCleanResult(hotel, CleanMode.Quick, 0f);
            CleanModeRules.ApplyCleanResult(restroom, CleanMode.Quick, 0f);

            Assert.IsTrue(hotel.Sick);
            Assert.IsTrue(restroom.Dirty);
            Assert.IsFalse(restroom.Sick);
        }

        [Test]
        public void ApplyCleanResult_above_chance_does_nothing()
        {
            var hotel = Room(1, HotelType(), new Vector2Int(0, 1));
            CleanModeRules.ApplyCleanResult(hotel, CleanMode.Deep, 0.5f);
            Assert.IsFalse(hotel.Sick);
            Assert.IsTrue(CleanModeRules.IllnessOccurs(CleanMode.Quick, 0.24f));
            Assert.IsFalse(CleanModeRules.IllnessOccurs(CleanMode.Quick, 0.25f));
        }

        [Test]
        public void Maids_clean_restrooms_after_hotels_and_before_conference()
        {
            var grid = Tower(out var hotel, out _, out _);
            Assert.IsTrue(grid.TryPlace(PublicRestroomType(), new Vector2Int(0, 2), out var restroom));
            hotel.MarkDirty();
            restroom.QueueCleanWork(10f);

            Assert.IsTrue(AgentSystem.NeedsMaidClean(restroom));
            Assert.Less(AgentSystem.CleanPriorityRank(hotel), AgentSystem.CleanPriorityRank(restroom));
            Assert.Less(
                AgentSystem.CleanPriorityRank(restroom),
                AgentSystem.CleanPriorityRank(Room(9, ConferenceType(), new Vector2Int(0, 5))));
        }

        static TowerGrid Tower(out RoomInstance hotel, out RoomInstance office, out RoomInstance clinic)
        {
            var grid = new TowerGrid();
            Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 40, 0, out _));
            Assert.IsTrue(grid.TryPlace(StairsType(), new Vector2Int(20, 0), out _));
            Assert.IsTrue(grid.TryPlace(HotelType(), new Vector2Int(0, 1), out hotel));
            Assert.IsTrue(grid.TryPlace(OfficeType(), new Vector2Int(10, 1), out office));
            Assert.IsTrue(grid.TryPlace(ClinicType(), new Vector2Int(14, 1), out clinic));
            return grid;
        }

        static AgentSystem CreateAgents(TowerGrid grid)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            router.Rebuild(grid);
            return new AgentSystem(router);
        }

        static RoomInstance Room(int id, RoomTypeSO type, Vector2Int origin) =>
            new(id, type, origin, Vector2Int.one);

        static RoomTypeSO LobbyType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "lobby";
            so.isLobby = true;
            so.allowAboveGround = true;
            so.size = Vector2Int.one;
            return so;
        }

        static RoomTypeSO StairsType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "stairs";
            so.isStairs = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            so.size = new Vector2Int(2, 2);
            return so;
        }

        static RoomTypeSO HotelType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "hotel_base";
            so.category = RoomCategory.Hotel;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO OfficeType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "office_unit";
            so.category = RoomCategory.Office;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO CondoType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "condo_unit";
            so.category = RoomCategory.Condo;
            so.luxuryBand = LuxuryBand.Base;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 2;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO ClinicType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_clinic";
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO PublicRestroomType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "service_public_restroom";
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }

        static RoomTypeSO ConferenceType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = ConferenceSystem.ConferenceId;
            so.category = RoomCategory.Service;
            so.size = new Vector2Int(3, 1);
            so.maxOccupants = 0;
            so.allowAboveGround = true;
            return so;
        }
    }
}
