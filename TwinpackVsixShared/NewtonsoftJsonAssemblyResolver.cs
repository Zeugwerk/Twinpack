using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NLog;

namespace Twinpack
{
    /// <summary>
    /// Serves Newtonsoft.Json 13 from this extension folder.
    /// NuGet.Protocol needs that assembly for NuGet V2 feeds (FindPackagesById). The VSIX SDK
    /// leaves Newtonsoft.Json.dll out of the package, and older TcXaeShell installs do not
    /// have 13.0.0.0. Requests from any other folder, including Creator, are ignored so the
    /// shell keeps the Newtonsoft build it already loaded.
    /// </summary>
    static class NewtonsoftJsonAssemblyResolver
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly object Sync = new object();
        static readonly byte[] PublicKeyToken = { 0x30, 0xad, 0x4f, 0xe6, 0xb2, 0xa6, 0xae, 0xed };
        static bool _registered;
        static Assembly _json;
        static int _loading;

        const string JsonAssemblyName = "Newtonsoft.Json";

        public static void EnsureRegistered()
        {
            lock (Sync)
            {
                if (_registered)
                    return;

                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _registered = true;
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

            if (!string.Equals(requested.Name, JsonAssemblyName, StringComparison.OrdinalIgnoreCase))
                return null;

            if (requested.Version != null && requested.Version.Major != 13)
                return null;

            var token = requested.GetPublicKeyToken();
            if (token != null && token.Length > 0 && !TokensEqual(token, PublicKeyToken))
                return null;

            if (!IsExtensionRequester(args.RequestingAssembly))
                return null;

            var loaded = LoadedJson();
            if (loaded != null)
                return loaded;

            if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0)
                return null;

            try
            {
                loaded = LoadedJson() ?? LoadShipped();
                if (loaded != null)
                    _json = loaded;
                return loaded;
            }
            finally
            {
                Interlocked.Exchange(ref _loading, 0);
            }
        }

        static bool IsExtensionRequester(Assembly requesting)
        {
            if (requesting == null)
                return false;

            var name = requesting.GetName().Name ?? string.Empty;
            if (!name.StartsWith("NuGet.", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("Twinpack", StringComparison.OrdinalIgnoreCase))
                return false;

            string location;
            try
            {
                location = requesting.Location;
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrEmpty(location))
                return false;

            var extensionDir = Path.GetDirectoryName(typeof(NewtonsoftJsonAssemblyResolver).Assembly.Location);
            var requesterDir = Path.GetDirectoryName(location);
            return string.Equals(extensionDir, requesterDir, StringComparison.OrdinalIgnoreCase);
        }

        static Assembly LoadedJson()
        {
            if (_json != null)
                return _json;

            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(IsJson13);
        }

        static bool IsJson13(Assembly assembly)
        {
            AssemblyName name;
            try
            {
                name = assembly.GetName();
            }
            catch (Exception)
            {
                return false;
            }

            return string.Equals(name.Name, JsonAssemblyName, StringComparison.OrdinalIgnoreCase)
                && name.Version != null
                && name.Version.Major == 13
                && TokensEqual(name.GetPublicKeyToken(), PublicKeyToken);
        }

        static Assembly LoadShipped()
        {
            var dir = Path.GetDirectoryName(typeof(NewtonsoftJsonAssemblyResolver).Assembly.Location);
            var path = Path.Combine(dir ?? string.Empty, JsonAssemblyName + ".dll");
            if (!File.Exists(path))
            {
                Log.Warn("Newtonsoft.Json 13 was requested by NuGet and is not in the extension folder: {0}", path);
                return null;
            }

            try
            {
                var assembly = Assembly.LoadFrom(path);
                Log.Info("Loaded {0} from {1}", assembly.FullName, path);
                return assembly;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load {0}", path);
                return null;
            }
        }

        static bool TokensEqual(byte[] actual, byte[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
                return false;

            for (var i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i])
                    return false;
            }

            return true;
        }
    }
}
