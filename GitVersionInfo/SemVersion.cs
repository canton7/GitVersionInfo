using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GitVersionInfo
{
    public class SemVersion : IComparable<SemVersion>
    {
        // https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
        // (With added revision)
        private static readonly Regex versionRegex = new(@"^[vV]?(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\.(?<revision>0|[1-9]\d*))?(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");

        public string Tag { get; }
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int? Revision {  get; }
        public string Prerelease { get; }
        public string BuildMetadata { get; }

        public SemVersion(string tag, int major, int minor, int patch, int? revision, string prerelease, string buildMetadata)
        {
            Tag = tag;
            Major = major;
            Minor = minor;
            Patch = patch;
            Revision = revision;
            Prerelease = prerelease;
            BuildMetadata = buildMetadata;
        }

        public static bool TryParse(string input, out SemVersion result)
        {
            var match = versionRegex.Match(input);
            if (match.Success)
            {
               result = new SemVersion(
                    tag: input,
                    major: int.Parse(match.Groups["major"].Value),
                    minor: int.Parse(match.Groups["minor"].Value),
                    patch: int.Parse(match.Groups["patch"].Value),
                    revision: string.IsNullOrEmpty(match.Groups["revision"].Value) ? null : int.Parse(match.Groups["revision"].Value),
                    prerelease: match.Groups["prerelease"].Value,
                    buildMetadata: match.Groups["buildmetadata"].Value);
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        public int CompareTo(SemVersion other)
        {
            // Precedence is determined by the first difference when comparing each of these identifiers from left to right as follows:
            // Major, minor, and patch versions are always compared numerically.
            // Example: 1.0.0 < 2.0.0 < 2.1.0 < 2.1.1.

            if (Major != other.Major)
                return Major.CompareTo(other.Major);
            if (Minor != other.Minor)
                return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch)
                return Patch.CompareTo(other.Patch);
            if (Revision != other.Revision)
                return Comparer<int?>.Default.Compare(Revision, other.Revision);
               
            // When major, minor, and patch are equal, a pre-release version has lower precedence than a normal version
            // Example: 1.0.0-alpha < 1.0.0.
            bool weHavePrerelease = !string.IsNullOrEmpty(Prerelease);
            bool theyHavePrerelease = !string.IsNullOrEmpty(other.Prerelease);

            if (!weHavePrerelease && !theyHavePrerelease)
                return 0;
            if (weHavePrerelease != theyHavePrerelease)
                return weHavePrerelease ? -1 : 1;

            // Precedence for two pre-release versions with the same major, minor, and patch version MUST be determined by comparing
            // each dot separated identifier from left to right until a difference is found as follows
            // 1. Identifiers consisting of only digits are compared numerically
            // 2. Identifiers with letters or hyphens are compared lexically in ASCII sort order.
            // 3. Numeric identifiers always have lower precedence than non - numeric identifiers
            // 4. A larger set of pre-release fields has a higher precedence than a smaller set, if all of the preceding identifiers are equal.

            // Example: 1.0.0-alpha < 1.0.0-alpha.1 < 1.0.0-alpha.beta < 1.0.0-beta < 1.0.0-beta.2 < 1.0.0-beta.11 < 1.0.0-rc.1 < 1.0.0.

            string[] ourPrereleaseParts = Prerelease.Split('.');
            string[] theirPrereleaseParts = other.Prerelease.Split('.');
            for (int i = 0; i < Math.Min(ourPrereleaseParts.Length, theirPrereleaseParts.Length); i++)
            {
                string ourPart = ourPrereleaseParts[i];
                string theirPart = theirPrereleaseParts[i];

                bool oursIsInt = int.TryParse(ourPart, out int ourInt);
                bool theirsIsInt = int.TryParse(theirPart, out int theirInt);

                if (oursIsInt && theirsIsInt)
                {
                    if (ourInt != theirInt)
                        return ourInt.CompareTo(theirInt);
                    continue;
                }

                if (oursIsInt || theirsIsInt)
                {
                    return oursIsInt ? -1 : 1;
                }

                int stringComparison = string.Compare(ourPart, theirPart, StringComparison.Ordinal);
                if (stringComparison != 0)
                    return Math.Sign(stringComparison);
            }

            return ourPrereleaseParts.Length.CompareTo(theirPrereleaseParts.Length);
        }
    }
}
