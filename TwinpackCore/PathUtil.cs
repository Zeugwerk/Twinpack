using System.IO;

namespace Twinpack
{
    /// <summary>
    /// Small set of cross-platform path helpers used throughout TwinpackCore.
    /// Many on-disk artefacts that Twinpack reads (.sln/.tsproj/.xti/config.json) were
    /// originally authored on Windows and therefore contain literal backslashes embedded
    /// inside the strings they hand to us. <see cref="System.IO.Path.Combine"/> does NOT
    /// rewrite those inner separators, so on Linux/macOS the resulting path is a single
    /// nonsensical filename. These helpers normalize the separator before combining so
    /// the same code path works on every OS.
    /// </summary>
    public static class PathUtil
    {
        /// <summary>
        /// Convert any backslashes in <paramref name="path"/> to the platform separator.
        /// Returns the input unchanged when it is null or empty.
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // On Windows DirectorySeparatorChar == '\\', so this is a no-op; on Linux/macOS
            // it folds Windows-authored fragments into POSIX paths.
            return path.Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Drop-in replacement for <see cref="System.IO.Path.Combine(string[])"/> that
        /// normalizes every fragment first, so an inner '\\' is treated as a separator
        /// on every OS instead of being mistaken for part of a filename.
        /// </summary>
        public static string Combine(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            var normalized = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                normalized[i] = Normalize(parts[i]);

            return Path.Combine(normalized);
        }
    }
}
