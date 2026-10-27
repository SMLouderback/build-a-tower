using UnityEngine;

namespace BuildATower
{
    public static class HudFloatingPanel
    {
        public const float MinVisibleGrip = 28f;

        public static Rect SoftClamp(Rect panel, float screenWidth, float screenHeight, float minVisible = MinVisibleGrip)
        {
            var minV = Mathf.Max(1f, minVisible);
            if (panel.xMax < minV) panel.x = minV - panel.width;
            if (panel.x > screenWidth - minV) panel.x = screenWidth - minV;
            if (panel.yMax < minV) panel.y = minV - panel.height;
            if (panel.y > screenHeight - minV) panel.y = screenHeight - minV;
            return panel;
        }

        public static void SaveRect(string keyPrefix, Rect rect)
        {
            PlayerPrefs.SetFloat(keyPrefix + ".x", rect.x);
            PlayerPrefs.SetFloat(keyPrefix + ".y", rect.y);
            PlayerPrefs.SetFloat(keyPrefix + ".w", rect.width);
            PlayerPrefs.SetFloat(keyPrefix + ".h", rect.height);
            PlayerPrefs.Save();
        }

        public static bool TryLoadRect(string keyPrefix, out Rect rect)
        {
            rect = default;
            var kx = keyPrefix + ".x";
            if (!PlayerPrefs.HasKey(kx)) return false;
            if (!PlayerPrefs.HasKey(keyPrefix + ".y")) return false;
            if (!PlayerPrefs.HasKey(keyPrefix + ".w")) return false;
            if (!PlayerPrefs.HasKey(keyPrefix + ".h")) return false;
            rect = new Rect(
                PlayerPrefs.GetFloat(kx),
                PlayerPrefs.GetFloat(keyPrefix + ".y"),
                PlayerPrefs.GetFloat(keyPrefix + ".w"),
                PlayerPrefs.GetFloat(keyPrefix + ".h"));
            return true;
        }

        public static void ClearRect(string keyPrefix)
        {
            PlayerPrefs.DeleteKey(keyPrefix + ".x");
            PlayerPrefs.DeleteKey(keyPrefix + ".y");
            PlayerPrefs.DeleteKey(keyPrefix + ".w");
            PlayerPrefs.DeleteKey(keyPrefix + ".h");
            PlayerPrefs.Save();
        }

        public static bool DragGrip(Rect gripRect, ref Rect panelRect, float screenW, float screenH, ref bool dragging, ref Vector2 dragOffset)
        {
            var e = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (gripRect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        dragging = true;
                        dragOffset = e.mousePosition - new Vector2(panelRect.x, panelRect.y);
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id && dragging)
                    {
                        panelRect.x = e.mousePosition.x - dragOffset.x;
                        panelRect.y = e.mousePosition.y - dragOffset.y;
                        panelRect = SoftClamp(panelRect, screenW, screenH);
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        dragging = false;
                        e.Use();
                        return true;
                    }
                    break;
            }
            return false;
        }
    }
}
