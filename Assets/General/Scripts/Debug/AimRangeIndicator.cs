using UnityEngine;

namespace Gameplay
{
    [RequireComponent(typeof(LineRenderer))]
    [ExecuteAlways]
    public sealed class AimRangeIndicator : MonoBehaviour
    {
        [SerializeField] private float characterHeight = 2f;
        [SerializeField] private float heightMultiplier = 2f;
        [SerializeField] private int segments = 64;
        [SerializeField] private Vector2 centerOffset = new Vector2(0f, 1f);
        [SerializeField] private LineRenderer line;

        private void Reset()
        {
            line = GetComponent<LineRenderer>();
            if (line != null)
            {
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = 0.03f;
            }
            Rebuild();
        }

        private void OnValidate()
        {
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
            }
            Rebuild();
        }

        private void Awake()
        {
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
            }
            Rebuild();
        }

        private void Rebuild()
        {
            if (line == null)
            {
                return;
            }

            var radius = Mathf.Max(0f, characterHeight * heightMultiplier);
            var segs = Mathf.Max(8, segments);

            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segs;

            for (var i = 0; i < segs; i++)
            {
                var t = (float)i / segs * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(
                    centerOffset.x + Mathf.Cos(t) * radius,
                    centerOffset.y + Mathf.Sin(t) * radius,
                    0f));
            }
        }
    }
}
