using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Fake.Particle.Render
{
    [Serializable]
    public struct ParticleRenderer : IDisposable
    {
        private static readonly int ParticleBufferProperty = Shader.PropertyToID("particle_buffer");
        
        [SerializeField] private Material m_Material;
        [SerializeField] private Mesh m_Mesh;

        private GraphicsBuffer m_CommandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] m_CommandData;
        private RenderParams m_RenderParams;

        private ComputeBuffer m_ParticleBuffer;
        
        private bool m_IsDisposed;

        public void Initialize(int bufferSize)
        {
            CheckDisposed();
            
            m_RenderParams = new RenderParams(m_Material)
            {
                worldBounds = new Bounds(Vector3.zero, 100 * Vector3.one),
                matProps = new MaterialPropertyBlock()
            };
            
            m_CommandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            m_CommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            m_CommandData[0].indexCountPerInstance = m_Mesh.GetIndexCount(0);
            m_CommandData[0].instanceCount = (uint)bufferSize;
            m_CommandBuffer.SetData(m_CommandData);

            m_ParticleBuffer = new ComputeBuffer(bufferSize, UnsafeUtility.SizeOf(typeof(Dynamics.Particle)), ComputeBufferType.Default);
            m_Material.SetBuffer(ParticleBufferProperty, m_ParticleBuffer);
        }

        public void SetParticles(NativeArray<Dynamics.Particle> particles)
        {
            CheckDisposed();
            m_ParticleBuffer.SetData(particles);
        }

        public void Render()
        {
            CheckDisposed();
            Graphics.RenderMeshIndirect(m_RenderParams, m_Mesh, m_CommandBuffer);
        }

        public void Dispose()
        {
            m_IsDisposed = true;
            
            m_CommandBuffer?.Release();
            m_CommandBuffer = null;

            m_ParticleBuffer?.Release();
            m_ParticleBuffer = null;
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
