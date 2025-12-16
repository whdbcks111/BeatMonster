namespace _02.Scripts.Utils
{
    public static class ExtraMath
    {
        public static float PositiveMod(float x, float m)
        {
            var result = x % m;
            if (result < 0f) result += m;
            return result;
        }
    }
}