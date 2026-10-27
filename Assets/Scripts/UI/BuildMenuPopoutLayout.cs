using UnityEngine;

namespace BuildATower
{
    public enum BuildMenuPopoutSide
    {
        Right,
        Left
    }

    public static class BuildMenuPopoutLayout
    {
        public static BuildMenuPopoutSide ChooseSide(Rect dock, Vector2 popoutSize, float screenWidth, float gap = 4f)
        {
            if (dock.xMax + gap + popoutSize.x <= screenWidth)
                return BuildMenuPopoutSide.Right;
            return BuildMenuPopoutSide.Left;
        }

        public static Rect Place(Rect dock, Vector2 popoutSize, BuildMenuPopoutSide side, float gap = 4f)
        {
            var x = side == BuildMenuPopoutSide.Right
                ? dock.xMax + gap
                : dock.x - gap - popoutSize.x;
            return new Rect(x, dock.y, popoutSize.x, popoutSize.y);
        }
    }
}
