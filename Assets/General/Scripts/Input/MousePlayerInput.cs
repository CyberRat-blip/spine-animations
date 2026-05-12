using UnityEngine;

namespace Gameplay.Input
{
    public sealed class MousePlayerInput : MonoBehaviour, IPlayerInput
    {
        [SerializeField] private Camera worldCamera;

        public Vector2 CursorWorldPosition { get; private set; }
        public bool IsAiming { get; private set; }

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        public void Tick()
        {
            if (worldCamera == null)
            {
                return;
            }

            var mouseScreenPoint = UnityEngine.Input.mousePosition;
            mouseScreenPoint.z = -worldCamera.transform.position.z;
            var worldPoint = worldCamera.ScreenToWorldPoint(mouseScreenPoint);
            CursorWorldPosition = new Vector2(worldPoint.x, worldPoint.y);

            IsAiming = UnityEngine.Input.GetMouseButton(1);
        }
    }
}
