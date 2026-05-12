using UnityEngine;

namespace Gameplay.Aim
{
    public sealed class WorldCursorCrosshair : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField] private float fadeTime = 0.12f;
        [SerializeField] private SpriteRenderer fadeRenderer;

        private float _alpha;

        private void Awake()
        {
            if (view != null)
            {
                view.gameObject.SetActive(false);
            }
        }

        public void Tick(bool isAiming, Vector2 cursorWorldPosition)
        {
            if (view == null)
            {
                return;
            }

            var fadeSpeed = 1f / Mathf.Max(0.0001f, fadeTime);
            _alpha = Mathf.MoveTowards(_alpha, isAiming ? 1f : 0f, fadeSpeed * Time.deltaTime);

            var shouldBeVisible = _alpha > 0f;
            if (view.gameObject.activeSelf != shouldBeVisible)
            {
                view.gameObject.SetActive(shouldBeVisible);
            }

            if (!shouldBeVisible)
            {
                return;
            }

            view.position = new Vector3(cursorWorldPosition.x, cursorWorldPosition.y, view.position.z);

            if (fadeRenderer != null)
            {
                var c = fadeRenderer.color;
                c.a = _alpha;
                fadeRenderer.color = c;
            }
        }
    }
}
