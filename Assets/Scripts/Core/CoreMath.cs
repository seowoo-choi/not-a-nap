namespace NotANap.Core
{
    internal static class CoreMath
    {
        public static double Clamp(double v, double min, double max)
            => v < min ? min : v > max ? max : v;

        public static double Clamp01(double v) => Clamp(v, 0, 1);
    }
}
