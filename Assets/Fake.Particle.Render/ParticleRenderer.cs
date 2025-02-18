using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Fake.Particle.Render
{
    public class ParticleRenderer : MonoBehaviour
    {
        private struct Particle
        {
            public float2 position;
        }

        private const int k_CommandCount = 1;

        [SerializeField] private Material m_Material;
        [SerializeField] private Mesh m_Mesh;

        private GraphicsBuffer m_CommandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] m_CommandData;
        private RenderParams m_RenderParams;

        private ComputeBuffer m_ParticleBuffer;
        private NativeArray<Particle> m_Particles;

        private void OnEnable()
        {
            m_CommandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, k_CommandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            m_CommandData = new GraphicsBuffer.IndirectDrawIndexedArgs[k_CommandCount];

            m_RenderParams = new RenderParams(m_Material)
            {
                worldBounds = new Bounds(Vector3.zero, 100 * Vector3.one),
                matProps = new MaterialPropertyBlock()
            };

            m_CommandData[0].indexCountPerInstance = m_Mesh.GetIndexCount(0);
            m_CommandData[0].instanceCount = 2;
            m_CommandBuffer.SetData(m_CommandData);

            m_ParticleBuffer = new ComputeBuffer(2, 8, ComputeBufferType.Default);
            m_Particles = new NativeArray<Particle>(2, Allocator.Persistent);
            m_Particles[0] = new Particle
            {
                position = new float2(-1.0f, 0.0f),
            };

            m_Particles[1] = new Particle
            {
                position = new float2(1.0f, 0.0f),
            };

            m_ParticleBuffer.SetData(m_Particles);

            m_Material.SetBuffer("particle_buffer", m_ParticleBuffer);
        }

        private void OnDisable()
        {
            m_CommandBuffer?.Release();
            m_CommandBuffer = null;

            m_ParticleBuffer?.Release();
            m_ParticleBuffer = null;

            if (m_Particles.IsCreated)
            {
                m_Particles.Dispose();
                m_Particles = default;
            }
        }

        private void Update()
        {
            Graphics.RenderMeshIndirect(m_RenderParams, m_Mesh, m_CommandBuffer, k_CommandCount);
        }
    }
}
