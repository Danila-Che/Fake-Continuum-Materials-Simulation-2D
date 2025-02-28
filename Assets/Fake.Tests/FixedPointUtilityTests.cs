using Fake.Utilities;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Fake.Tests
{
    [TestFixture]
    public class FixedPointUtilityTests
    {
        [Test]
        public void Test_DecodeFromFixedPoint_Int()
        {
            var result = FixedPointUtility.DecodeFixedPoint(10, 10);
            
            Assert.That(result, Is.EqualTo(1.0f));
        }

        [Test]
        public void Test_EncodeToFixedPoint_Int()
        {
            var result = FixedPointUtility.EncodeFixedPoint(1.0f, 10);
            
            Assert.That(result, Is.EqualTo(10));
        }

        [Test]
        public void Test_DecodeToFixedPoint_Int2()
        {
            var result = FixedPointUtility.DecodeFixedPoint(new int2(10), 10);
            
            Assert.That(result, Is.EqualTo(new float2(1.0f)));
        }

        [Test]
        public void Test_EncodeToFixedPoint_Int2()
        {
            var result = FixedPointUtility.EncodeFixedPoint(new float2(1.0f), 10);
            
            Assert.That(result, Is.EqualTo(new int2(10)));
        }
    }
}
