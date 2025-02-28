using System.Runtime.CompilerServices;
using Unity.Mathematics;

using float2x2 = Unity.Mathematics.float2x2;
using static Unity.Mathematics.math;

namespace Fake.Utilities
{
    public static class MatrixUtility
    {
        public struct SVDResult
        {
            public float2x2 u;
            public float2 sigma;
            public float2x2 vt;
        }

        // https://scicomp.stackexchange.com/questions/8899/robust-algorithm-for-2-times-2-svd/14103#14103
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SVDResult SingularValuesDecomposition(float2x2 m)
        {
            float m00 = m.c0.x;
            float m01 = m.c1.x;
            float m10 = m.c0.y;
            float m11 = m.c1.y;
            
            float E = (m00 + m11) * 0.5f;
            float F = (m00 - m11) * 0.5f;
            float G = (m10 + m01) * 0.5f;
            float H = (m10 - m01) * 0.5f;
            
            float Q = sqrt(E * E + H * H);
            float R = sqrt(F * F + G * G);
            float sx = Q + R;
            float sy = Q - R;
            
            float a1 = atan2(G, F);
            float a2 = atan2(H, E);
            
            float theta = (a2 - a1) * 0.5f;
            float phi = (a2 + a1) * 0.5f;
            
            return new SVDResult
            {
                u = float2x2.Rotate(phi),
                sigma = float2(sx, sy),
                vt = float2x2.Rotate(theta)
            };
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Tr(float2x2 m)
        {
            float m00 = m.c0.x;
            float m11 = m.c1.y;
            
            return m00 + m11;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x2 OuterProduct(float2 x, float2 y)
        {
            return float2x2(x * y.x, x * y.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2x2 Diagonal(float2 v)
        {
            return new float2x2(v.x, 0.0f, 0.0f, v.y);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Length(float2x2 m)
        {
            float m00 = m.c0.x;
            float m11 = m.c1.y;
            
            return length(float2(m00, m11));
        }
    }
}
