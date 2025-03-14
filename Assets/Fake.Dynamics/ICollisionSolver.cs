using Unity.Collections;

namespace Fake.Dynamics
{
    public interface ICollisionSolver
    {
        void ResolveCollisions(NativeArray<Cell> grid, uint fixedPointMultiplier, float deltaTime);
        
        void ResolveCollisions(NativeArray<Particle> particles, uint fixedPointMultiplier, float deltaTime);
    }
}
