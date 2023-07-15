using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Twinpack
{
    public class VsixManifest
    {
        private static readonly NLog.Logger logger_ = LogManager.GetCurrentClassLogger();

        public string Id { get; set; }

        public string Version { get; set; }

        public VsixManifest()
        {

        }

        public VsixManifest(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            if (doc.DocumentElement == null || doc.DocumentElement.Name != "PackageManifest") return;

            var metaData = doc.DocumentElement.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Metadata");
            var identity = metaData.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Identity");

            Id = identity.GetAttribute("Id");
            logger_.Trace("VsixManifest - Id: " + Id);
            Version = identity.GetAttribute("Version");
            logger_.Trace("VsixManifest - Version: " + Version);
        }

        public static VsixManifest GetManifest()
        {
            var assembly = Assembly.GetExecutingAssembly();
            logger_.Trace("VsixManifest - Assembly: " + assembly.FullName);
            var assemblyUri = new UriBuilder(assembly.CodeBase);
            logger_.Trace("VsixManifest - AssemblyUri: " + assemblyUri.Path);
            var assemblyPath = Uri.UnescapeDataString(assemblyUri.Path);
            logger_.Trace("VsixManifest - AssemblyPath: " + assemblyPath);
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            logger_.Trace("VsixManifest - AssemblyDirectory: " + assemblyDirectory);
            var vsixManifestPath = Path.Combine(assemblyDirectory, "extension.vsixmanifest");

            logger_.Debug("VsixManifest: " + vsixManifestPath);
            return new VsixManifest(vsixManifestPath);
        }
    }
}
