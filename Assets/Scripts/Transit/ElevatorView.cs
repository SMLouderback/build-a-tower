using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>Draws one marker at each runtime elevator car position.</summary>
    public sealed class ElevatorView : MonoBehaviour
    {
        readonly List<SpriteRenderer> _renderers = new();
        ElevatorSystem _elevators;
        Sprite _carSprite;

        public void Bind(ElevatorSystem elevators) => _elevators = elevators;

        void LateUpdate()
        {
            if (_elevators == null) return;

            EnsureSprite();
            var shafts = _elevators.Shafts;
            while (_renderers.Count < shafts.Count)
            {
                var go = new GameObject("ElevatorCar");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _carSprite;
                renderer.color = new Color(1f, 0.78f, 0.18f, 1f);
                renderer.sortingOrder = 30;
                renderer.transform.localScale = new Vector3(0.62f, 0.48f, 1f);
                _renderers.Add(renderer);
            }

            for (var i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (i >= shafts.Count)
                {
                    renderer.enabled = false;
                    continue;
                }

                var shaft = shafts[i];
                renderer.enabled = true;
                renderer.transform.position =
                    new Vector3(shaft.X + 0.5f, shaft.Car.Floor + 0.5f, 0f);
            }
        }

        void EnsureSprite()
        {
            if (_carSprite != null) return;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            _carSprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);
        }
    }
}
