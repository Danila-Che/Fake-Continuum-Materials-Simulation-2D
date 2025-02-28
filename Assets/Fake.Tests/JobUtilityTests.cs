using Fake.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Fake.Tests
{
    public class JobUtilityTests
    {
        [Test]
        public void Test_FillNativeArrayWithValue()
        {
            using var array = new NativeArray<int>(1, Allocator.TempJob);

            new JobUtility.FillJob<int>
            {
                array = array,
                value = 1
            }.Run();
            
            Assert.That(array[0], Is.EqualTo(1));
        }
    }
}
