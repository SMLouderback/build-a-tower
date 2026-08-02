using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CrimeStressTests
    {
        [Test]
        public void High_floor_crime_adds_daily_stress_to_worker()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(2, 100f);
            var agent = new Agent(1, AgentRole.OfficeWorker, null, new Vector2Int(3, 2))
            {
                Stress = 0f,
                CrimeStressDay = -1
            };

            AgentSystem.ApplyCrimeStressDaily(agent, crime, dayIndex: 1);

            Assert.AreEqual(AgentSystem.CrimeStressPerDayAtMax, agent.Stress, 0.001f);
            Assert.AreEqual(1, agent.CrimeStressDay);
        }

        [Test]
        public void Security_exempt_from_crime_stress()
        {
            var crime = new CrimeSystem();
            crime.SetCrime(2, 100f);
            var agent = new Agent(1, AgentRole.Security, null, new Vector2Int(3, 2))
            {
                Stress = 10f,
                CrimeStressDay = -1
            };

            AgentSystem.ApplyCrimeStressDaily(agent, crime, dayIndex: 1);

            Assert.AreEqual(10f, agent.Stress, 0.001f);
            Assert.AreEqual(-1, agent.CrimeStressDay);
        }

        [Test]
        public void Criminal_on_same_floor_adds_proximity_stress()
        {
            var worker = new Agent(1, AgentRole.OfficeWorker, null, new Vector2Int(3, 2))
            {
                Stress = 0f,
                Phase = AgentPhase.Working
            };
            var criminal = new Agent(2, AgentRole.Criminal, null, new Vector2Int(8, 2))
            {
                Phase = AgentPhase.Moving,
                Visible = true
            };
            var agents = new List<Agent> { worker, criminal };

            AgentSystem.UpdateCrimeProximityStress(worker, agents, deltaGameMinutes: 10f);

            Assert.AreEqual(
                AgentSystem.CriminalProximityStressPerMinute * 10f,
                worker.Stress,
                0.001f);
        }
    }
}
