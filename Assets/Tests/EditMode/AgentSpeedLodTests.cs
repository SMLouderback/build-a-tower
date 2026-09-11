using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentSpeedLodTests
    {
        static Agent AgentWithPhase(AgentPhase phase) =>
            new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero)
            {
                Visible = true,
                Phase = phase
            };

        [TestCase(9.99f, false)]
        [TestCase(10f, true)]
        [TestCase(60f, true)]
        public void IsHighSpeed_uses_threshold(float rate, bool expected) =>
            Assert.AreEqual(expected, AgentSpeedLod.IsHighSpeed(rate));

        [TestCase(AgentPhase.AtHome, true)]
        [TestCase(AgentPhase.Working, true)]
        [TestCase(AgentPhase.Staying, true)]
        [TestCase(AgentPhase.VisitingShop, true)]
        [TestCase(AgentPhase.Moving, false)]
        [TestCase(AgentPhase.WaitingAtElevator, false)]
        [TestCase(AgentPhase.Riding, false)]
        public void Idle_in_room_phases(AgentPhase phase, bool expected) =>
            Assert.AreEqual(expected, AgentSpeedLod.IsIdleInRoom(phase));

        [Test]
        public void NeedsLodHide_false_for_idle_room_and_riders()
        {
            var idle = AgentWithPhase(AgentPhase.AtHome);
            var rider = AgentWithPhase(AgentPhase.Riding);
            var walker = AgentWithPhase(AgentPhase.Moving);
            Assert.IsFalse(AgentSpeedLod.NeedsLodHide(idle));
            Assert.IsFalse(AgentSpeedLod.NeedsLodHide(rider));
            Assert.IsTrue(AgentSpeedLod.NeedsLodHide(walker));
        }

        [Test]
        public void TryBeginFade_respects_per_frame_budget()
        {
            var started = 0;
            for (var i = 0; i < AgentSpeedLod.MaxNewFadesPerFrame + 5; i++)
            {
                if (AgentSpeedLod.TryBeginFade(ref started))
                    continue;
                Assert.AreEqual(AgentSpeedLod.MaxNewFadesPerFrame, started);
                return;
            }
            Assert.Fail("budget never blocked");
        }

        [Test]
        public void StepFade_reaches_zero_and_one()
        {
            var a = 1f;
            a = AgentSpeedLod.StepAlpha(a, target: 0f, dt: 10f);
            Assert.AreEqual(0f, a, 0.001f);
            a = AgentSpeedLod.StepAlpha(a, target: 1f, dt: 10f);
            Assert.AreEqual(1f, a, 0.001f);
        }

        [Test]
        public void TargetAlpha_idle_room_wins_over_high_speed()
        {
            Assert.AreEqual(
                1f,
                AgentSpeedLod.TargetAlpha(
                    highSpeed: true,
                    needsLodHide: false,
                    isIdleInRoom: true));
            Assert.AreEqual(
                0f,
                AgentSpeedLod.TargetAlpha(
                    highSpeed: true,
                    needsLodHide: true,
                    isIdleInRoom: false));
            Assert.AreEqual(
                1f,
                AgentSpeedLod.TargetAlpha(
                    highSpeed: false,
                    needsLodHide: true,
                    isIdleInRoom: false));
        }
    }
}
