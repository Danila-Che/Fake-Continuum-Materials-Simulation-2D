using Unity.Mathematics;

namespace Fake.Dynamics
{
    public struct Particle
    {
        public float2 position;
        public float2 velocity;
        public float2x2 affineMomentum;
        public float2x2 deformationGradient;
        public float mass;
        public float volume0;
    }
}
