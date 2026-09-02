using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class AgentViewSpriteTests
    {
        [Test]
        public void ShouldRenderSprite_hides_while_riding_elevator()
        {
            var agent = new Agent(1, AgentRole.OfficeWorker, null, Vector2Int.zero)
            {
                Visible = true,
                Phase = AgentPhase.Riding
            };
            Assert.IsFalse(AgentView.ShouldRenderSprite(agent));

            agent.Phase = AgentPhase.Moving;
            Assert.IsTrue(AgentView.ShouldRenderSprite(agent));

            agent.Phase = AgentPhase.WaitingAtElevator;
            Assert.IsTrue(AgentView.ShouldRenderSprite(agent));
        }

        [Test]
        public void PickWalkFrame_idle_uses_neutral_pose()
        {
            Assert.AreEqual(AgentSpriteArt.IdleFrameIndex, AgentView.PickWalkFrame(0.5f, moving: false));
        }

        [Test]
        public void PickWalkFrame_cycles_four_frames_while_moving()
        {
            Assert.AreEqual(0, AgentView.PickWalkFrame(0f, moving: true));
            Assert.AreEqual(2, AgentView.PickWalkFrame(0.25f, moving: true));
            Assert.AreEqual(0, AgentView.PickWalkFrame(0.48f, moving: true));
        }

        [Test]
        public void ShouldFlipX_holds_facing_when_idle()
        {
            Assert.IsTrue(AgentView.ShouldFlipX(0f, previousFlip: true, moving: false));
            Assert.IsFalse(AgentView.ShouldFlipX(0f, previousFlip: false, moving: false));
        }

        [Test]
        public void ShouldFlipX_follows_horizontal_travel()
        {
            Assert.IsTrue(AgentView.ShouldFlipX(-0.05f, previousFlip: false, moving: true));
            Assert.IsFalse(AgentView.ShouldFlipX(0.05f, previousFlip: true, moving: true));
        }
    }
}
