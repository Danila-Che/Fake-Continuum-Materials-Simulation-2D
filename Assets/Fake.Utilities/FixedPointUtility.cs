using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Fake.Utilities
{
    public static class FixedPointUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecodeFixedPoint(int fixedPoint, uint fixedPointMultiplier)
        {
            return (float)fixedPoint / (float)fixedPointMultiplier;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 DecodeFixedPoint(int2 fixedPoint, uint fixedPointMultiplier)
        {
            return (float2)fixedPoint / new float2(fixedPointMultiplier);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EncodeFixedPoint(float floatingPoint, uint fixedPointMultiplier)
        {
            return (int)(floatingPoint * (float)fixedPointMultiplier);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 EncodeFixedPoint(float2 floatingPoint, uint fixedPointMultiplier)
        {
            return (int2)(floatingPoint * new float2(fixedPointMultiplier));
        }
    }
}
