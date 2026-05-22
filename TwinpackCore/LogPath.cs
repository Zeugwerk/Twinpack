using System;
using System.IO;

namespace Twinpack
{
    /// <summary>Formats filesystem paths for log output (relative to cwd when possible).</summary>
    public static class LogPath
    {
        public static string Display(string path, string relativeTo = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (!Path.IsPathRooted(path))
                return path;

            relativeTo = relativeTo ?? Directory.GetCurrentDirectory();
            return ToRelative(relativeTo, path);
        }

        public static string ToRelative(string relativeTo, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var baseFull = Path.GetFullPath(relativeTo);
            var pathFull = Path.GetFullPath(path);

            if (string.Equals(baseFull, pathFull, StringComparison.OrdinalIgnoreCase))
                return ".";

            if (!baseFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                baseFull += Path.DirectorySeparatorChar;

            var from = new Uri(baseFull, UriKind.Absolute);
            var to = new Uri(pathFull, UriKind.Absolute);
            if (!string.Equals(from.Scheme, to.Scheme, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(pathFull) ?? path;

            var rel = Uri.UnescapeDataString(from.MakeRelativeUri(to).ToString())
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (string.IsNullOrEmpty(rel) || Path.IsPathRooted(rel))
                return Path.GetFileName(pathFull) ?? path;

            if (rel.StartsWith("..", StringComparison.Ordinal))
            {
                var underTemp = TryRelativeToTemp(pathFull);
                if (underTemp != null)
                    return underTemp;
                return pathFull;
            }

            return rel;
        }

        static string TryRelativeToTemp(string pathFull)
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (!tempRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                tempRoot += Path.DirectorySeparatorChar;

            if (!pathFull.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                return null;

            var tail = pathFull.Substring(tempRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(tail) ? "tmp" : "tmp" + Path.DirectorySeparatorChar + tail;
        }
    }
}
