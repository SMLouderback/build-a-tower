using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Drives the main camera clear color from the simulation clock (day / night / dawn / dusk).
    /// </summary>
    public sealed class DayNightSkyController : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] TowerSimulation simulation;

        void Awake()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>() ?? Camera.main;
            if (simulation == null)
                simulation = FindAnyObjectByType<TowerSimulation>();
        }

        void LateUpdate()
        {
            if (targetCamera == null) return;
            if (simulation == null)
                simulation = FindAnyObjectByType<TowerSimulation>();

            targetCamera.backgroundColor = DayNightSky.ColorAt(simulation?.Clock);
        }
    }
}
