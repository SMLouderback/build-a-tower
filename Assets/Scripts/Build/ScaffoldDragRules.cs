namespace BuildATower
{
    /// <summary>
    /// Pure rules for scaffold paint-drag. Keeps sticky-drag fixes testable without a scene.
    /// </summary>
    public static class ScaffoldDragRules
    {
        public static bool ShouldEndDrag(bool dragging, bool mouseButtonHeld) =>
            dragging && !mouseButtonHeld;

        public static bool ShouldPaintCell(bool dragging, bool mouseButtonHeld) =>
            dragging && mouseButtonHeld;
    }
}
