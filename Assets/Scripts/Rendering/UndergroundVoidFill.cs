using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Camera-following dark soil void below Floor G so sky never shows under parcel dirt.
    /// </summary>
    public sealed class UndergroundVoidFill : MonoBehaviour
    {
        const int SortingOrder = -20;
        const float HorizontalPad = 4f;
        const float VerticalPad = 8f;
        const float FloorG = 0f;
        static readonly Color VoidColor = new(
            DirtBand.Color.r * 0.42f,
            DirtBand.Color.g * 0.42f,
            DirtBand.Color.b * 0.42f,
            1f);

        Camera _camera;
        Transform _plate;
        SpriteRenderer _renderer;

        public void Bind(Camera cam)
        {
            _camera = cam;
            EnsurePlate();
        }

        void EnsurePlate()
        {
            if (_plate != null) return;

            var go = new GameObject("UndergroundVoidPlate");
            go.transform.SetParent(transform, false);
            _plate = go.transform;

            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = BuildSolidSprite();
            _renderer.color = VoidColor;
            _renderer.sortingOrder = SortingOrder;
        }

        void LateUpdate()
        {
            if (_camera == null || _plate == null || _renderer == null) return;

            var camPos = _camera.transform.position;
            var halfH = _camera.orthographicSize;
            var halfW = halfH * _camera.aspect;
            var viewBottom = camPos.y - halfH;
            var viewTop = camPos.y + halfH;

            if (viewBottom >= FloorG)
            {
                _renderer.enabled = false;
                return;
            }

            var fillTop = Mathf.Min(viewTop, FloorG);
            var fillBottom = viewBottom - VerticalPad;
            var fillHeight = fillTop - fillBottom;
            if (fillHeight <= 0f)
            {
                _renderer.enabled = false;
                return;
            }

            _renderer.enabled = true;
            var fillCenterY = (fillTop + fillBottom) * 0.5f;
            var fillWidth = halfW * 2f + HorizontalPad;

            _plate.position = new Vector3(camPos.x, fillCenterY, 0f);
            _plate.localScale = new Vector3(fillWidth, fillHeight, 1f);
        }

        static Sprite BuildSolidSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "underground_void"
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
