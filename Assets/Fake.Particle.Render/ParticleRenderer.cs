using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Fake.Particle.Render
{
    [Serializable]
    public class ParticleRenderer : IDisposable
    {
        private static readonly int ParticleBufferProperty = Shader.PropertyToID("_ParticleBuffer");
        private static readonly int GridHalfSizeProperty = Shader.PropertyToID("_GridHalfSize");
        private static readonly int BoxSizeProperty = Shader.PropertyToID("_BoxSize");

        [SerializeField] private Material m_Material;
        [SerializeField] private Mesh m_Mesh;
        [SerializeField] private float m_BoxSize;

        private GraphicsBuffer m_CommandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] m_CommandData;
        private RenderParams m_RenderParams;

        private ComputeBuffer m_ParticleBuffer;
        
        private bool m_IsDisposed;

        public void Initialize(int bufferSize, int2 gridResolution)
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

            m_ParticleBuffer = new ComputeBuffer(bufferSize, UnsafeUtility.SizeOf<Dynamics.Particle>(), ComputeBufferType.Default);
            m_Material.SetBuffer(ParticleBufferProperty, m_ParticleBuffer);
            m_Material.SetVector(GridHalfSizeProperty, (Vector2)(0.5f * (float2)gridResolution));
            m_Material.SetFloat(BoxSizeProperty, m_BoxSize);
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
            CheckDisposed();
            
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
