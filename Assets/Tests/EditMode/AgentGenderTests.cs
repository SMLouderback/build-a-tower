using System;
using System.Collections.Generic;
using BuildATower;
using NUnit.Framework;

namespace BuildATower.Tests
{
    public class AgentGenderTests
    {
        [Test]
        public void RollGender_with_fixed_seed_produces_both_genders()
        {
            var rng = new Random(42);
            var genders = new HashSet<AgentGender>();
            for (var i = 0; i < 50; i++)
                genders.Add(AgentSystem.RollGender(rng));

            Assert.That(genders, Does.Contain(AgentGender.Male));
            Assert.That(genders, Does.Contain(AgentGender.Female));
        }

        [Test]
        public void Agent_has_gender_property()
        {
            var agent = new Agent(1, AgentRole.OfficeWorker, null, default)
            {
                Gender = AgentGender.Female
            };
            Assert.AreEqual(AgentGender.Female, agent.Gender);
        }
    }
}
