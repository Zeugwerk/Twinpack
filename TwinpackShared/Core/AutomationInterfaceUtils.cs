using EnvDTE80;
using System;
using TCatSysManagerLib;

namespace Twinpack.Core
{
    public class AutomationInterfaceService
    {
        DTE2 _dte;

        public AutomationInterfaceService(DTE2 dte)
        {
            _dte = dte;
        }

        public string ResolveEffectiveVersion(string projectName, string placeholderName)
        {
            ResolvePlaceholder(LibraryManager(projectName), placeholderName, out _, out string effectiveVersion);

            return effectiveVersion;
        }

        private static ITcPlcLibrary ResolvePlaceholder(ITcPlcLibraryManager libManager, string placeholderName, out string distributorName, out string effectiveVersion)
        {
            // Try to remove the already existing reference
            foreach (ITcPlcLibRef item in libManager.References)
            {
                string itemPlaceholderName;
                ITcPlcLibrary plcLibrary;

                try
                {
                    ITcPlcPlaceholderRef2 plcPlaceholder; // this will throw if the library is currently not installed
                    plcPlaceholder = (ITcPlcPlaceholderRef2)item;

                    itemPlaceholderName = plcPlaceholder.PlaceholderName;

                    if (plcPlaceholder.EffectiveResolution != null)
                        plcLibrary = plcPlaceholder.EffectiveResolution;
                    else
                        plcLibrary = plcPlaceholder.DefaultResolution;

                    effectiveVersion = plcLibrary.Version;
                    distributorName = plcLibrary.Distributor;
                }
                catch
                {
                    plcLibrary = (ITcPlcLibrary)item;
                    effectiveVersion = null;
                    itemPlaceholderName = plcLibrary.Name.Split(',')[0];
                    distributorName = plcLibrary.Distributor;
                }

                if (string.Equals(itemPlaceholderName, placeholderName, StringComparison.InvariantCultureIgnoreCase))
                    return plcLibrary;
            }

            distributorName = null;
            effectiveVersion = null;
            return null;
        }

        private ITcSysManager SystemManager(string projectName = null)
        {
            var ready = false;
            while (!ready)
            {
                ready = true;
                foreach (EnvDTE.Project project in _dte.Solution.Projects)
                {
                    if (project == null)
                        ready = false;
                    else if ((projectName == null || project?.Name == projectName) && project.Object as ITcSysManager != null)
                        return project.Object as ITcSysManager;
                }

                if (!ready)
                    System.Threading.Thread.Sleep(1000);
            }

            return null;
        }

        private ITcPlcLibraryManager LibraryManager(string projectName = null)
        {
            var systemManager = SystemManager(projectName);

            if (projectName == null)
            {
                var plcConfiguration = systemManager.LookupTreeItem("TIPC");
                for (var j = 1; j <= plcConfiguration.ChildCount; j++)
                {
                    var plc = (plcConfiguration.Child[j] as ITcProjectRoot)?.NestedProject;
                    for (var k = 1; k <= (plc?.ChildCount ?? 0); k++)
                    {
                        if (plc.Child[k] as ITcPlcLibraryManager != null)
                        {
                            return plc.Child[k] as ITcPlcLibraryManager;
                        }
                    }
                }
            }
            else
            {
                var projectRoot = systemManager.LookupTreeItem($"TIPC^{projectName}");
                var plc = (projectRoot as ITcProjectRoot)?.NestedProject;
                for (var k = 1; k <= (plc?.ChildCount ?? 0); k++)
                {
                    if (plc.Child[k] as ITcPlcLibraryManager != null)
                    {
                        return plc.Child[k] as ITcPlcLibraryManager;
                    }
                }

            }

            return null;
        }
    }
}