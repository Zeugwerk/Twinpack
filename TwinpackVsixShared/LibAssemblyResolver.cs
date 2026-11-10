using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;

namespace Twinpack
{
    /// <summary>
    /// System.Text.Json and the assemblies it needs live under lib. Zeugwerk, Twinpack, NuGet,
    /// and anything already loaded from lib can take a lib file that is new enough.
    /// Any other request is answered when that file has the same major version.
    /// A request the file in lib cannot satisfy may use lib\v{major}, which is how the
    /// 32-bit shell keeps 6 for us and still loads 9 for Beckhoff.
    /// </summary>
    static class LibAssemblyResolver
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly object Sync = new object();
        static bool _registered;
        [ThreadStatic] static string _loadingName;

        public static void EnsureRegistered()
        {
            lock (Sync)
            {
                if (_registered)
                    return;

                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _registered = true;
                Log.Info("AssemblyResolve registered (lib for Zeugwerk, Twinpack, and NuGet)");
            }
        }

        static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            AssemblyName requested;
            try
            {
                requested = new AssemblyName(args.Name);
            }
            catch (Exception)
            {
                return null;
            }

            return ResolveLib(requested, args.RequestingAssembly);
        }

        static Assembly ResolveLib(AssemblyName requested, Assembly requesting)
        {
            if (SameName(_loadingName, requested.Name))
                return null;

            var ours = ServesLib(requesting);
            var path = FindLibPath(requested, ours);
            if (path == null)
            {
                Log.Trace("Skip {0} {1}; no matching file under lib", requested.Name, requested.Version);
                return null;
            }

            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => SameName(x.GetName().Name, requested.Name)
                    && string.Equals(SafeLocation(x), path, StringComparison.OrdinalIgnoreCase));
            if (loaded != null)
                return loaded;

            _loadingName = requested.Name;
            try
            {
                var assembly = Assembly.LoadFrom(path);
                Log.Info("Loaded {0} from {1}", assembly.FullName, path);
                return assembly;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load {0} from {1}", requested.Name, path);
                return null;
            }
            finally
            {
                _loadingName = null;
            }
        }

        static string FindLibPath(AssemblyName requested, bool ours)
        {
            var fileName = requested.Name + ".dll";
            var direct = Path.Combine(LibDirectory(), fileName);
            if (Matches(direct, requested, ours))
                return direct;

            if (requested.Version == null)
                return null;

            var sameMajor = Path.Combine(LibDirectory(), "v" + requested.Version.Major, fileName);
            if (Matches(sameMajor, requested, false))
                return sameMajor;

            string best = null;
            Version bestVersion = null;
            var root = LibDirectory();
            if (!Directory.Exists(root))
                return null;

            foreach (var dir in Directory.GetDirectories(root, "v*"))
            {
                var candidate = Path.Combine(dir, fileName);
                if (!TryRead(candidate, requested, out var onDisk) || onDisk.Version == null)
                    continue;
                if (onDisk.Version < requested.Version)
                    continue;
                if (bestVersion != null && onDisk.Version >= bestVersion)
                    continue;

                best = candidate;
                bestVersion = onDisk.Version;
            }

            return best;
        }

        static bool Matches(string path, AssemblyName requested, bool ours)
        {
            if (!TryRead(path, requested, out var onDisk))
                return false;

            return VersionSatisfies(ours, requested.Version, onDisk.Version);
        }

        static bool TryRead(string path, AssemblyName requested, out AssemblyName onDisk)
        {
            onDisk = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                onDisk = AssemblyName.GetAssemblyName(path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not read assembly name from {0}", path);
                return false;
            }

            var token = requested.GetPublicKeyToken();
            return token == null || token.Length == 0 || SameToken(token, onDisk.GetPublicKeyToken());
        }

        static bool VersionSatisfies(bool ours, Version requested, Version onDisk)
        {
            if (requested == null || onDisk == null)
                return ours;

            if (onDisk < requested)
                return false;

            return ours || onDisk.Major == requested.Major;
        }

        static bool ServesLib(Assembly requesting)
        {
            if (requesting == null)
                return false;

            var name = requesting.GetName().Name ?? string.Empty;
            if (name.StartsWith("Zeugwerk.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Twinpack.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("NuGet.", StringComparison.OrdinalIgnoreCase))
                return true;

            var directory = Path.GetDirectoryName(SafeLocation(requesting));
            var lib = LibDirectory();
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(lib))
                return false;

            return string.Equals(directory, lib, StringComparison.OrdinalIgnoreCase)
                || directory.StartsWith(lib + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        static string LibDirectory()
        {
            var dir = Path.GetDirectoryName(typeof(LibAssemblyResolver).Assembly.Location);
            return Path.Combine(dir ?? string.Empty, "lib");
        }

        static string SafeLocation(Assembly assembly)
        {
            try
            {
                return assembly.IsDynamic ? "(dynamic)" : assembly.Location;
            }
            catch (Exception)
            {
                return "(unknown)";
            }
        }

        static bool SameName(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        static bool SameToken(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
