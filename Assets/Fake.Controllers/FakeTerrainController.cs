using System.Runtime.CompilerServices;
using Fake.Dynamics;
using Fake.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float2x2 = Unity.Mathematics.float2x2;

namespace Fake.Controllers
{
    [RequireComponent(typeof(LineRenderer))]
    public class FakeTerrainController : MonoBehaviour, ICollisionSolver
    {
        [BurstCompile]
        private unsafe struct GridCollisionJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Cell* gridPtr;
            public uint fixedPointMultiplier;

            public float relaxation;

            [NativeDisableUnsafePtrRestriction]
            public float* heights;
            public int heightLength;
            public int resolution;

            public float deltaTime;
            
            public void Execute(int i)
            {
                int x = i / 64;
                int y = i - x * 64;

                float2 gridPosition = float2(x, y);
                float2 displacement = FixedPointUtility.DecodeFixedPoint(gridPtr[i].displacement, fixedPointMultiplier);
                float2 displacedGridPosition = gridPosition + displacement;
                
                var (height, normal) = LerpHeight(heights, heightLength, displacedGridPosition.x, resolution);
                normal = -normal;
                
                var collides = displacedGridPosition.y < height;
                var point = float2(displacedGridPosition.x, height);
                
                if (collides)
                {
                    float gap = min(0.0f, dot(normal, point - gridPosition));
                    float gridPenetration = dot(normal, displacement) - gap;
                    float radialImpulse = max(gridPenetration, 0.0f);
                    
                    displacement -= radialImpulse * normal * (1.0f - relaxation);
                }
                
                gridPtr[i].displacement = FixedPointUtility.EncodeFixedPoint(displacement, fixedPointMultiplier);
            }
        }
        
        [BurstCompile]
        private unsafe struct ParticleCollisionJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Dynamics.Particle* particlePtr;

            public float relaxation;
            
            [NativeDisableUnsafePtrRestriction]
            public float* heights;
            public int heightLength;
            public int resolution;
            
            public float deltaTime;
            
            public void Execute(int i)
            {
                var (height, normal) = LerpHeight(heights, heightLength, particlePtr[i].position.x, resolution);
                normal = -normal;
                
                bool collides = particlePtr[i].position.y < height;
                float penetration = distance(height, particlePtr[i].position.y);
                
                if (collides)
                {
                    particlePtr[i].displacement -= penetration * normal * (1.0f - relaxation);
                } 
            }
        }

        private const int m_Resolution = 64;
        
        [SerializeField] private float m_Size;
        [Range(0.0f, 1.0f)]
        [SerializeField] private float m_Relaxation;
        [SerializeField] private float[] m_Heights;
        
        private LineRenderer m_LineRenderer;
        
        // private Terrain.Terrain m_Terrain;

        private void OnEnable()
        {
            m_LineRenderer = GetComponent<LineRenderer>();
            
            // m_Terrain ??= new Terrain.Terrain(64);
            m_LineRenderer.positionCount = m_Heights.Length;

            // for (int i = 0; i < m_Terrain.Resolution; i++)
            // {
            //     m_Terrain.SetHeight(i, m_Height);
            // }
        }

        private void Start()
        {
            if (TryGetComponent<FakeDynamicsController>(out var controller) && controller.enabled)
            {
                controller.RegisterCollisionSolver(this);
            }
        }

        private void Update()
        {
            var xOffset = (float)m_Resolution / (float)(m_Heights.Length - 1);
            var gridHalfSize = 0.5f * new Vector2(m_Resolution, m_Resolution);
            
            for (int i = 0; i < m_Heights.Length; i++)
            {
                var x = xOffset * i;
                var y = Mathf.Clamp01(m_Heights[i]) * (float)m_Resolution;
                var p = (new Vector2(x, y) - gridHalfSize) * m_Size;

                m_LineRenderer.SetPosition(i, new Vector3(p.x, p.y, 0.0f));
            }
        }

        public unsafe void ResolveCollisions(NativeArray<Cell> grid, uint fixedPointMultiplier, float deltaTime)
        {
            fixed (float* heights = m_Heights)
            {
                new GridCollisionJob
                {
                    gridPtr = (Cell*)grid.GetUnsafePtr(),
                    fixedPointMultiplier = fixedPointMultiplier,
                    relaxation = m_Relaxation,
                    heights = heights,
                    heightLength = m_Heights.Length,
                    resolution = m_Resolution,
                }.Schedule(grid.Length, 64).Complete();
            }
        }

        public unsafe void ResolveCollisions(NativeArray<Dynamics.Particle> particles, uint fixedPointMultiplier, float deltaTime)
        {
            fixed (float* heights = m_Heights)
            {
                new ParticleCollisionJob
                {
                    particlePtr = (Dynamics.Particle*)particles.GetUnsafePtr(),
                    relaxation = m_Relaxation,
                    heights = heights,
                    heightLength = m_Heights.Length,
                    resolution = m_Resolution,
                }.Schedule(particles.Length, 64).Complete();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe (float, float2) LerpHeight(float* heights, int heightLength, float x, int gridResolution)
        {
            if (abs(x - gridResolution) < EPSILON)
            {
                return (clamp(heights[heightLength - 1], 0.0f, 1.0f), float2(0.0f, 1.0f));
            }
            
            float offset = (float)gridResolution / (float)heightLength;
            int i = (int)floor(x / gridResolution * (heightLength - 1));
            float t = (x - i * offset) / offset;
            float h0 = clamp(heights[i], 0.0f, 1.0f);
            float h1 = clamp(heights[i + 1], 0.0f, 1.0f);

            float2 p0 = float2(i * offset, h0);
            float2 p1 = float2((i + 1) * offset, h1);
            float2 dir = normalize(p1 - p0);
            
            return (lerp(h0, h1, t) * gridResolution, float2(dir.y, dir.x));
        }

        public struct CollideResult
        {
            public bool collides;
            public float penetration;
            public float2 normal;
            public float2 point;
        }

        public static CollideResult BoxCollide(float2 shapePosition, float shapeRotation, float2 shapeHalfSize, float2 position)
        {
            float2 offset = position - shapePosition;
            float2x2 R = float2x2.Rotate(shapeRotation * TORADIANS);
            float2 rotOffset = mul(R, offset);
            float sx = sign(rotOffset.x);
            float sy = sign(rotOffset.y);
            float2 penetration = -(abs(rotOffset) - shapeHalfSize);
            var normal = mul(transpose(R), penetration.y < penetration.x ? float2(sx , 0) : float2(0 , sy));
            var minPen = min(penetration.x, penetration.y);
    
            var pointOnBox = shapePosition + mul(transpose(R), clamp(rotOffset, -shapeHalfSize, shapeHalfSize));
    
            return new CollideResult
            {
                collides = minPen > 0,
                penetration = minPen,
                normal = -normal,
                point = pointOnBox
            };
        }
    }
}
