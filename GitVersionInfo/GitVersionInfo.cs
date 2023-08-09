using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                // Do we have a tag checked out currently?
                var tag = FindAndSortTags(Exec("tag --points-at HEAD")).FirstOrDefault();

                if (tag == null)
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
                                break;
                            }
                        }
                    }
                }

                tag ??= new SemVersion("0.0.0", 0, 0, 0, null, "", "");

            }
            catch (AbortException)
            {
            }

            return !Log.HasLoggedErrors;
        }

        private string Exec(string args)
        {
            using var process = Process.Start(new ProcessStartInfo()
            { 
                FileName = "git",
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
            });

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string stderr = process.StandardError.ReadToEnd();
                Log.LogError($"Unable to execute 'git {args}': process returned {process.ExitCode} with output '{output} {stderr}'");
                throw new AbortException();
            }

            return output;
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
