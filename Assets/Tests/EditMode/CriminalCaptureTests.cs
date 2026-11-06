using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CriminalCaptureTests
    {
        [Test]
        public void TryCapture_removes_criminal_on_same_floor_and_drops_crime()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(2, 50f);
            var agents = new List<Agent>
            {
                new Agent(1, AgentRole.Security, null, new Vector2Int(5, 2))
                {
                    Phase = AgentPhase.Working,
                    Visible = true
                },
                new Agent(2, AgentRole.Criminal, null, new Vector2Int(8, 2))
                {
                    Phase = AgentPhase.Moving,
                    Visible = true
                }
            };

            var captures = CrimeCapture.TryCapture(agents, crime, out var message);

            Assert.AreEqual(1, captures);
            Assert.AreEqual(1, agents.Count);
            Assert.AreEqual(AgentRole.Security, agents[0].Role);
            Assert.AreEqual(50f - CrimeSystem.CaptureCrimeDrop, crime.GetCrime(2), 0.001f);
            StringAssert.Contains("floor 2", message);
        }

        [Test]
        public void TryCapture_one_criminal_per_security_per_tick()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(3, 40f);
            var agents = new List<Agent>
            {
                new Agent(1, AgentRole.Security, null, new Vector2Int(1, 3))
                {
                    Phase = AgentPhase.Working
                },
                new Agent(2, AgentRole.Criminal, null, new Vector2Int(2, 3))
                {
                    Phase = AgentPhase.AtHome
                },
                new Agent(3, AgentRole.Criminal, null, new Vector2Int(4, 3))
                {
                    Phase = AgentPhase.AtHome
                }
            };

            var captures = CrimeCapture.TryCapture(agents, crime, out _);

            Assert.AreEqual(1, captures);
            Assert.AreEqual(2, agents.Count);
            Assert.AreEqual(1, agents.Count(a => a.Role == AgentRole.Criminal));
        }

        [Test]
        public void AgentSystem_Tick_one_security_captures_at_most_one_criminal()
        {
            var grid = LobbyOnlyGrid();
            var agents = CreateAgents(grid);
            // AtHome avoids Security UpdateServiceWork (null HomeRoom) during Tick.
            Inject(agents, new Agent(1, AgentRole.Security, null, new Vector2Int(1, 2))
            {
                Phase = AgentPhase.AtHome,
                Visible = true,
                ServiceWorkRemaining = 99f
            });
            Inject(agents, new Agent(2, AgentRole.Criminal, null, new Vector2Int(3, 2))
            {
                Phase = AgentPhase.Working,
                Visible = true,
                CriminalDwellRemaining = AgentSystem.CriminalLifeMinutes,
                VisitDwellRemaining = 99f
            });
            Inject(agents, new Agent(3, AgentRole.Criminal, null, new Vector2Int(5, 2))
            {
                Phase = AgentPhase.Working,
                Visible = true,
                CriminalDwellRemaining = AgentSystem.CriminalLifeMinutes,
                VisitDwellRemaining = 99f
            });

            var crime = new CrimeSystem();
            crime.SetCrime(2, 40f);
            var clock = new GameClock(1f, 12 * 60);

            agents.Tick(1f, clock, grid, crime: crime);

            Assert.AreEqual(
                1,
                agents.Agents.Count(a => a.Role == AgentRole.Criminal),
                "One Security must capture only one Criminal per Tick.");
            Assert.AreEqual(1, agents.Agents.Count(a => a.Role == AgentRole.Security));
            StringAssert.Contains("floor 2", agents.LastCaptureMessage);
        }

        [Test]
        public void TryCapture_ignores_outside_security_or_criminal()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(1, 30f);
            var agents = new List<Agent>
            {
                new Agent(1, AgentRole.Security, null, new Vector2Int(1, 1))
                {
                    Phase = AgentPhase.Outside
                },
                new Agent(2, AgentRole.Criminal, null, new Vector2Int(2, 1))
                {
                    Phase = AgentPhase.Moving
                },
                new Agent(3, AgentRole.Security, null, new Vector2Int(3, 2))
                {
                    Phase = AgentPhase.Working
                },
                new Agent(4, AgentRole.Criminal, null, new Vector2Int(4, 2))
                {
                    Phase = AgentPhase.Outside
                }
            };

            var captures = CrimeCapture.TryCapture(agents, crime, out var message);

            Assert.AreEqual(0, captures);
            Assert.AreEqual(4, agents.Count);
            Assert.IsTrue(string.IsNullOrEmpty(message));
            Assert.AreEqual(30f, crime.GetCrime(1), 0.001f);
        }

        [Test]
        public void AgentSystem_capture_sets_LastCaptureMessage()
        {
            var agents = CreateAgents();
            Inject(agents, new Agent(1, AgentRole.Security, null, new Vector2Int(5, 4))
            {
                Phase = AgentPhase.Working
            });
            Inject(agents, new Agent(2, AgentRole.Criminal, null, new Vector2Int(7, 4))
            {
                Phase = AgentPhase.AtHome,
                CriminalDwellRemaining = AgentSystem.CriminalLifeMinutes
            });

            var crime = new CrimeSystem();
            crime.SetCrime(4, 40f);
            var before = crime.GetCrime(4);

            var captures = agents.CaptureCriminalsNow(crime);

            Assert.AreEqual(1, captures);
            Assert.AreEqual(0, agents.Agents.Count(a => a.Role == AgentRole.Criminal));
            Assert.Less(crime.GetCrime(4), before);
            StringAssert.Contains("floor 4", agents.LastCaptureMessage);
        }

        [Test]
        public void Criminal_is_excluded_from_population()
        {
            var agents = CreateAgents();
            Inject(agents, new Agent(1, AgentRole.Criminal, null, Vector2Int.zero)
            {
                Phase = AgentPhase.AtHome
            });
            Assert.AreEqual(1, agents.Agents.Count(a => a.Role == AgentRole.Criminal));
            Assert.AreEqual(0, agents.Population);
        }

        [Test]
        public void TrySpawnCriminal_respects_concurrent_cap()
        {
            var agents = CreateAgents();
            for (var i = 0; i < AgentSystem.MaxConcurrentCriminals; i++)
            {
                Inject(agents, new Agent(100 + i, AgentRole.Criminal, null, Vector2Int.zero)
                {
                    Phase = AgentPhase.AtHome,
                    CriminalDwellRemaining = AgentSystem.CriminalLifeMinutes
                });
            }

            Assert.IsFalse(agents.TrySpawnCriminal(null, ForceCrime(50f)));
            Assert.AreEqual(
                AgentSystem.MaxConcurrentCriminals,
                agents.Agents.Count(a => a.Role == AgentRole.Criminal));
        }

        [Test]
        public void TrySpawnCriminal_enters_via_lobby_when_grid_has_roam_target()
        {
            var grid = CriminalRoamGrid();
            Assert.IsTrue(grid.HasLobby, "grid must have lobby");
            var roomSummary = string.Join(
                ", ",
                grid.Rooms.Select(r =>
                    r is null || r.Type is null
                        ? "null-type"
                        : $"{r.Type.id}:{r.Type.category}@{r.Origin.y}"));
            Assert.GreaterOrEqual(
                grid.Rooms.Count(r =>
                    r is not null &&
                    r.Type is not null &&
                    (r.Type.category == RoomCategory.Hotel || r.Type.id == "hotel")),
                1,
                "grid must expose a hotel roam target; rooms=[" + roomSummary + "]");
            var agents = CreateAgents(grid);
            var crime = ForceCrime(50f);
            crime.SetCrime(1, 50f);
            Assert.GreaterOrEqual(crime.AverageCrime, AgentSystem.CriminalSpawnMinAvg);

            var spawned = agents.TrySpawnCriminal(grid, crime);
            Assert.IsTrue(
                spawned,
                $"TrySpawnCriminal failed. criminals={agents.Agents.Count(a => a.Role == AgentRole.Criminal)} avg={crime.AverageCrime} rooms={grid.Rooms.Count}");

            var criminal = agents.Agents.Single(a => a.Role == AgentRole.Criminal);
            Assert.Greater(criminal.CriminalDwellRemaining, 0f);
            Assert.AreNotEqual(
                AgentPhase.Outside,
                criminal.Phase,
                "Spawned Criminal should leave Outside via lobby trip into the tower.");
            Assert.IsTrue(
                criminal.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator
                    or AgentPhase.Riding or AgentPhase.Working);
        }

        [Test]
        public void Criminal_Outside_with_remaining_life_is_despawned()
        {
            var grid = LobbyOnlyGrid();
            var agents = CreateAgents(grid);
            Inject(agents, new Agent(1, AgentRole.Criminal, null, new Vector2Int(0, 0))
            {
                Phase = AgentPhase.Outside,
                Visible = false,
                CriminalDwellRemaining = AgentSystem.CriminalLifeMinutes
            });

            var clock = new GameClock(1f, 12 * 60);
            agents.Tick(1f, clock, grid, crime: ForceCrime(50f));

            Assert.AreEqual(
                0,
                agents.Agents.Count(a => a.Role == AgentRole.Criminal),
                "Outside + life > 0 must not permanently consume the concurrent cap.");
        }

        [Test]
        public void Criminal_life_timeout_starts_leave_and_zeros_life()
        {
            var grid = CriminalRoamGrid();
            var agents = CreateAgents(grid);
            var criminal = new Agent(1, AgentRole.Criminal, null, new Vector2Int(3, 1))
            {
                Phase = AgentPhase.Working,
                Visible = true,
                CriminalDwellRemaining = 0.5f,
                VisitDwellRemaining = 99f
            };
            Inject(agents, criminal);

            var clock = new GameClock(1f, 12 * 60);
            agents.Tick(1f, clock, grid, crime: ForceCrime(50f));

            Assert.AreEqual(0f, criminal.CriminalDwellRemaining, 0.001f);
            if (agents.Agents.Any(a => a.Id == criminal.Id))
            {
                Assert.IsTrue(
                    criminal.Phase == AgentPhase.Outside ||
                    criminal.PhaseAfterMove == AgentPhase.Outside ||
                    criminal.Phase is AgentPhase.Moving or AgentPhase.WaitingAtElevator
                        or AgentPhase.Riding,
                    "Life timeout should leave via lobby (or already despawn if Outside).");
            }
            else
            {
                Assert.AreEqual(
                    0,
                    agents.Agents.Count(a => a.Role == AgentRole.Criminal),
                    "Despawned after reaching Outside on life timeout.");
            }
        }

        [Test]
        public void CollectFloorsForRole_lists_criminal_floors()
        {
            var agents = CreateAgents();
            Inject(agents, new Agent(1, AgentRole.Criminal, null, new Vector2Int(2, 6))
            {
                Phase = AgentPhase.AtHome
            });

            var floors = new List<int>();
            agents.CollectFloorsForRole(AgentRole.Criminal, floors);

            Assert.AreEqual(1, floors.Count);
            Assert.AreEqual(6, floors[0]);
        }

        static CrimeSystem ForceCrime(float averageFloorCrime)
        {
            var crime = new CrimeSystem();
            crime.SetCrime(0, averageFloorCrime);
            return crime;
        }

        static TowerGrid LobbyOnlyGrid()
        {
            // Prefer real Unity placement; fall back for net8 host (no ScriptableObject ECall).
            if (CanCreateScriptableObjects())
            {
                var grid = new TowerGrid();
                Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 24, 0, out _));
                return grid;
            }

            return BuildGridWithoutUnity(withHotel: false);
        }

        static TowerGrid CriminalRoamGrid()
        {
            if (CanCreateScriptableObjects())
            {
                var grid = new TowerGrid();
                Assert.IsTrue(grid.TryPlaceLobby(LobbyType(), 0, 24, 0, out _));
                Assert.IsTrue(grid.TryPlace(StairsType(), new Vector2Int(12, 0), out _));
                Assert.IsTrue(grid.TryPlace(HotelType(), new Vector2Int(3, 1), out _));
                return grid;
            }

            return BuildGridWithoutUnity(withHotel: true);
        }

        static bool CanCreateScriptableObjects()
        {
            try
            {
                // TowerGrid ctor calls ScriptableObject.CreateInstance for scaffolding.
                _ = new TowerGrid();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Builds a lobby (+ optional hotel) grid without ScriptableObject.CreateInstance,
        /// so net8 reflection hosts can exercise TrySpawnCriminal / Tick paths.
        /// </summary>
        static TowerGrid BuildGridWithoutUnity(bool withHotel)
        {
            var grid = (TowerGrid)FormatterServices.GetUninitializedObject(typeof(TowerGrid));
            SetField(grid, "_cells", new Dictionary<Vector2Int, RoomInstance>());
            SetField(grid, "_rooms", new List<RoomInstance>());
            SetField(grid, "_underStairs", new Dictionary<Vector2Int, RoomInstance>());
            SetField(grid, "_underElevator", new Dictionary<Vector2Int, RoomInstance>());
            SetField(grid, "_nextId", 1);
            SetField(grid, "_scaffoldingType", MakeRoomTypeRaw("scaffolding", isScaffolding: true));

            var rooms = (List<RoomInstance>)GetField(grid, "_rooms");
            var cells = (Dictionary<Vector2Int, RoomInstance>)GetField(grid, "_cells");

            var lobbyType = MakeRoomTypeRaw("lobby", isLobby: true);
            var lobby = new RoomInstance(1, lobbyType, new Vector2Int(0, 0), new Vector2Int(25, 1));
            rooms.Add(lobby);
            foreach (var cell in lobby.OccupiedCells())
                cells[cell] = lobby;
            SetProp(grid, "HasLobby", true);
            SetProp(grid, "MinX", 0);
            SetProp(grid, "MaxX", 24);
            SetField(grid, "_nextId", 2);

            if (withHotel)
            {
                var hotelType = MakeRoomTypeRaw(
                    "hotel",
                    category: RoomCategory.Hotel,
                    size: new Vector2Int(9, 1));
                var hotel = new RoomInstance(2, hotelType, new Vector2Int(3, 1), new Vector2Int(9, 1));
                rooms.Add(hotel);
                foreach (var cell in hotel.OccupiedCells())
                    cells[cell] = hotel;
                SetField(grid, "_nextId", 3);
            }

            Assert.IsTrue(grid.HasLobby);
            return grid;
        }

        static RoomTypeSO MakeRoomTypeRaw(
            string id,
            bool isLobby = false,
            bool isScaffolding = false,
            bool isStairs = false,
            RoomCategory category = RoomCategory.Structure,
            Vector2Int? size = null)
        {
            // Always uninitialized outside a living Unity player — CreateInstance can return
            // objects whose fields do not stick under the net8 host.
            var so = (RoomTypeSO)FormatterServices.GetUninitializedObject(typeof(RoomTypeSO));
            so.id = id;
            so.isLobby = isLobby;
            so.isScaffolding = isScaffolding;
            so.isStairs = isStairs;
            so.category = category;
            so.size = size ?? Vector2Int.one;
            so.allowAboveGround = true;
            so.allowBasement = isStairs || isScaffolding;
            return so;
        }

        static RoomTypeSO MakeRoomType(
            string id,
            bool isLobby = false,
            bool isScaffolding = false,
            bool isStairs = false,
            RoomCategory category = RoomCategory.Structure,
            Vector2Int? size = null)
        {
            if (!CanCreateScriptableObjects())
                return MakeRoomTypeRaw(id, isLobby, isScaffolding, isStairs, category, size);

            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = id;
            so.isLobby = isLobby;
            so.isScaffolding = isScaffolding;
            so.isStairs = isStairs;
            so.category = category;
            so.size = size ?? Vector2Int.one;
            so.allowAboveGround = true;
            so.allowBasement = isStairs || isScaffolding;
            return so;
        }

        static AgentSystem CreateAgents(TowerGrid grid = null)
        {
            var router = new TransitRouter(new StairsPathfinder(), new ElevatorSystem());
            if (grid != null)
                router.Rebuild(grid);
            return new AgentSystem(router);
        }

        static void Inject(AgentSystem system, Agent agent)
        {
            var field = typeof(AgentSystem).GetField(
                "_agents",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var list = (List<Agent>)field.GetValue(system);
            list.Add(agent);
        }

        static RoomTypeSO LobbyType() =>
            MakeRoomType("lobby", isLobby: true);

        static RoomTypeSO StairsType()
        {
            var so = MakeRoomType("stairs", isStairs: true, size: new Vector2Int(2, 2));
            so.allowBasement = true;
            return so;
        }

        static RoomTypeSO HotelType() =>
            MakeRoomType("hotel", category: RoomCategory.Hotel, size: new Vector2Int(9, 1));

        static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        static object GetField(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return field.GetValue(target);
        }

        static void SetProp(object target, string name, object value)
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(prop, name);
            prop.SetValue(target, value);
        }
    }
}
