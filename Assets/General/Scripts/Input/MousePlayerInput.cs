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

            var screen = UnityEngine.Input.mousePosition;
            screen.z = -worldCamera.transform.position.z;
            var world = worldCamera.ScreenToWorldPoint(screen);
            CursorWorldPosition = new Vector2(world.x, world.y);

            IsAiming = UnityEngine.Input.GetMouseButton(1);
        }
    }
}
