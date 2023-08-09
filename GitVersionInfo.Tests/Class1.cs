using Microsoft.Build.Framework;
using NSubstitute;
using NUnit.Framework;

namespace GitVersionInfo.Tests
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void Test()
        {
            var host = Substitute.For<IBuildEngine>();
            var task = new GitVersionInfo();
            task.BuildEngine = host;
            task.Execute();
        }
    }
}