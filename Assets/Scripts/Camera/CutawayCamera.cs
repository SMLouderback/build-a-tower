using UnityEngine;

namespace BuildATower
{
    public sealed class CutawayCamera : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] float panSpeed = 1f;
        [SerializeField] float zoomSpeed = 2f;
        [SerializeField] float minOrtho = 5f;
        [SerializeField] float maxOrtho = 40f;

        const float BoundsPadding = 5f;
        const float ScrollbarThickness = 18f;
        const float FallbackMinX = -80f;
        const float FallbackMaxX = 100f;
        const float FallbackMinY = -15f;
        const float FallbackMaxY = 30f;

        Vector3 _lastMouse;

        public static Rect HorizontalScrollbarScreenRect { get; private set; }
        public static Rect VerticalScrollbarScreenRect { get; private set; }

        void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            targetCamera.orthographic = true;
        }

        void Update()
        {
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                targetCamera.orthographicSize = Mathf.Clamp(
                    targetCamera.orthographicSize - scroll * zoomSpeed, minOrtho, maxOrtho);

            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                _lastMouse = Input.mousePosition;

            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                var delta = Input.mousePosition - _lastMouse;
                _lastMouse = Input.mousePosition;
                var worldDelta = targetCamera.ScreenToWorldPoint(Vector3.zero) -
                                 targetCamera.ScreenToWorldPoint(delta);
                transform.position += new Vector3(worldDelta.x * panSpeed, worldDelta.y * panSpeed, 0f);
            }

            ClampCameraToScrollableBounds();
        }

        void OnGUI()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;

            GetScrollableBounds(out var minX, out var maxX, out var minY, out var maxY);
            var viewportHeight = targetCamera.orthographicSize * 2f;
            var viewportWidth = viewportHeight * targetCamera.aspect;
            var contentWidth = maxX - minX;
            var contentHeight = maxY - minY;
            var horizontalViewport = Mathf.Min(viewportWidth, contentWidth);
            var verticalViewport = Mathf.Min(viewportHeight, contentHeight);

            HorizontalScrollbarScreenRect = new Rect(
                0f, Screen.height - ScrollbarThickness,
                Screen.width - ScrollbarThickness, ScrollbarThickness);
            VerticalScrollbarScreenRect = new Rect(
                Screen.width - ScrollbarThickness, 0f,
                ScrollbarThickness, Screen.height - ScrollbarThickness);

            var cameraPosition = targetCamera.transform.position;
            var horizontalValue = cameraPosition.x - minX - viewportWidth * 0.5f;
            horizontalValue = GUI.HorizontalScrollbar(
                HorizontalScrollbarScreenRect, horizontalValue, horizontalViewport, 0f, contentWidth);
            var verticalValue = maxY - cameraPosition.y - viewportHeight * 0.5f;
            verticalValue = GUI.VerticalScrollbar(
                VerticalScrollbarScreenRect, verticalValue, verticalViewport, 0f, contentHeight);

            cameraPosition.x = minX + horizontalValue + viewportWidth * 0.5f;
            cameraPosition.y = maxY - verticalValue - viewportHeight * 0.5f;
            targetCamera.transform.position = ClampCameraPosition(cameraPosition, minX, maxX, minY, maxY);
        }

        public static Vector2 GetScrollableCenterRange(float min, float max, float viewportSpan)
        {
            var center = (min + max) * 0.5f;
            var halfViewport = viewportSpan * 0.5f;
            return viewportSpan >= max - min
                ? new Vector2(center, center)
                : new Vector2(min + halfViewport, max - halfViewport);
        }

        void ClampCameraToScrollableBounds()
        {
            GetScrollableBounds(out var minX, out var maxX, out var minY, out var maxY);
            targetCamera.transform.position = ClampCameraPosition(
                targetCamera.transform.position, minX, maxX, minY, maxY);
        }

        Vector3 ClampCameraPosition(Vector3 position, float minX, float maxX, float minY, float maxY)
        {
            var viewportHeight = targetCamera.orthographicSize * 2f;
            var viewportWidth = viewportHeight * targetCamera.aspect;
            var xRange = GetScrollableCenterRange(minX, maxX, viewportWidth);
            var yRange = GetScrollableCenterRange(minY, maxY, viewportHeight);
            position.x = Mathf.Clamp(position.x, xRange.x, xRange.y);
            position.y = Mathf.Clamp(position.y, yRange.x, yRange.y);
            return position;
        }

        void GetScrollableBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            var controller = FindAnyObjectByType<BuildController>();
            if (controller == null || controller.Grid == null || !controller.Grid.HasLobby)
            {
                minX = FallbackMinX;
                maxX = FallbackMaxX;
                minY = FallbackMinY;
                maxY = FallbackMaxY;
                return;
            }

            minX = controller.Grid.MinX - BoundsPadding;
            maxX = controller.Grid.MaxX + BoundsPadding;
            minY = float.MaxValue;
            maxY = float.MinValue;
            foreach (var room in controller.Grid.Rooms)
            {
                minY = Mathf.Min(minY, room.Origin.y);
                maxY = Mathf.Max(maxY, room.Origin.y + room.Size.y);
            }

            // Always allow scrolling through the painted underground dirt band.
            minY = Mathf.Min(minY, -10f);
            minX = Mathf.Min(minX, -80f);
            maxX = Mathf.Max(maxX, 100f);

            minY -= BoundsPadding;
            maxY += BoundsPadding;
        }
    }
}
