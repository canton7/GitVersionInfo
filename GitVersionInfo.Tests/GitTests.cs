using Microsoft.Build.Framework;
using NSubstitute;
using NUnit.Framework;
using System.ComponentModel;
using System.Diagnostics;
using System;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Threading;
using Microsoft.Build.Utilities;
using NUnit.Framework.Constraints;
using System.Collections.Generic;

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
            Exec("branch develop");

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
            var version = RunTask();

            AssertVersion(version, isTagged: false, commitsSinceTag: 1, commitsSinceTagFirstParent: 1);
            Assert.AreEqual(commit, version.GetMetadata("FullSha"));
            Assert.That(commit, Does.StartWith(version.GetMetadata("ShortSha")));
        }

        [Test]
        public void TaggedValidCommitOnMain()
        {
            Exec("tag -a v1.2.3 -m \"Tag\"");

            var version = RunTask();
            AssertVersion(version, 1, 2, 3);
        }

        [Test]
        public void CommitAfterTaggedCommitOnMain()
        {
            Exec("commit --allow-empty -m \"Second\"");
            Exec("tag -a v2.3.4 -m \"Tag\"");
            Exec("commit --allow-empty -m \"Third\"");

            var version = RunTask();
            AssertVersion(version, 2, 3, 4, isTagged: false, commitsSinceTag: 1, commitsSinceTagFirstParent: 1);
        }

        [Test]
        public void CommitOnDevelopBeforeTaggedVersion()
        {
            Exec("checkout develop");
            Exec("commit --allow-empty -m \"Of interest\"");
            Exec("checkout main");
            Exec("merge --no-ff develop");
            Exec("tag -a v3.4.5 -m \"Tag\"");
            Exec("checkout develop");

            var version = RunTask();
            AssertVersion(version, 0, 0, 0, isTagged: false, commitsSinceTag: 2, commitsSinceTagFirstParent: 2, branch: "develop");
        }

        private void AssertVersion(
            TaskItem version,
            int major = 0,
            int minor = 0,
            int patch = 0,
            int? revision = null,
            string prerelease = "",
            string buildMetadata = "",
            bool isTagged = true,
            int? commitsSinceTag = 0,
            int? commitsSinceTagFirstParent = 0,
            string branch = "main",
            bool isDirty = false)
        {
            Assert.Multiple(() =>
            {
                Assert.AreEqual(major.ToString(), version.GetMetadata("TagMajor"));
                Assert.AreEqual(minor.ToString(), version.GetMetadata("TagMinor"));
                Assert.AreEqual(patch.ToString(), version.GetMetadata("TagPatch"));
                Assert.AreEqual(revision?.ToString() ?? "", version.GetMetadata("TagRevision"));
                Assert.AreEqual(prerelease, version.GetMetadata("TagPrerelease"));
                Assert.AreEqual(buildMetadata, version.GetMetadata("TagBuildMetadata"));
                Assert.AreEqual(isTagged ? "true" : "false", version.GetMetadata("IsTagged"));
                Assert.AreEqual(commitsSinceTag.ToString(), version.GetMetadata("CommitsSinceTag"));
                Assert.AreEqual(commitsSinceTagFirstParent.ToString(), version.GetMetadata("CommitsSinceTagFirstParent"));
                Assert.AreEqual(branch, version.GetMetadata("Branch"));
                Assert.AreEqual(isDirty ? "true" : "false", version.GetMetadata("IsDirty"));
            });
        }

        private TaskItem RunTask()
        {
            bool success = task.Execute();
            // TODO: Check messages
            Assert.True(success);
            return task.Version;
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