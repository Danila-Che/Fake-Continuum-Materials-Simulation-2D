using System.Collections.Generic;
using Fake.Dynamics;
using Fake.Particle.Render;
using Unity.Mathematics;
using UnityEngine;

namespace Fake.Controllers
{
    public class FakeDynamicsController : MonoBehaviour
    {
        private const int k_GridResolution = 64;
        
        [SerializeField] private ParticleRenderer m_ParticleRenderer;
        [SerializeField] private float2 m_GravitationalAcceleration = (Vector2)Physics.gravity;
        [SerializeField] private DynamicsSolver.SolverArgs m_SolverArgs;

        private DynamicsSolver m_Solver;

        private void OnEnable()
        {
            m_Solver = new DynamicsSolver(k_GridResolution, m_SolverArgs, m_GravitationalAcceleration);

            CreateParticles();
            
            m_ParticleRenderer.Initialize(m_Solver.Particles.Length, new int2(k_GridResolution));
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

        private void FixedUpdate()
        {
            m_Solver.Step(Time.fixedDeltaTime);
            m_ParticleRenderer.SetParticles(m_Solver.Particles);
        }

        private void CreateParticles()
        {
            var positions = new List<float2>();

            var spacing = 0.5f;
            var boxSize = new int2(16, 16);
            var center = 0.5f * new float2(k_GridResolution);
            
            for (float y = center.y - 0.5f * boxSize.y; y < center.y + 0.5f * boxSize.y; y += spacing)
            {
                for (float x = center.x - 0.5f * boxSize.x; x < center.x + 0.5f * boxSize.x; x += spacing)
                {
                    positions.Add(new float2(x, y));
                }
            }
            
            m_Solver.InstanceParticles(positions);
            
            var particles = m_Solver.Particles;

            for (int i = 0; i < m_Solver.Particles.Length; i++)
            {
                var particle = new Dynamics.Particle
                {
                    position = positions[i],
                    velocity = new float2(0.0f),
                    mass = 0.05f,
                    deformationGradient = new float2x2(
                        1, 0,
                        0, 1)
                };

                particles[i] = particle;
            }
            
            m_Solver.CalculateParticleVolumes();
        }
    }
}
