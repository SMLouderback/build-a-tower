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

        Vector3 _lastMouse;

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
        }
    }
}
