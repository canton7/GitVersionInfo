using Microsoft.Build.Framework;
using NSubstitute;
using NUnit.Framework;
using System.ComponentModel;
using System.Diagnostics;
using System;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Threading;

namespace GitVersionInfo.Tests
{
    [TestFixture]
    public class GitTests
    {
        private string testDir;
        private GitVersionInfo task;

        public GitTests()
        {
            testDir = Path.Combine(Path.GetDirectoryName(typeof(GitTests).Assembly.Location), "TestRepository");
        }

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(testDir);
            Directory.SetCurrentDirectory(testDir);

            Exec("init .");
            Exec("branch -m main");
            Exec("commit --allow-empty -m \"Initial Commit\"");

            var host = Substitute.For<IBuildEngine>();
            task = new GitVersionInfo();
            task.BuildEngine = host;
            task.ReleaseBranch = "main";
        }

        [TearDown]
        public void TearDown()
        {
            Directory.SetCurrentDirectory(Path.Combine(testDir, ".."));

            // This can take a few attempts...
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(testDir, true);
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        [Test]
        public void InitialCommitNoTags()
        {
            string commit = Exec("rev-parse HEAD");
            
            task.Execute();
            var version = task.Version;

            Assert.AreEqual("0", version.GetMetadata("TagMajor"));
            Assert.AreEqual("0", version.GetMetadata("TagMinor"));
            Assert.AreEqual("0", version.GetMetadata("TagPatch"));
            Assert.AreEqual("", version.GetMetadata("TagRevision"));
            Assert.AreEqual("", version.GetMetadata("TagPrerelease"));
            Assert.AreEqual("", version.GetMetadata("TagBuildMetadata"));
            Assert.AreEqual("false", version.GetMetadata("IsTagged"));
            Assert.AreEqual("false", version.GetMetadata("IsTagged"));
            Assert.AreEqual("", version.GetMetadata("CommitsSinceTag"));
            Assert.AreEqual("", version.GetMetadata("CommitsSinceTagFirstParent"));
            Assert.AreEqual("false", version.GetMetadata("IsDirty"));
            Assert.AreEqual(commit, version.GetMetadata("FullSha"));
            Assert.That(commit, Does.StartWith(version.GetMetadata("ShortSha")));
        }

        private string Exec(string args)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo()
                {
                    FileName = "git.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                string output = process.StandardOutput.ReadToEnd().TrimEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string stderr = process.StandardError.ReadToEnd();
                    Assert.Fail($"Unable to execute 'git {args}': process returned {process.ExitCode} with output '{stderr}'");
                }

                return output;
            }
            catch (Win32Exception ex) when (ex.HResult == -2147467259)
            {
                Assert.Fail("Unable to find git.exe. Please ensure it is in your PATH");
            }

            return null;
        }
    }
}