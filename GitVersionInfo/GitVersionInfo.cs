using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;

namespace GitVersionInfo
{
    public class GitVersionInfo : Task
    {
        [Required]
        public string ReleaseBranch { get; set; }

        [Output]
        public TaskItem Version { get; set; }

        public override bool Execute()
        {
            try
            {
                string isTagged = "false";
                string commitsSinceTagFirstParent = null;
                string commitsSinceTag = null;

                string isDirty = ExecGetStatusCode("diff-index --quiet HEAD") > 0 ? "true" : "false";

                string fullSha = Exec("rev-parse HEAD");
                string shortSha = Exec("rev-parse --short HEAD");

                // Do we have a tag checked out currently?
                var tag = FindAndSortTags(Exec("tag --points-at HEAD")).FirstOrDefault();
                if (tag != null)
                {
                    isTagged = "true";
                    commitsSinceTagFirstParent = "0";
                    commitsSinceTag = "0";
                }
                else if (!string.IsNullOrWhiteSpace(ReleaseBranch))
                {
                    // Walk backwards through the commits on the release branch, looking for one where there exists a common
                    // ancestor between that tag and HEAD
                    string head = Exec("rev-parse HEAD");
                    using var releaseCommitReader = new StringReader(Exec($"rev-list --first-parent \"{ReleaseBranch}\""));
                    while (releaseCommitReader.ReadLine() is { } commit)
                    {
                        string mergeBase = Exec($"merge-base \"{commit}\" \"{head}\"");
                        if (mergeBase != head)
                        {
                            tag = FindAndSortTags(Exec($"tag --points-at \"{commit}\"")).FirstOrDefault();
                            if (tag != null)
                            {
                                commitsSinceTagFirstParent = Exec($"rev-list --count --first-parent \"{mergeBase}\"..\"{head}\"");
                                commitsSinceTag = Exec($"rev-list --count \"{mergeBase}\"..\"{head}\"");
                                break;
                            }
                        }
                    }
                }

                tag ??= new SemVersion("0.0.0", 0, 0, 0, null, "", "");

                Version = new TaskItem(tag.Tag, new Dictionary<string, object>()
                {
                    { "TagMajor", tag.Major.ToString() },
                    { "TagMinor", tag.Minor.ToString() },
                    { "TagPatch", tag.Patch.ToString() },
                    { "TagRevision", tag.Revision?.ToString() ?? "" },
                    { "TagPrerelease", tag.Prerelease },
                    { "TagBuildMetadata", tag.BuildMetadata },
                    { "IsTagged", isTagged },
                    { "CommitsSinceTag", commitsSinceTag },
                    { "CommitsSinceTagFirstParent",  commitsSinceTagFirstParent },
                    { "IsDirty", isDirty },
                    { "FullSha", fullSha },
                    { "ShortSha", shortSha },
                });
            }
            catch (AbortException)
            {
                // An error has already been logged
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
            }

            return !Log.HasLoggedErrors;
        }

        private string Exec(string args)
        {
            return ExecGit(args, process =>
            {
                string output = process.StandardOutput.ReadToEnd().TrimEnd();
                if (process.ExitCode != 0)
                {
                    string stderr = process.StandardError.ReadToEnd();
                    Log.LogError($"Unable to execute 'git {args}': process returned {process.ExitCode} with output '{output} {stderr}'");
                    throw new AbortException();
                }

                return output;
            });
        }

        private int ExecGetStatusCode(string args)
        {
            return ExecGit(args, process => process.ExitCode);
        }

        private T ExecGit<T>(string args, Func<Process, T> handler)
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

                process.WaitForExit();

                var result = handler(process);

                return result;
            }
            catch (Win32Exception ex) when (ex.HResult == -2147467259)
            {
                Log.LogError("Unable to find git.exe. Please ensure it is in your PATH");
                throw new AbortException();
            }
        }

        private List<SemVersion> FindAndSortTags(string inputs)
        {
            var tags = new List<SemVersion>();

            using var reader = new StringReader(inputs);
            while (reader.ReadLine() is { } line)
            {
                if (SemVersion.TryParse(line, out var version))
                {
                    tags.Add(version);
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"Ignoring tag {line} as it is not a valid Semver 2.0 version");
                }
            }

            // Sort descending
            tags.Sort((x, y) => y.CompareTo(x));
            return tags;
        }
    }

    internal class AbortException : Exception { }
}
