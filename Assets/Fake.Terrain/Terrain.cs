namespace Fake.Terrain
{
    public class Terrain
    {
        private readonly float[] m_Vertices; 
        private readonly int m_Resolution;

        public Terrain(int resolution)
        {
            m_Resolution = resolution;
            m_Vertices = new float[m_Resolution];
        }
        
        public int Resolution => m_Resolution;

        public float GetHeight(int index)
        {
            return m_Vertices[index];
        }
        
        public void SetHeight(int index, float height)
        {
            m_Vertices[index] = height;
        }
    }
}
