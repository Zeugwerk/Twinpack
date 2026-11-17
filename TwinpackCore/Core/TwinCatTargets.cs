using System;
using System.Collections.Generic;
using Twinpack.Configuration;

namespace Twinpack.Core
{
    /// <summary>
    /// A reference whose target is empty or <see cref="ConfigPlcPackage.DefaultTarget"/> follows the
    /// open solution. A reference that names a product line, such as <c>TC3.1.4024</c>, stays on that line.
    /// </summary>
    public static class TwinCatTargets
    {
        public static bool IsUnpinned(string target)
        {
            return string.IsNullOrEmpty(target)
                || string.Equals(target, ConfigPlcPackage.DefaultTarget, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// <c>TC3.1.4024</c> names a product line. <c>TC3.1</c> and <c>TC3.1.4024.59</c> do not.
        /// </summary>
        public static bool IsProductLineKey(string key)
        {
            return VersionPartCount(key) == 3;
        }

        /// <summary>
        /// <c>TC3.1.4024.59</c> is a patch build. The legacy map matches that key exactly.
        /// </summary>
        public static bool IsPatchLevelKey(string key)
        {
            return VersionPartCount(key) >= 4;
        }

        /// <summary>
        /// <c>TC3.1</c> is 2, <c>TC3.1.4024</c> is 3, <c>TC3.1.4024.59</c> is 4.
        /// </summary>
        public static int VersionPartCount(string tcVersion)
        {
            if (string.IsNullOrWhiteSpace(tcVersion))
                return 0;

            var body = tcVersion.Trim();
            if (body.StartsWith("TC", StringComparison.OrdinalIgnoreCase))
                body = body.Substring(2);

            var count = 0;
            foreach (var part in body.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part, out _))
                    break;
                count++;
            }

            return count;
        }

        /// <summary>
        /// <c>TC3.1.4024.59</c> and <c>3.1.4024.59</c> become <c>TC3.1.4024</c>.
        /// <c>TC3.1</c> stays <c>TC3.1</c>.
        /// </summary>
        public static string ProductLine(string tcVersion)
        {
            if (string.IsNullOrWhiteSpace(tcVersion))
                return null;

            var body = tcVersion.Trim();
            if (body.StartsWith("TC", StringComparison.OrdinalIgnoreCase))
                body = body.Substring(2);

            var parts = body.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return ConfigPlcPackage.DefaultTarget;

            return "TC" + parts[0] + "." + parts[1] + "." + parts[2];
        }

        /// <summary>
        /// Targets to ask for, in order. A pin is exact. An unpinned reference asks for the solution
        /// line and then <c>TC3.1</c>. Another product line is never added.
        /// </summary>
        public static IReadOnlyList<string> ResolveOrder(string configuredTarget, string solutionProductLine)
        {
            if (!IsUnpinned(configuredTarget))
                return new[] { configuredTarget };

            var line = ProductLine(solutionProductLine);
            if (!string.IsNullOrEmpty(line) && !IsUnpinned(line))
                return new[] { line, ConfigPlcPackage.DefaultTarget };

            return new[] { ConfigPlcPackage.DefaultTarget };
        }

        /// <summary>
        /// Value written back to <c>config.json</c>. The solution line is stored only when the user
        /// picked it. An automatic selection leaves an unpinned reference on <c>TC3.1</c>.
        /// </summary>
        public static string TargetToStore(string configuredTarget, string selectedTarget, bool userPicked)
        {
            if (userPicked && !string.IsNullOrEmpty(selectedTarget))
                return selectedTarget;

            if (IsUnpinned(configuredTarget))
                return ConfigPlcPackage.DefaultTarget;

            return configuredTarget;
        }
    }
}
