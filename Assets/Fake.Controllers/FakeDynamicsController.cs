using System.Collections.Generic;
using Fake.Dynamics;
using Fake.Particle.Render;
using Unity.Mathematics;
using UnityEngine;

namespace Fake.Controllers
{
    public class FakeDynamicsController : MonoBehaviour
    {
        [SerializeField] private ParticleRenderer m_ParticleRenderer;
        
        private DynamicsSolver m_Solver;

        private void OnEnable()
        {
            m_Solver = new DynamicsSolver();

            var positions = new List<float2>
            {
                new(-1.0f, -1.0f),
                new(-1.0f, 1.0f),
                new(1.0f, -1.0f),
                new(1.0f, 1.0f)
            };
            
            m_Solver.InstanceParticles(positions);
            m_ParticleRenderer.Initialize(m_Solver.Particles.Length);
            m_ParticleRenderer.SetParticles(m_Solver.Particles);
        }

        private void OnDisable()
        {
            m_Solver.Dispose();
            m_ParticleRenderer.Dispose();
        }

        private void Update()
        {
            m_ParticleRenderer.Render();
        }
    }
}
