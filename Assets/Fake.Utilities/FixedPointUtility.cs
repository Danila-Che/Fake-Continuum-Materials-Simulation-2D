namespace Fake.Utilities
{
    public static class FixedPointUtility
    {
        public static float DecodeFixedPoint(int fixedPoint, uint fixedPointMultiplier)
        {
            return (float)fixedPoint / (float)fixedPointMultiplier;
        }

        public static int EncodeFixedPoint(float floatingPoint, uint fixedPointMultiplier)
        {
            return (int)(floatingPoint * (float)fixedPointMultiplier);
        }
    }
}
