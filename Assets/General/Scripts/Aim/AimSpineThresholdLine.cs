using UnityEngine;

namespace Gameplay.Aim
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AimSpineThresholdLine : MonoBehaviour
    {
        [SerializeField] private SpineAimController aim;
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float horizontalHalfWidth = 12f;
        [SerializeField] private float zOffset;

        private void Reset()
        {
            aim = GetComponent<SpineAimController>();
            if (aim == null)
            {
                aim = GetComponentInParent<SpineAimController>();
            }

            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.loop = false;
                lineRenderer.positionCount = 2;
            }
        }

        private void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
        }

        private void LateUpdate()
        {
            if (lineRenderer == null || aim == null)
            {
                return;
            }

            var center = aim.transform.position;
            var y = aim.AimSpineThresholdWorldY;
            var z = center.z + zOffset;
            var half = Mathf.Max(0.01f, horizontalHalfWidth);
            lineRenderer.SetPosition(0, new Vector3(center.x - half, y, z));
            lineRenderer.SetPosition(1, new Vector3(center.x + half, y, z));
        }
    }
}
