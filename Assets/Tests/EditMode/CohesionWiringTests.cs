using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class CohesionWiringTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyAll<SkyCloudLayer>();
            DestroyAll<WeatherFx>();
            DestroyAll<MetroTunnelView>();
            DestroyAll<ParallaxBackdrop>();
            DestroyAll<TowerSimulation>();
            DestroyAll<BuildController>();
            DestroyAll<AgentView>();
            DestroyAll<ElevatorView>();
        }

        [Test]
        public void TowerSimulation_initialize_wires_weather_clouds_and_metro_views()
        {
            var go = new GameObject("CohesionSimulationHost");
            go.AddComponent<BuildController>();
            var simulation = go.AddComponent<TowerSimulation>();

            simulation.InitializeSimulation();

            Assert.IsNotNull(Object.FindAnyObjectByType<SkyCloudLayer>());
            Assert.IsNotNull(Object.FindAnyObjectByType<WeatherFx>());
            Assert.IsNotNull(Object.FindAnyObjectByType<MetroTunnelView>());
        }

        static void DestroyAll<T>() where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    Object.DestroyImmediate(found[i].gameObject);
            }
        }
    }
}
