using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Fake.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Fake.Dynamics
{
    public class DynamicsSolver : IDisposable
    {
        [BurstCompile]
        private struct ParticleToGridJob : IJob
        {
            [WriteOnly] public NativeArray<Cell> grid;
            [ReadOnly] public NativeArray<Particle> particles;
        
            public int gridResolution;
            public float elasticMu;
            public float elasticLambda;
            public float deltaTime;
            
            public unsafe void Execute()
            {
                var weights = stackalloc float2[3];
                
                var length = particles.Length;
                var particlesPtr = (Particle*)particles.GetUnsafeReadOnlyPtr();
                var gridPtr = (Cell*)grid.GetUnsafePtr();
        
                for (int i = 0; i < length; i++)
                {
                    var particle = particlesPtr[i];
        
                    var F = particle.deformationGradient;
                    var J = math.determinant(F);
                    var volume = particle.volume0 * J;
        
                    var FT = math.transpose(F);
                    var FinvT = math.inverse(FT);
                    var FminusFinvT = F - FinvT;
                    
                    var Ptemp0 = elasticMu * FminusFinvT;
                    var Ptemp1 = elasticLambda * math.log(J) * FinvT;
                    var P = Ptemp0 + Ptemp1;
        
                    float2x2 stress = (1.0f / J) * math.mul(P, FT);
        
                    var eq16term0 = -volume * 4 * stress * deltaTime;
                    
                    uint2 cellCoordinate = (uint2)particle.position;
                    CalculateQuadraticInterpolationWeights(weights, particle, cellCoordinate);
        
                    for (uint gx = 0; gx < 3; gx++)
                    {
                        for (uint gy = 0; gy < 3; gy++)
                        {
                            float weight = GetWeight(weights, gx, gy);
                            
                            var neighborCellCoordinate = new uint2(cellCoordinate.x + gx - 1, cellCoordinate.y + gy - 1);
                            float2 cellDistance = neighborCellCoordinate - particle.position + 0.5f;
                            float2 q = math.mul(particle.affineMomentum, cellDistance);
                            
                            int cellIndex = GetCellIndex(gridResolution, neighborCellCoordinate);
                            var cell = gridPtr[cellIndex];
                            
                            float massContribution = weight * particle.mass;
                            cell.mass += massContribution;
                            cell.velocity += massContribution * (particle.velocity + q); // momentum
        
                            float2 momentum = math.mul(eq16term0 * weight, cellDistance);
                            cell.velocity += momentum;
                            
                            gridPtr[cellIndex] = cell;
                        }
                    }
                }
            }
        }

        [BurstCompile]
        private struct GridUpdateJob : IJob
        {
            public NativeArray<Cell> grid;
            
            public int gridResolution;
            public float2 acceleration;
            public float deltaTime;
            
            public unsafe void Execute()
            {
                var length = grid.Length;
                var gridPtr = (Cell*)grid.GetUnsafePtr();
                
                for (int i = 0; i < length; i++)
                {
                    var cell = gridPtr[i];

                    if (cell.mass > 0.0f)
                    {
                        cell.velocity /= cell.mass;
                        cell.velocity += acceleration * deltaTime;

                        int x = i / gridResolution;
                        int y = i - x * gridResolution;

                        if (x < 2 || x > gridResolution - 3)
                        {
                            cell.velocity.x = 0.0f;
                        }

                        if (y < 2 || y > gridResolution - 3)
                        {
                            cell.velocity.y = 0.0f;
                        }

                        grid[i] = cell;
                    }
                }
            }
        }

        [BurstCompile]
        private struct GridToParticleJob : IJob
        {
            [ReadOnly]
            public NativeArray<Cell> grid;
            [WriteOnly]
            public NativeArray<Particle> particles;
            
            public int gridResolution;
            public float deltaTime;
            
            public unsafe void Execute()
            {
                var weights = stackalloc float2[3];
                
                var length = particles.Length;
                var particlesPtr = (Particle*)particles.GetUnsafePtr();
                var gridPtr = (Cell*)grid.GetUnsafeReadOnlyPtr();
                
                for (int i = 0; i < length; i++)
                {
                    var particle = particlesPtr[i];

                    particle.velocity = new float2(0.0f);
                    
                    uint2 cellCoordinate = (uint2)particle.position;
                    CalculateQuadraticInterpolationWeights(weights, particle, cellCoordinate);

                    float2x2 B = new float2x2(0.0f);

                    for (uint gx = 0; gx < 3; gx++)
                    {
                        for (uint gy = 0; gy < 3; gy++)
                        {
                            float weight = GetWeight(weights, gx, gy);
                            
                            var neighborCellCoordinate = new uint2(cellCoordinate.x + gx - 1, cellCoordinate.y + gy - 1);
                            
                            int cellIndex = GetCellIndex(gridResolution, neighborCellCoordinate);
                            float2 distance = neighborCellCoordinate - particle.position + 0.5f;
                            float2 weightedVelocity = gridPtr[cellIndex].velocity * weight;

                            var term = new float2x2(weightedVelocity * distance.x, weightedVelocity * distance.y);

                            B += term;
                            
                            particle.velocity += weightedVelocity;
                        }
                    }

                    particle.affineMomentum = B * 4;

                    var FNew = math.float2x2(
                        1, 0,
                        0, 1);
                    
                    FNew += deltaTime * particle.affineMomentum;
                    
                    particle.deformationGradient = math.mul(FNew, particle.deformationGradient);
                    
                    particlesPtr[i] = particle;
                }
            }
        }
        
        [BurstCompile]
        private struct IntegrateParticlesJob : IJob
        {
            public NativeArray<Particle> particles;

            public float gridResolution;
            public float deltaTime;
            
            public unsafe void Execute()
            {
                var length = particles.Length;
                var particlesPtr = (Particle*)particles.GetUnsafePtr();
                
                for (int i = 0; i < length; i++)
                {
                    var particle = particlesPtr[i];

                    particle.position += particle.velocity * deltaTime;
                    particle.position = math.clamp(particle.position, new float2(1.0f), new float2(gridResolution - 2.0f));
                    
                    particlesPtr[i] = particle;
                }
            }
        }

        [Serializable]
        public class SolverArgs
        {
            [Min(0.0f)]
            public float ElasticLambda = 10.0f;
            [Min(0.0f)]
            public float ElasticMu = 20.0f;
            [Min(1)]
            public int IterationNumber = 1;
        }
        
        private readonly int m_GridResolution;
        private readonly SolverArgs m_SolverArgs;
        
        private NativeArray<Particle> m_Particles;
        private NativeArray<Cell> m_Grid;
        
        private readonly float2[] m_Weights = new float2[3];
        private readonly float2 m_GravitationalAcceleration;
        
        private bool m_IsDisposed;

        public DynamicsSolver(int gridResolution, SolverArgs args, float2 gravitationalAcceleration)
        {
            m_GridResolution = gridResolution;
            m_SolverArgs = args;
            m_GravitationalAcceleration = gravitationalAcceleration;
            
            m_Grid = new NativeArray<Cell>(gridResolution * gridResolution, Allocator.Persistent);
            
            ClearGrid();
        }
        
        public NativeArray<Particle> Particles => m_Particles;

        public void InstanceParticles(List<float2> positions)
        {
            CheckDisposed();
            DisposeParticles();
            
            m_Particles = new NativeArray<Particle>(positions.Count, Allocator.Persistent);
        }
        
        public void CalculateParticleVolumes()
        {
            ParticleToGrid(0.0f);

            for (int i = 0; i < m_Particles.Length; i++)
            {
                var particle = m_Particles[i];
                
                uint2 cellCoordinate = (uint2)particle.position;
                CalculateQuadraticInterpolationWeights(particle, cellCoordinate);

                float density = 0.0f;

                for (uint gx = 0; gx < 3; gx++)
                {
                    for (uint gy = 0; gy < 3; gy++)
                    {
                        float weight = GetWeight(gx, gy);
                        var neighborCellCoordinate = new uint2(cellCoordinate.x + gx - 1, cellCoordinate.y + gy - 1);
                        int cellIndex = GetCellIndex(neighborCellCoordinate);
                        
                        density += m_Grid[cellIndex].mass * weight;
                    }
                }

                particle.volume0 = particle.mass / density;
                
                m_Particles[i] = particle;
            }
        }
        
        public void Dispose()
        {
            CheckDisposed();
            
            m_IsDisposed = true;
            
            DisposeParticles();
            DisposeGrid();
        }

        public void Step(float deltaTime)
        {
            CheckDisposed();

            deltaTime /= m_SolverArgs.IterationNumber;
            
            for (int i = 0; i < m_SolverArgs.IterationNumber; i++)
            {
                ClearGrid();
                ParticleToGrid(deltaTime);
                GridUpdate(deltaTime, m_GravitationalAcceleration);
                GridToParticle(deltaTime);
                IntegrateParticles(deltaTime);
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
            JobUtility.Fill(m_Grid, new Cell
            {
                velocity = new float2(0.0f),
                mass = 0.0f
            });
        }

        private void ParticleToGrid(float deltaTime)
        {
            new ParticleToGridJob
            {
                grid = m_Grid,
                particles = m_Particles,
                elasticMu = m_SolverArgs.ElasticMu,
                elasticLambda = m_SolverArgs.ElasticLambda,
                deltaTime = deltaTime,
                gridResolution = m_GridResolution
            }.Run();
        }

        private void GridUpdate(float deltaTime, float2 acceleration)
        {
            new GridUpdateJob
            {
                grid = m_Grid,
                gridResolution = m_GridResolution,
                acceleration = acceleration,
                deltaTime = deltaTime
            }.Run();
        }

        private void GridToParticle(float deltaTime)
        {
            new GridToParticleJob
            {
                grid = m_Grid,
                particles = m_Particles,
                gridResolution = m_GridResolution,
                deltaTime = deltaTime
            }.Run();
        }

        private void IntegrateParticles(float deltaTime)
        {
            new IntegrateParticlesJob
            {
                particles = m_Particles,
                gridResolution = m_GridResolution,
                deltaTime = deltaTime
            }.Run();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void CalculateQuadraticInterpolationWeights(Particle particle, uint2 cellCoordinate)
        {
            fixed (float2* ptr = m_Weights)
            {
                CalculateQuadraticInterpolationWeights(ptr, particle, cellCoordinate);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void CalculateQuadraticInterpolationWeights(float2* weights, Particle particle, uint2 cellCoordinate)
        {
            float2 cellDifference = particle.position - cellCoordinate - 0.5f;
            
            weights[0] = 0.5f * math.square(0.5f - cellDifference);
            weights[1] = 0.75f - math.square(cellDifference);
            weights[2] = 0.5f * math.square(0.5f + cellDifference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe float GetWeight(uint gx, uint gy)
        {
            fixed (float2* ptr = m_Weights)
            {
                return GetWeight(ptr, gx, gy);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float GetWeight(float2* weights, uint gx, uint gy)
        {
            return weights[gx].x * weights[gy].y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCellIndex(uint2 cellCoordinate)
        {
            return GetCellIndex(m_GridResolution, cellCoordinate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetCellIndex(int gridResolution, uint2 cellCoordinate)
        {
            return (int)cellCoordinate.x * gridResolution + (int)cellCoordinate.y;
        }
    }
}