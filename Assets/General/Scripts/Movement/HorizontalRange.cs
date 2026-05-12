using UnityEngine;

namespace Gameplay.Movement
{
    public readonly struct HorizontalRange
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float YLine;

        public HorizontalRange(float minX, float maxX, float yLine)
        {
            if (maxX <= minX)
            {
                maxX = minX + 0.0001f;
            }

            MinX = minX;
            MaxX = maxX;
            YLine = yLine;
        }

        public float Width => MaxX - MinX;

        public float Clamp(float x) => Mathf.Clamp(x, MinX, MaxX);

        public float Normalize(float x) => (Clamp(x) - MinX) / Width;
    }
}
