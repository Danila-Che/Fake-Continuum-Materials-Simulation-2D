using System.Runtime.CompilerServices;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Fake.Dynamics
{
    public struct Particle
    {
        public float2 position;
        public float2 displacement;
        public float2x2 deformationGradient;
        public float2x2 deformationDisplacement;

        public float liquidDensity;
        public float mass;
        public float volume;
        public float logJp;
    }

    public static class ParticleUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ProjectInsideGuardian(float2 position, int gridSize, float guardianSize)
        {
            float2 clampMin = float2(guardianSize);
            float2 clampMax = float2(gridSize) - float2(guardianSize) - float2(1.0f);

            return clamp(position, clampMin, clampMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsideGuardian(uint2 id, uint2 gridSize, float guardianSize)
        {
            if(id.x <= guardianSize)
            {
                return false;
            }

            if(id.x >= gridSize.x - guardianSize - 1)
            {
                return false;
            }

            if(id.y <= guardianSize)
            {
                return false;
            }

            if(id.y >= gridSize.y - guardianSize - 1)
            {
                return false;
            }

            return true;
        }
    }
}
