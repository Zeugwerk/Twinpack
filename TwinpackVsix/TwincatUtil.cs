using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TCatSysManagerLib;

namespace Twinpack
{
    internal class TwincatUtil
    {
        public static string ActiveProject(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic prj = null;

            if (dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
            {
                prj = activeSolutionProjects.GetValue(0) as Project;
            }

            return prj.Name;
        }

        public static ITcSysManager12 ActiveSystemManager(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic prj = null;

            if (dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
            {
                prj = activeSolutionProjects.GetValue(0) as Project;
            }

            try
            {
                ITcSysManager systemManager = (ITcSysManager)prj.Object;
                ITcSmTreeItem plcs = systemManager.LookupTreeItem("TIPC");

                foreach (ITcSmTreeItem9 plc in plcs)
                {
                    if (plc is ITcProjectRoot)
                    {
                        ITcSmTreeItem nestedProject = ((ITcProjectRoot)plc).NestedProject;
                        string xml = plc.ProduceXml();
                        string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                        if (projectPath != null && projectPath == prj.FullName)
                            return plc.SystemManager;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        public static ITcPlcLibraryManager ActivePlcLibraryManager(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            dynamic prj = null;

            if (dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
            {
                prj = activeSolutionProjects.GetValue(0) as Project;
            }

            try
            {
                ITcSysManager systemManager = (ITcSysManager)prj.Object;
                ITcSmTreeItem plcs = systemManager.LookupTreeItem("TIPC");

                foreach (ITcSmTreeItem9 plc in plcs)
                {
                    if (plc is ITcProjectRoot)
                    {
                        ITcSmTreeItem nestedProject = ((ITcProjectRoot)plc).NestedProject;
                        string xml = plc.ProduceXml();
                        string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                        if (projectPath != null && projectPath == prj.FullName)
                            return plc.SystemManager.LookupTreeItem(prj.Object.Project.SysManTreeItem.PathName + "^References");
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        internal static string FindTargetSystem(DTE2 dte, ref string requested_tc_version)
        {
            string remotemanager_tcversion = requested_tc_version?.Replace("TC", ""); // TC3.1.4024.10

            dynamic remote = dte.GetObject("TcRemoteManager");
            // set target system - check if the target system is installed. If not, abort
            if (requested_tc_version != null && requested_tc_version != "")
            {
                Console.WriteLine($"[CONFIGURE] Output target system: {requested_tc_version}");

                var rmversions = (remote.Version != null && remote.Version != "") ? new string[] { remote.Version } : ((string[])remote.Versions);
                var len = rmversions[0].Split('.').Length;
                var targetLen = requested_tc_version.Split('.').Length;
                bool found = false;
                while (!found && len >= targetLen)
                {
                    foreach (var version in rmversions)
                    {
                        var versionArray = version.Split('.');
                        Array.Resize(ref versionArray, len);
                        if (remotemanager_tcversion == string.Join(".", versionArray))
                        {
                            remotemanager_tcversion = version;
                            found = true;
                            break;
                        }
                    }
                    len--;
                }

                if (!found)
                    throw new System.ArgumentException($"Output target system {remotemanager_tcversion} not found in {string.Join(",", remote.Versions)}");

                Console.WriteLine($"[CONFIGURE] Using target system:  TC{remotemanager_tcversion}");

                if (remote.Version != remotemanager_tcversion)
                    remote.Version = remotemanager_tcversion;
                return remotemanager_tcversion;
            }
            else
            {
                Console.WriteLine($"[CONFIGURE] Using target system: Local default");
                return null;
            }
        }
    }
}
