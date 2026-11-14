using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Pooled world-space floor labels centered on each elevator shaft footprint.
    /// </summary>
    public sealed class ElevatorShaftFloorLabels
    {
        public const int SortingOrder = 24;
        public const string ContainerName = "ElevatorFloorLabels";

        public static readonly Color ActiveColor = new(0.95f, 0.95f, 0.98f, 1f);
        public static readonly Color MutedColor = new(0.7f, 0.72f, 0.78f, 0.4f);

        const float CharacterSize = 0.1f;
        const int FontSize = 32;

        readonly List<TextMesh> _pool = new();
        Transform _container;

        public void Sync(TowerGrid grid, ElevatorSystem elevators, Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            EnsureContainer(parent);

            var used = 0;
            if (elevators != null)
            {
                foreach (var shaft in elevators.Shafts)
                {
                    for (var y = shaft.MinFloor; y <= shaft.MaxFloor; y++)
                    {
                        var label = EnsureLabel(used++);
                        label.gameObject.SetActive(true);
                        label.text = FloorLabels.Format(y, grid);
                        label.color = FloorLabels.IsActive(shaft, y)
                            ? ActiveColor
                            : MutedColor;
                        label.transform.position = new Vector3(
                            shaft.X + shaft.Width * 0.5f,
                            y + 0.5f,
                            0f);
                    }
                }
            }

            for (var i = used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        public void Clear()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                    _pool[i].gameObject.SetActive(false);
            }
        }

        void EnsureContainer(Transform parent)
        {
            if (_container != null && _container.parent == parent)
                return;

            var existing = parent.Find(ContainerName);
            if (existing != null)
            {
                _container = existing;
                return;
            }

            var go = new GameObject(ContainerName);
            go.transform.SetParent(parent, false);
            _container = go.transform;
        }

        TextMesh EnsureLabel(int index)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("FloorLabel");
                go.transform.SetParent(_container, false);
                var tm = go.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.characterSize = CharacterSize;
                tm.fontSize = FontSize;
                tm.fontStyle = FontStyle.Bold;
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (font != null)
                    tm.font = font;

                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = SortingOrder;

                _pool.Add(tm);
            }

            return _pool[index];
        }
    }
}
