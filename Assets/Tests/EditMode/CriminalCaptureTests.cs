using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        static AgentSystem CreateAgents() =>
            new AgentSystem(new TransitRouter(new StairsPathfinder(), new ElevatorSystem()));

        static void Inject(AgentSystem system, Agent agent)
        {
            var field = typeof(AgentSystem).GetField(
                "_agents",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var list = (List<Agent>)field.GetValue(system);
            list.Add(agent);
        }
    }
}
