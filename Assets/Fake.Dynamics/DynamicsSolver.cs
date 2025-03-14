using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Fake.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using static Unity.Mathematics.math;
using static Fake.Utilities.MatrixUtility;
using static Fake.Utilities.FixedPointUtility;
using float2 = Unity.Mathematics.float2;
using float2x2 = Unity.Mathematics.float2x2;

namespace Fake.Dynamics
{
    public class DynamicsSolver : IDisposable
    {
        public enum MaterialType
        {
            Liquid,
            Sand,
            Visco,
            Elastic
        }

        [BurstCompile]
        private unsafe struct ParticleUpdateJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Particle* particlePtr;
            
            public MaterialType materialType;
            public float elasticityRatio;
            public float liquidRelaxation;
            public float elasticRelaxation;
            public float liquidViscosity;
            
            public void Execute(int i)
            {
                var particle = particlePtr[i];

                if (materialType is MaterialType.Liquid)
                {
                    float2x2 deviatoric = -1.0f * (particle.deformationDisplacement + transpose(particle.deformationDisplacement));
                    particle.deformationDisplacement += liquidViscosity * 0.5f * deviatoric;
                    
                    float alpha = 0.5f * (1.0f / particle.liquidDensity - Tr(particle.deformationDisplacement) - 1.0f);
                    particle.deformationDisplacement += liquidRelaxation * alpha * float2x2.identity;
                }
                if (materialType is MaterialType.Sand)
                {
                    float2x2 F = mul(float2x2.identity + particle.deformationDisplacement, particle.deformationGradient);
                    
                    var svdResult = SingularValuesDecomposition(F);
                    
                    if (particle.logJp == 0.0f)
                    {
                        svdResult.sigma = clamp(svdResult.sigma, float2(1.0f), float2(1000.0f));
                    }
                    
                    float df = determinant(F);
                    float cdf = clamp(abs(df), 0.1f, 1000.0f);
                    float2x2 Q = 1.0f / (sign(df) * sqrt(cdf)) * F;
                    
                    float2x2 elasticPart = mul(mul(svdResult.u, Diagonal(svdResult.sigma)), svdResult.vt);
                    float alpha = elasticityRatio;
                    float2x2 tgt = alpha * elasticPart + (1.0f - alpha) * Q;
                    
                    float2x2 diff = mul(tgt, inverse(particle.deformationGradient)) - float2x2.identity - particle.deformationDisplacement;
                    particle.deformationDisplacement += elasticRelaxation * diff;
                    
                    float2x2 deviatoric = -1.0f * (particle.deformationDisplacement + transpose(particle.deformationDisplacement));
                    particle.deformationDisplacement += liquidViscosity * 0.5f * deviatoric;
                }
                else if (materialType is MaterialType.Visco or MaterialType.Elastic)
                {
                    float2x2 F = mul(float2x2.identity + particle.deformationDisplacement, particle.deformationGradient);

                    var svdResult = SingularValuesDecomposition(F);
                    
                    float df = determinant(F);
                    float cdf = clamp(abs(df), 0.1f, 1000.0f);
                    float2x2 Q = 1.0f / (sign(df) * sqrt(cdf)) * F;

                    float alpha = elasticityRatio;
                    float2x2 tgt = alpha * mul(svdResult.u, svdResult.vt) + (1.0f - alpha) * Q;

                    float2x2 diff = mul(tgt, inverse(particle.deformationGradient)) - float2x2.identity - particle.deformationDisplacement;
                    particle.deformationDisplacement += elasticRelaxation * diff;
                }

                particlePtr[i] = particle;
            }
        }
        
        [BurstCompile]
        private unsafe struct ParticleToGridJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Cell* gridPtr;
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public Particle* particlePtr;

            public uint fixedPointMultiplier;            
            
            public bool useGridVolumeForLiquid;
            public int gridResolution;

            public void Execute(int i)
            {
                var weights = stackalloc float2[3];
                var particle = particlePtr[i];
                uint2 cellCoordinate = CalculateQuadraticInterpolationWeights(weights, particle);

                for (uint gx = 0; gx < 3; gx++)
                {
                    for (uint gy = 0; gy < 3; gy++)
                    {
                        float weight = GetWeight(weights, gx, gy);
                        uint2 neighborCellCoordinate = cellCoordinate + uint2(gx, gy) - 1;
                        
                        int cellIndex = GetCellIndex(gridResolution, neighborCellCoordinate);
                        
                        float weightedMass = weight * particle.mass;
                        Interlocked.Add(ref gridPtr[cellIndex].mass, EncodeFixedPoint(weightedMass, fixedPointMultiplier));
                        
                        float2 cellDistance = neighborCellCoordinate - particle.position + 0.5f;
                        float2 Q = mul(particle.deformationDisplacement, cellDistance);
                        float2 momentum = weightedMass * (particle.displacement + Q);
                        Interlocked.Add(ref gridPtr[cellIndex].displacement.x, EncodeFixedPoint(momentum.x, fixedPointMultiplier));
                        Interlocked.Add(ref gridPtr[cellIndex].displacement.y, EncodeFixedPoint(momentum.y, fixedPointMultiplier));

                        if (useGridVolumeForLiquid)
                        {
                            Interlocked.Add(ref gridPtr[cellIndex].volume, EncodeFixedPoint(particle.volume * weight, fixedPointMultiplier));
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private unsafe struct GridUpdateJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Cell* gridPtr;

            public uint fixedPointMultiplier;

            public MaterialType materialType;
            public float2 acceleration;
            public float deltaTime;
            
            public void Execute(int i)
            {
                var cell = gridPtr[i];

                float2 displacement = DecodeFixedPoint(cell.displacement, fixedPointMultiplier);
                float mass = DecodeFixedPoint(cell.mass, fixedPointMultiplier);

                if (mass > 0.0f)
                {
                    displacement /= mass;

                    cell.displacement = EncodeFixedPoint(displacement, fixedPointMultiplier);

                    gridPtr[i] = cell;
                }
            }
        }

        [BurstCompile]
        private unsafe struct GridLimitJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Cell* gridPtr;
            
            public uint fixedPointMultiplier;
            public int gridResolution;
            
            public void Execute(int i)
            {
                float2 displacement = DecodeFixedPoint(gridPtr[i].displacement, fixedPointMultiplier);
                
                int x = i / gridResolution;
                int y = i - x * gridResolution;
                    
                if (x < 2 || x > gridResolution - 3)
                {
                    displacement.x = 0.0f;
                }

                if (y < 2 || y > gridResolution - 3)
                {
                    displacement.y = 0.0f;
                }
                
                gridPtr[i].displacement = EncodeFixedPoint(displacement, fixedPointMultiplier);
            }
        }
        
        [BurstCompile]
        private unsafe struct GridToParticleJob : IJobParallelFor
        {
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public Cell* gridPtr;
            [NativeDisableUnsafePtrRestriction]
            public Particle* particlePtr;
            
            public uint fixedPointMultiplier;
            
            public MaterialType materialType;
            public bool useGridVolumeForLiquid;
            public int gridResolution;
            
            public void Execute(int i)
            {
                var weights = stackalloc float2[3];
                var particle = particlePtr[i];

                uint2 cellCoordinate = CalculateQuadraticInterpolationWeights(weights, particle);
                float2x2 B = float2x2(0.0f);
                float2 displacement = float2(0.0f);
                float volume = 0.0f;

                for (uint gx = 0; gx < 3; gx++)
                {
                    for (uint gy = 0; gy < 3; gy++)
                    {
                        float weight = GetWeight(weights, gx, gy);
                        uint2 neighborCellCoordinate = cellCoordinate + uint2(gx, gy) - 1;
                        
                        int cellIndex = GetCellIndex(gridResolution, neighborCellCoordinate);
                        var cell = gridPtr[cellIndex];
                        
                        float2 weightedDisplacement = weight * DecodeFixedPoint(cell.displacement, fixedPointMultiplier);
                        float2 distance = neighborCellCoordinate - particle.position + 0.5f;

                        B += OuterProduct(weightedDisplacement, distance);
                        displacement += weightedDisplacement;

                        if (useGridVolumeForLiquid)
                        {
                            volume += weight * DecodeFixedPoint(cell.volume, fixedPointMultiplier);
                        }
                    }
                }

                particle.deformationDisplacement = B * 4.0f;
                particle.displacement = displacement;

                if (useGridVolumeForLiquid)
                {
                    volume = 1.0f / max(volume, 1e-6f);

                    if (volume < 1.0f)
                    {
                        particle.liquidDensity = lerp(particle.liquidDensity, volume, 0.1f);
                    }
                }
                
                particlePtr[i] = particle;
            }
        }
        
        [BurstCompile]
        private unsafe struct IntegrateParticlesJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Particle* particlePtr;
            
            public MaterialType materialType;
            public float elasticityRatio;
            public float frictionAngle;
            public float plasticity;
            public float deltaTime;
            public float2 acceleration;
            
            public void Execute(int i)
            {
                var particle = particlePtr[i];
                
                if (materialType is MaterialType.Liquid)
                {
                    particle.liquidDensity *= Tr(particle.deformationDisplacement) + 1.0f;
                    particle.liquidDensity = max(particle.liquidDensity, 0.05f);
                }
                else
                {
                    particle.deformationGradient = (float2x2.identity + particle.deformationDisplacement) * particle.deformationGradient;
                
                    var svdResult = SingularValuesDecomposition(particle.deformationGradient);
                
                    svdResult.sigma = clamp(svdResult.sigma, 0.2f, 10_000.0f);
                    
                    if (materialType is MaterialType.Sand)
                    {
                        float sinPhi = sin(frictionAngle * TORADIANS);
                        float alpha = sqrt(2.0f / 3.0f) * 2.0f * sinPhi / (3.0f - sinPhi);
                        float beta = 0.5f;
                        
                        float2 eDiag = log(max(abs(svdResult.sigma), 1e-6f));
                        
                        float2x2 eps = Diagonal(eDiag);
                        float trace = Tr(eps) + particle.logJp;
                        
                        float2x2 eHat = eps - trace * 0.5f * float2x2.identity;
                        float frobNrm = Length(eHat);
                        
                        if (trace >= 0.0f)
                        {
                            svdResult.sigma = float2(1.0f);
                            particle.logJp = beta * trace;
                        }
                        else
                        {
                            particle.logJp = 0.0f;
                            float deltaGammaI = frobNrm + (elasticityRatio + 1.0f) * trace * alpha;
                        
                            if (deltaGammaI > 0.0f)
                            {
                                float2 h = eDiag - deltaGammaI / frobNrm * (eDiag - trace * 0.5f);
                
                                svdResult.sigma = exp(h);
                            }
                        }
                    }
                    else if (materialType is MaterialType.Visco)
                    {
                        var yieldSurface = exp(1.0f - plasticity);
                        var J = svdResult.sigma.x * svdResult.sigma.y;
                        
                        svdResult.sigma = clamp(svdResult.sigma, float2(1.0f / yieldSurface), float2(yieldSurface));
                        
                        var newJ = svdResult.sigma.x * svdResult.sigma.y;
                        svdResult.sigma *= sqrt(J / newJ);
                    }
                
                    particle.deformationGradient = mul(mul(svdResult.u, Diagonal(svdResult.sigma)), svdResult.vt);
                }

                particle.displacement += acceleration * square(deltaTime);
                particle.position += particle.displacement;
                
                particlePtr[i] = particle;
            }
        }

        [BurstCompile]
        private unsafe struct ParticleLimit : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public Particle* particlePtr;
            
            public float gridResolution;
            
            public void Execute(int i)
            {
                particlePtr[i].position = clamp(particlePtr[i].position, 1.0f, gridResolution - 2.0f);
            }
        }

        [Serializable]
        public class SolverArgs
        {
            [Min(0.0f)]
            public float SimulationUpdateRateInHz = 120.0f;
            public MaterialType MaterialType;
            [Range(3, 10)]
            public int FixedPointMultiplier = 7;
            public bool UseGridVolumeForLiquid;
            [Range(0.0f, 1.0f)]
            public float ElasticityRatio = 1.0f;
            [Range(0.0f, 10.0f)]
            public float LiquidRelaxation = 1.0f;
            [Range(0.0f, 10.0f)]
            public float ElasticRelaxation = 1.0f;
            [Range(0.0f, 1.0f)]
            public float LiquidViscosity = 1.0f;
            [Min(0.0f)]
            public float FrictionAngle = 45.0f;
            [Range(0.0f, 1.0f)]
            public float ViscoPlasticity = 1.0f;
            [Min(1)]
            public int IterationNumber = 1;
        }
        
        private readonly int m_GridResolution;
        private readonly SolverArgs m_SolverArgs;
        
        private NativeArray<Particle> m_Particles;
        private NativeArray<Cell> m_Grid;
        
        private readonly float2 m_GravitationalAcceleration;
        
        private bool m_IsDisposed;

        private uint m_FixedPointMultiplier;
        private readonly List<ICollisionSolver> m_CollisionSolvers;

        public DynamicsSolver(int gridResolution, SolverArgs args, float2 gravitationalAcceleration)
        {
            m_GridResolution = gridResolution;
            m_SolverArgs = args;
            m_GravitationalAcceleration = gravitationalAcceleration;
            
            m_Grid = new NativeArray<Cell>(gridResolution * gridResolution, Allocator.Persistent);
            m_CollisionSolvers = new List<ICollisionSolver>();
            
            ClearGrid();
        }
        
        public NativeArray<Particle> Particles => m_Particles;

        public void InstanceParticles(List<float2> positions)
        {
            CheckDisposed();
            DisposeParticles();
            
            m_Particles = new NativeArray<Particle>(positions.Count, Allocator.Persistent);
        }
        
        public void Dispose()
        {
            CheckDisposed();
            
            m_IsDisposed = true;
            
            DisposeParticles();
            DisposeGrid();
        }

        public void RegisterCollisionSolver(ICollisionSolver solver)
        {
            CheckDisposed();
            m_CollisionSolvers.Add(solver);
        }

        public void Step(float deltaTime)
        {
            CheckDisposed();

            m_FixedPointMultiplier = 1;

            for (int i = 0; i < m_SolverArgs.FixedPointMultiplier; i++)
            {
                m_FixedPointMultiplier *= 10;
            }

            var updateRateInHz = 1.0f / deltaTime;
            deltaTime *= m_SolverArgs.SimulationUpdateRateInHz / updateRateInHz;
            deltaTime /= m_SolverArgs.IterationNumber;
            
            for (int i = 0; i < m_SolverArgs.IterationNumber; i++)
            {
                ClearGrid();
                ParticleToGrid();
                GridUpdate(deltaTime, m_GravitationalAcceleration);
                GridToParticle();
                IntegrateParticles(deltaTime);
                UpdateParticles();
            }
        }

        private void DisposeParticles()
        {
            if (m_Particles.IsCreated)
            {
                m_Particles.Dispose();
            }
        }

        private void DisposeGrid()
        {
            if (m_Grid.IsCreated)
            {
                m_Grid.Dispose();
            }
        }

        private void CheckDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private void ClearGrid()
        {
            Profiler.BeginSample(nameof(ClearGrid));
            
            new JobUtility.FillJob<Cell>
            {
                array = m_Grid,
                value = new Cell
                {
                    displacement = 0,
                    mass = 0,
                    volume = 0
                }
            }.Run();
            
            Profiler.EndSample();
        }

        private unsafe void UpdateParticles()
        {
            Profiler.BeginSample(nameof(UpdateParticles));
            
            new ParticleUpdateJob
            {
                particlePtr = (Particle*)m_Particles.GetUnsafePtr(),
                materialType = m_SolverArgs.MaterialType,
                elasticityRatio = m_SolverArgs.ElasticityRatio,
                liquidRelaxation = m_SolverArgs.LiquidRelaxation,
                elasticRelaxation = m_SolverArgs.ElasticRelaxation,
                liquidViscosity = m_SolverArgs.LiquidViscosity,
            }.Schedule(m_Particles.Length, 64).Complete();
            
            Profiler.EndSample();
        }

        private unsafe void ParticleToGrid()
        {
            Profiler.BeginSample(nameof(ParticleToGrid));
            
            new ParticleToGridJob
            {
                gridPtr = (Cell*)m_Grid.GetUnsafePtr(),
                fixedPointMultiplier = m_FixedPointMultiplier,
                useGridVolumeForLiquid = m_SolverArgs.UseGridVolumeForLiquid,
                particlePtr = (Particle*)m_Particles.GetUnsafeReadOnlyPtr(),
                gridResolution = m_GridResolution
            }.Schedule(m_Particles.Length, 64).Complete();
            
            Profiler.EndSample();
        }
        
        private unsafe void GridUpdate(float deltaTime, float2 acceleration)
        {
            Profiler.BeginSample(nameof(GridUpdate));
            
            new GridUpdateJob
            {
                gridPtr = (Cell*)m_Grid.GetUnsafePtr(),
                fixedPointMultiplier = m_FixedPointMultiplier,
                materialType = m_SolverArgs.MaterialType,
                acceleration = acceleration,
                deltaTime = deltaTime
            }.Schedule(m_Grid.Length, 64).Complete();
            
            m_CollisionSolvers.ForEach(solver => solver.ResolveCollisions(m_Grid, m_FixedPointMultiplier, deltaTime));
            
            new GridLimitJob
            {
                gridPtr = (Cell*)m_Grid.GetUnsafePtr(),
                fixedPointMultiplier = m_FixedPointMultiplier,
                gridResolution = m_GridResolution
            }.Schedule(m_Grid.Length, 64).Complete();
            
            Profiler.EndSample();
        }
        
        private unsafe void GridToParticle()
        {
            Profiler.BeginSample(nameof(GridToParticle));
            
            new GridToParticleJob
            {
                gridPtr = (Cell*)m_Grid.GetUnsafeReadOnlyPtr(),
                fixedPointMultiplier = m_FixedPointMultiplier,
                useGridVolumeForLiquid = m_SolverArgs.UseGridVolumeForLiquid,
                particlePtr = (Particle*)m_Particles.GetUnsafePtr(),
                gridResolution = m_GridResolution
            }.Schedule(m_Particles.Length, 64).Complete();
            
            Profiler.EndSample();
        }
        
        private unsafe void IntegrateParticles(float deltaTime)
        {
            Profiler.BeginSample(nameof(IntegrateParticles));
            
            new IntegrateParticlesJob
            {
                particlePtr = (Particle*)m_Particles.GetUnsafePtr(),
                materialType = m_SolverArgs.MaterialType,
                elasticityRatio = m_SolverArgs.ElasticityRatio,
                frictionAngle = m_SolverArgs.FrictionAngle,
                plasticity = m_SolverArgs.ViscoPlasticity,
                deltaTime = deltaTime,
                acceleration = m_GravitationalAcceleration
            }.Schedule(m_Particles.Length, 64).Complete();
            
            m_CollisionSolvers.ForEach(solver => solver.ResolveCollisions(m_Particles, m_FixedPointMultiplier, deltaTime));
            
            new ParticleLimit
            {
                particlePtr = (Particle*)m_Particles.GetUnsafePtr(),
                gridResolution = m_GridResolution
            }.Schedule(m_Particles.Length, 64).Complete();
            
            Profiler.EndSample();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe uint2 CalculateQuadraticInterpolationWeights(float2* weights, Particle particle)
        {
            float2 cellCoordinate = floor(particle.position);
            float2 cellDifference = particle.position - cellCoordinate - 0.5f;
            
            weights[0] = 0.5f * square(0.5f - cellDifference);
            weights[1] = 0.75f - square(cellDifference);
            weights[2] = 0.5f * square(0.5f + cellDifference);

            return (uint2)cellCoordinate;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float GetWeight(float2* weights, uint gx, uint gy)
        {
            return weights[gx].x * weights[gy].y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCellIndex(int gridResolution, uint2 cellCoordinate)
        {
            return (int)cellCoordinate.x * gridResolution + (int)cellCoordinate.y;
        }
    }
}