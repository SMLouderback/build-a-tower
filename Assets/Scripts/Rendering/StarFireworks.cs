using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Procedural sky fireworks for star promotions. Sorting sits above sky/parallax and below cars (order 30).
    /// </summary>
    public static class StarFireworks
    {
        const int SortingOrder = 8;
        static GameObject _root;

        public static void Play(Transform parent, Vector3 towerTopWorld)
        {
            Stop();

            _root = new GameObject("StarFireworks");
            if (parent != null)
                _root.transform.SetParent(parent, worldPositionStays: false);
            _root.transform.position = towerTopWorld + Vector3.up * 1.5f;

            SpawnBurst(_root.transform, Vector3.zero, new Color(1f, 0.85f, 0.35f), 28);
            SpawnBurst(_root.transform, new Vector3(-1.4f, 0.6f, 0f), new Color(1f, 0.45f, 0.2f), 22);
            SpawnBurst(_root.transform, new Vector3(1.5f, 0.4f, 0f), new Color(1f, 0.95f, 0.55f), 22);
            SpawnBurst(_root.transform, new Vector3(0.2f, 1.2f, 0f), new Color(1f, 0.55f, 0.75f), 18);
        }

        public static void Stop()
        {
            if (_root == null) return;
            Object.Destroy(_root);
            _root = null;
        }

        static void SpawnBurst(Transform parent, Vector3 localOffset, Color color, int count)
        {
            var go = new GameObject("Burst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1.2f;
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = color;
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count * 4;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)count),
                new ParticleSystem.Burst(0.55f, (short)(count / 2)),
                new ParticleSystem.Burst(1.1f, (short)count)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.35f), 0.35f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = SortingOrder;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
                renderer.material = new Material(shader);

            ps.Play();
        }
    }
}
