using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using static Fake.Utilities.MatrixUtility;

namespace Fake.Tests
{
    public class MatrixUtilityTests
    {
        [Test]
        public void Test_SVD_IdentityMatrix()
        {
            var svdResult = SingularValuesDecomposition(new float2x2(1, 0, 0, 1));

            Assert.That(svdResult.u, Is.EqualTo(new float2x2(1, 0, 0, 1)));
            Assert.That(Diagonal(svdResult.sigma), Is.EqualTo(new float2x2(1, 0, 0, 1)));
            Assert.That(svdResult.vt, Is.EqualTo(new float2x2(1, 0, 0, 1)));
        }

        [Test]
        public void Test_SVD()
        {
            var original = float2x2(3, 1, 1, 3);
            var svdResult = SingularValuesDecomposition(original);
            
            var m = mul(mul(svdResult.u, Diagonal(svdResult.sigma)), svdResult.vt);

            Debug.Log(m);
            
            Assert.That(m[0][0], Is.EqualTo(original[0][0]).Within(1e-6f));
            Assert.That(m[0][1], Is.EqualTo(original[0][1]).Within(1e-6f));
            Assert.That(m[1][0], Is.EqualTo(original[1][0]).Within(1e-6f));
            Assert.That(m[1][1], Is.EqualTo(original[1][1]).Within(1e-6f));
        }

        [Test]
        public void Test_Matrix()
        {
            var m = new float2x2(1.0f, 2.0f, 3.0f, 4.0f);

            Debug.Log(m.c0);
            Debug.Log(m.c1);

            Assert.Pass();
        }

        [Test]
        public void Test_Hypot()
        {
            var m = new float2x2(1.0f, 2.0f, 3.0f, 4.0f);

            var len1 = length(new float2(m[0][0], m[1][1]));

            Debug.Log(len1);

            var len2 = 0.0f;

            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    float val = m[row][col];
                    len2 += val * val;
                }
            }

            len2 = sqrt(len2);

            Debug.Log(len2);
            
            Assert.Pass();
        }
    }
}
