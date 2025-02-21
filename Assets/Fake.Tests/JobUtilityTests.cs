using Fake.Utilities;
using NUnit.Framework;
using Unity.Collections;

namespace Fake.Tests
{
    public class JobUtilityTests
    {
        [Test]
        public void Test_FillNativeArrayWithValue()
        {
            using var array = new NativeArray<int>(1, Allocator.TempJob); 
            JobUtility.Fill(array, 1);
            
            Assert.That(array[0], Is.EqualTo(1));
        }
    }
}
