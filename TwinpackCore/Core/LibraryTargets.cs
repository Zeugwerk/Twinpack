using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Twinpack.Core
{
    /// <summary>
    /// A build saves its libraries to <c>.Zeugwerk/libraries/{target}</c>, where <c>{target}</c> is
    /// the TwinCAT target the build actually resolved. That is not necessarily <c>TC3.1</c>: with the
    /// target left unspecified it is whatever the remote manager picked, for instance
    /// <c>TC3.1.4024.56</c>. Defaulting to <c>TC3.1</c> would then read from a folder that does not
    /// exist, so the target is read back off disk instead of guessed.
    /// </summary>
    public static class LibraryTargets
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>Used only when nothing was built yet, so there is nothing to read back.</summary>
        public const string Default = "TC3.1";

        public static string DirectoryOf(string root)
        {
            return Path.Combine(root ?? Directory.GetCurrentDirectory(), ".Zeugwerk", "libraries");
        }

        /// <summary>Targets a build left behind, in the order they sort.</summary>
        public static IReadOnlyList<string> Available(IEnumerable<string> libraryDirectories)
        {
            var targets = new List<string>();
            foreach (var directory in libraryDirectories ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    continue;

                foreach (var target in Directory.GetDirectories(directory).Select(Path.GetFileName))
                {
                    if (!targets.Contains(target, StringComparer.OrdinalIgnoreCase))
                        targets.Add(target);
                }
            }

            targets.Sort(StringComparer.OrdinalIgnoreCase);
            return targets;
        }

        /// <summary>
        /// Returns <paramref name="requested"/> unchanged when the caller named a target. Otherwise
        /// the single target found on disk. Several targets throw rather than pick one, because a
        /// wrong pick would silently pack stale output from another build.
        /// </summary>
        public static string Resolve(string requested, IEnumerable<string> libraryDirectories)
        {
            if (!string.IsNullOrWhiteSpace(requested))
                return requested;

            var available = Available(libraryDirectories);
            if (available.Count > 1)
            {
                throw new InvalidOperationException(
                    $"The build output contains more than one TwinCAT target ({string.Join(", ", available.ToArray())}), "
                    + "name the one to use explicitly");
            }

            if (available.Count == 0)
                return Default;

            _logger.Info("[pack] target: {0} (from the build output)", available[0]);
            return available[0];
        }
    }
}
