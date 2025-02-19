using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Fake.Dynamics
{
    public struct DynamicsSolver : IDisposable
    {
        private NativeArray<Particle> m_Particles;
        private bool m_IsDisposed;
        
        public NativeArray<Particle> Particles => m_Particles;

        public void InstanceParticles(List<float2> positions)
        {
            CheckDisposed();
            DisposeParticles();
            
            m_Particles = new NativeArray<Particle>(positions.Count, Allocator.Persistent);

            for (int i = 0; i < positions.Count; i++)
            {
                m_Particles[i] = new Particle
                {
                    position = positions[i]
                };
            }
        }
        
        public void Dispose()
        {
            m_IsDisposed = true;
            
            DisposeParticles();
        }

        private void DisposeParticles()
        {
            if (m_Particles.IsCreated)
            {
                m_Particles.Dispose();
            }
        }

        private void CheckDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}