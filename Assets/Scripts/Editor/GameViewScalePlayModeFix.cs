#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuildATower.EditorTools
{
    /// <summary>
    /// Game view Scale &gt; 1x zooms/crops the framebuffer and clips the IMGUI HUD.
    /// Reset Scale to 1 when entering Play Mode so the menu stays on-screen.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewScalePlayModeFix
    {
        static GameViewScalePlayModeFix()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += ResetGameViewScaleToOne;
        }

        [MenuItem("Build-A-Tower/Reset Game View Scale to 1x")]
        public static void ResetGameViewScaleToOne()
        {
            try
            {
                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null) return;

                var views = Resources.FindObjectsOfTypeAll(gameViewType);
                if (views == null || views.Length == 0) return;

                foreach (var view in views)
                {
                    if (view is not EditorWindow window) continue;

                    // Newer Unity: public/internal scale property
                    var scaleProp = gameViewType.GetProperty(
                        "scale",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (scaleProp != null && scaleProp.CanWrite)
                    {
                        scaleProp.SetValue(window, 1f, null);
                        window.Repaint();
                        continue;
                    }

                    // Fallback: ZoomableArea.m_Scale (Vector2)
                    var zoomField = gameViewType.GetField(
                        "m_ZoomArea",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    var zoomArea = zoomField?.GetValue(window);
                    if (zoomArea == null) continue;

                    var scaleField = zoomArea.GetType().GetField(
                        "m_Scale",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (scaleField != null)
                    {
                        scaleField.SetValue(zoomArea, Vector2.one);
                        window.Repaint();
                    }
                }

                Debug.Log("Build-A-Tower: Game view Scale set to 1x so the HUD stays visible.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Build-A-Tower: could not auto-reset Game view Scale. " +
                    "Drag the Scale slider to 1x manually. Details: " + ex.Message);
            }
        }
    }
}
#endif
