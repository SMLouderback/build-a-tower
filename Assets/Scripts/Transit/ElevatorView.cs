using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>Draws one car sprite at each runtime elevator position.</summary>
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
                renderer.color = Color.white;
                renderer.sortingOrder = 30;
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
                renderer.sprite = _carSprite;
                renderer.transform.position =
                    new Vector3(shaft.X + 0.5f, shaft.Car.Floor + 0.5f, 0f);
                // Real cars run inches from the shaft walls — fill nearly the full cell.
                var b = _carSprite != null ? _carSprite.bounds.size : Vector3.one;
                var sx = b.x > 0.01f ? 0.98f / b.x : 1f;
                var sy = b.y > 0.01f ? 0.94f / b.y : 1f;
                renderer.transform.localScale = new Vector3(sx, sy, 1f);
            }
        }

        void EnsureSprite()
        {
            if (_carSprite != null) return;
            _carSprite = LoadCarSprite() ?? BuildFallbackCar();
        }

        static Sprite LoadCarSprite()
        {
            const string path = "Art/Structure/elevator_car";
            byte[] png = null;
            var ta = Resources.Load<TextAsset>(path);
            if (ta != null) png = ta.bytes;
            if (png == null || png.Length < 32)
            {
                var srcTex = Resources.Load<Texture2D>(path);
                if (srcTex == null) return null;
                try { png = srcTex.EncodeToPNG(); }
                catch (UnityException) { return null; }
            }

            if (png == null || png.Length < 32) return null;
            var runtime = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!runtime.LoadImage(png, false)) return null;

            // Key near-black plate, then crop to opaque bounds so padding
            // doesn't leave empty gaps inside the shaft.
            var px = runtime.GetPixels();
            var w = runtime.width;
            var h = runtime.height;
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                var lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                if (lum < 0.08f) px[i] = Color.clear;
            }

            var minX = w;
            var minY = h;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (px[y * w + x].a < 0.08f) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX)
            {
                runtime.SetPixels(px);
                runtime.Apply(false, false);
                return Sprite.Create(
                    runtime,
                    new Rect(0, 0, w, h),
                    new Vector2(0.5f, 0.5f),
                    64f);
            }

            var cw = maxX - minX + 1;
            var ch = maxY - minY + 1;
            var cropped = new Color[cw * ch];
            for (var y = 0; y < ch; y++)
            for (var x = 0; x < cw; x++)
                cropped[y * cw + x] = px[(minY + y) * w + (minX + x)];

            var croppedTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            croppedTex.SetPixels(cropped);
            croppedTex.Apply(false, false);
            Object.Destroy(runtime);
            return Sprite.Create(
                croppedTex,
                new Rect(0, 0, cw, ch),
                new Vector2(0.5f, 0.5f),
                64f);
        }

        static Sprite BuildFallbackCar()
        {
            const int w = 32;
            const int h = 40;
            var px = new Color[w * h];
            var body = new Color(0.72f, 0.58f, 0.28f, 1f);
            var dark = new Color(0.35f, 0.28f, 0.16f, 1f);
            var win = new Color(0.55f, 0.75f, 0.85f, 1f);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (x < 3 || x >= w - 3 || y < 2 || y >= h - 4)
                    px[y * w + x] = dark;
                else if (y > h * 0.45f && y < h * 0.78f && x > 7 && x < w - 7)
                    px[y * w + x] = win;
                else
                    px[y * w + x] = body;
            }

            // Cable stub
            for (var y = h - 4; y < h; y++)
            {
                px[y * w + w / 2] = dark;
                px[y * w + w / 2 - 1] = dark;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 40f);
        }
    }
}
