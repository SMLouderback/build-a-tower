using NUnit.Framework;

namespace BuildATower.Tests
{
    public class ScaffoldDragRulesTests
    {
        [Test]
        public void ShouldEndDrag_when_dragging_and_button_released()
        {
            Assert.IsTrue(ScaffoldDragRules.ShouldEndDrag(dragging: true, mouseButtonHeld: false));
            Assert.IsFalse(ScaffoldDragRules.ShouldEndDrag(dragging: true, mouseButtonHeld: true));
            Assert.IsFalse(ScaffoldDragRules.ShouldEndDrag(dragging: false, mouseButtonHeld: false));
        }

        [Test]
        public void ShouldPaintCell_only_while_button_held_during_drag()
        {
            Assert.IsTrue(ScaffoldDragRules.ShouldPaintCell(dragging: true, mouseButtonHeld: true));
            Assert.IsFalse(ScaffoldDragRules.ShouldPaintCell(dragging: true, mouseButtonHeld: false));
            Assert.IsFalse(ScaffoldDragRules.ShouldPaintCell(dragging: false, mouseButtonHeld: true));
        }
    }
}
