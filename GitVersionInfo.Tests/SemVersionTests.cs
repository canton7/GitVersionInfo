using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitVersionInfo.Tests
{
    [TestFixture]
    public class SemVersionTests
    {
        // Example: 1.0.0 < 2.0.0 < 2.1.0 < 2.1.1.
        [TestCase("1.0.0", "1.0.0", 0)]
        [TestCase("1.0.0", "2.0.0", -1)]
        [TestCase("2.0.0", "2.1.0", -1)]
        [TestCase("2.1.0", "2.1.1", -1)]

        // Our own extension
        [TestCase("1.0.0.3", "1.0.0.3", 0)]

        [TestCase("1.0.0", "1.0.0.0", -1)]
        [TestCase("1.0.0.0", "1.0.0.1", -1)]

        // Example: 1.0.0-alpha < 1.0.0.
        [TestCase("1.0.0-alpha", "1.0.0-alpha", 0)]

        [TestCase("1.0.0-alpha", "1.0.0", -1)]

        // Example: 1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta < 1.0.0-beta.2 < 1.0.0-beta.11 < 1.0.0-rc.1 < 1.0.0
        [TestCase("1.0.0-alpha.1", "1.0.0-alpha.1", 0)]
        [TestCase("1.0.0-alpha.beta", "1.0.0-alpha.beta", 0)]
        [TestCase("1.0.0-beta", "1.0.0-beta", 0)]
        [TestCase("1.0.0-beta.2", "1.0.0-beta.2", 0)]
        [TestCase("1.0.0-beta.11", "1.0.0-beta.11", 0)]
        [TestCase("1.0.0-rc.1", "1.0.0-rc.1", 0)]

        [TestCase("1.0.0-alpha.beta", "1.0.0-alpha.beta", 0)]
        [TestCase("1.0.0-alpha", "1.0.0-alpha.1", -1)]
        [TestCase("1.0.0-alpha.1", "1.0.0-alpha.beta", -1)]
        [TestCase("1.0.0-alpha.beta", "1.0.0-beta", -1)]
        [TestCase("1.0.0-beta", "1.0.0-beta.2", -1)]
        [TestCase("1.0.0-beta", "1.0.0-beta.2", -1)]
        [TestCase("1.0.0-beta.2", "1.0.0-beta.11", -1)]
        [TestCase("1.0.0-beta.11", "1.0.0-rc.1", -1)]
        [TestCase("1.0.0-rc.1", "1.0.0", -1)]
        public void ComparesCorrectly(string a, string b, int result)
        {
            Assert.NotNull(SemVersion.TryParse(a, out var versionA));
            Assert.NotNull(SemVersion.TryParse(b, out var versionB));

            Assert.AreEqual(result, versionA.CompareTo(versionB));
            Assert.AreEqual(-result, versionB.CompareTo(versionA));
        }
    }
}
