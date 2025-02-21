using Fake.Utilities;
using NUnit.Framework;

namespace Fake.Tests
{
    [TestFixture]
    public class FixedPointUtilityTests
    {
        [Test]
        public void Test_DecodeFromFixedPoint()
        {
            var result = FixedPointUtility.DecodeFixedPoint(10, 10);
            
            Assert.That(result, Is.EqualTo(1.0f));
        }

        [Test]
        public void Test_EncodeToFixedPoint()
        {
            var result = FixedPointUtility.EncodeFixedPoint(1.0f, 10);
            
            Assert.That(result, Is.EqualTo(10));
        }
    }
}
