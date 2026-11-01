using System;

namespace Civilizations
{
    internal static class GroundFacing
    {
        public static float Angle(float x, float y)
        {
            float angle = (float)(Math.Atan2(x < 0f ? -y : y, Math.Abs(x)) * 180.0 / Math.PI);
            return Math.Max(-75f, Math.Min(75f, angle));
        }
    }
}
