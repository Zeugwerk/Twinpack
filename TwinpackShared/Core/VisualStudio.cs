using System;
using System.IO;
using TCatSysManagerLib;
using System.Linq;
using EnvDTE80;
using System.Collections.Generic;
using NLog;
using System.Runtime.InteropServices;
using Twinpack.Models;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using Twinpack;
using EnvDTE;
using System.Management;

namespace Twinpack.Core
{
    public class VisualStudio : IDisposable
    {
        protected DTE2 _dte;
        protected EnvDTE.Solution _solution;
        protected IAutomationInterface _automationInterface;
        protected System.Timers.Timer _timeout;
        MessageFilter _filter;

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public string OutputTcVersion { get; private set; }
        public string UsedTcVersion { get; private set; }
        public DTE2 Dte => _dte;
        public EnvDTE.Solution Solution => _solution;
        public IAutomationInterface AutomationInterface { get { return _automationInterface ?? EnsureAutomationInterface(); } }

        public VisualStudio(bool hidden = true)
        {
            Initialize(hidden);
        }

        public VisualStudio(DTE2 dte, EnvDTE.Solution solution)
        {
            _dte = dte;
            _solution = solution;
        }

        public void Close(bool save=true)
        {
            _solution?.Close(save);
        }

        protected bool Initialize(bool hidden = true)
        {
            if (_dte != null)
                return true;

            // on some occasions, DTE2 gives up on us and blocks forever. For this case
            // we add a timeout 
            _timeout = new System.Timers.Timer(1000 * 60 * 30);
            _timeout.Elapsed += KillProcess;
            _timeout.AutoReset = false; // One-shot-timer
            _timeout.Start();

            _filter = new MessageFilter();
            List<string> shells = new List<string> { "TcXaeShell.DTE.15.0", "TcXaeShell.DTE.17.0" };

            foreach(var shell in shells)
            {
                Type t = Type.GetTypeFromProgID(shell);
                if (t != null)
                {
                    _logger.Info($"Loading {shell}");
                    DTE2 dte = Activator.CreateInstance(t) as DTE2;
                    dte.SuppressUI = hidden;
                    dte.MainWindow.Visible = !hidden;
                    dynamic settings = dte.GetObject("TcAutomationSettings");
                    settings.SilentMode = hidden;

                    _dte = dte;
                    _solution = _dte.Solution;

                    return true;
                }
            }

            throw new NotSupportedException($"No supported Visual Studio ({string.Join(", ", shells)}) detected!");
        }

        private void KillProcess(Object source, System.Timers.ElapsedEventArgs e)
        {
            _logger.Info($"Timeout occured ... killing processes");
            Environment.Exit(-1);

            Dispose();

            string processName = AppDomain.CurrentDomain.FriendlyName;
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (System.Diagnostics.Process p in processes)
            {
                p.Kill();
            }
        }

        public IAutomationInterface Open(Config config, string tcversion="TC3.1")
        {
            _logger.Info(new string('-', 3) + $" open-solution:{config.Solution}");

            OutputTcVersion = tcversion;
            var used_rm_version = FindTargetSystem(tcversion);

            _solution.Open($"{Directory.GetCurrentDirectory()}\\{config.Solution}");
            CloseAllWindows();
            var projects = WaitProjects();

            // disable all projects that are not needed
            var configProjectNames = config.Projects.Select(x => x.Name);
            foreach (EnvDTE.Project project in projects)
            {
                if (project == null)
                {
                    _logger.Trace("Project is null");
                    continue;
                }

                if (!configProjectNames.Contains(project.Name))
                {
                    try
                    {
                        _logger.Info($"Removing '{project.Name}' (not included in config)");
                        _solution.Remove(project);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex.Message);
                    }
                }
            }

            _logger.Info($"Setting up RemoteManager");

            // remote.Version is only set AFTER opening a _solution - weird but true
            UsedTcVersion = CurrentTcVersion();

            if (OutputTcVersion == null)
                OutputTcVersion = CurrentTcVersion();

            return EnsureAutomationInterface();
        }

        public IAutomationInterface EnsureAutomationInterface()
        {
            if (_automationInterface != null)
                return _automationInterface;

            var automationInterfaces = new List<IAutomationInterface>
            {
                new AutomationInterface(this),
            };

            foreach (var automationInterface in automationInterfaces)
            {
                if (automationInterface.IsSupported(UsedTcVersion))
                {
                    _logger.Info("Using " + automationInterface.GetType().FullName);
                    _automationInterface = automationInterface;

                    _automationInterface.ProgressedEvent += new EventHandler<ProgressEventArgs>((s, e) => {
                        _timeout.Stop();
                        _timeout.Start();
                    });

                    return _automationInterface;
                }
            }

            throw new NotSupportedException($"No AutomationInterface implementation supports {UsedTcVersion}");
        }

        public string CurrentTcVersion()
        {
            dynamic remoteManager = _dte.GetObject("TcRemoteManager");
            if (string.IsNullOrEmpty(remoteManager.Version))
            {
                if (remoteManager.Versions.Length > 0)
                    return $"TC{remoteManager.Versions[0]}";
                else
                    throw new ArgumentException("RemoteManager version is unknown, you have to open a _solution");
            }

            return $"TC{remoteManager.Version}";
        }

        private string FindTargetSystem(string requestedTcVersion = "TC3.1")
        {
            string remotemanager_tcversion = requestedTcVersion?.Replace("TC", "") ?? "TC3.1"; // TC3.1.4024.10

            dynamic remote = _dte.GetObject("TcRemoteManager");
            // set target system - check if the target system is installed. If not, abort
            if (requestedTcVersion != null && requestedTcVersion != "")
            {
                _logger.Info($"Output target system: {requestedTcVersion}");

                var rmversions = (remote.Version != null && remote.Version != "") ? new string[] { remote.Version } : ((string[])remote.Versions);
                var len = rmversions[0].Split('.').Length;
                var targetLen = requestedTcVersion.Split('.').Length;
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

                _logger.Info($"Using target system:  TC{remotemanager_tcversion}");

                if(remote.Version != remotemanager_tcversion)
                    remote.Version = remotemanager_tcversion;
                return remotemanager_tcversion;
            }
            else
            {
                _logger.Info($"Using target system: Local default");
                return null;
            }
        }

        public void CloseAllWindows()
        {
            try
            {
                while (!_dte.ActiveWindow.Caption.Contains("EnvDTE.Solution Explorer"))
                    _dte.ActiveWindow.Close();
            }
            catch { }
        }

        public void UnloadProject(string projectName)
        {
            try
            {
                var solutionName = Path.GetFileNameWithoutExtension(_solution.FileName);
                var se = _dte.ToolWindows.SolutionExplorer;
                var proj = se.GetItem($@"{solutionName}\{projectName}");
                proj.Select(vsUISelectionType.vsUISelectionTypeSelect);
                _dte.ExecuteCommand("Project.UnloadProject");
            }
            catch { }

        }

        public void ReloadProject(string projectName)
        {
            try
            {
                var solutionName = Path.GetFileNameWithoutExtension(_solution.FileName);
                var se = _dte.ToolWindows.SolutionExplorer;
                var proj = se.GetItem($@"{solutionName}\{projectName}");
                proj.Select(vsUISelectionType.vsUISelectionTypeSelect);
                _dte.ExecuteCommand("Project.ReloadProject");
            }
            catch { }
        }

        public bool ProjectExists(string projectName)
        {
            try
            {
                var solutionName = Path.GetFileNameWithoutExtension(_solution.FileName);
                var se = _dte.ToolWindows.SolutionExplorer;
                var proj = se.GetItem($@"{solutionName}\{projectName}");
                return true;
            }
            catch { }

            return false;
        }

        public void RemoveProject(string projectName)
        {
            EnvDTE.Project prj = null;
            foreach (EnvDTE.Project p in _solution.Projects)
            {
                if (p.Name == projectName)
                {
                    prj = p;
                    break;
                }
            }

            if (prj != null)
                _solution.Remove(prj);
        }

        // check the DTE2 for potential build errors
        public int BuildErrorCount()
        {
            int errorCount = 0;
            ErrorItems errors = _dte.ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= errors.Count; i++)
            {
                var item = errors.Item(i);

                switch (item.ErrorLevel)
                {
                    case vsBuildErrorLevel.vsBuildErrorLevelHigh:
                        _logger.Info($"{item.ErrorLevel} in {item.FileName} (line {item.Line}, col {item.Column}): {item.Description}");
                        errorCount++;
                        break;
                    case vsBuildErrorLevel.vsBuildErrorLevelMedium:
                        if (!item.Description.Contains("Method 'FB_init' already called implicitly") &&
                           !item.Description.Contains("The specified library will not be supported by TwinCAT XAE engineering environments older than version 3.1.4020.0"))
                            _logger.Info($"{item.ErrorLevel} in {item.FileName} (line {item.Line}, col {item.Column}): {item.Description}");
                        break;
                    default:
                        break;
                }
            }

            return errorCount;
        }

        public void SaveAll()
        {
            _dte.ExecuteCommand("File.SaveAll");
        }

        private Projects WaitProjects()
        {
            var ready = false;
            Projects projects = null;
            while (!ready)
            {
                ready = true;
                projects = _solution.Projects;
                foreach (EnvDTE.Project project in projects)
                {
                    if (project == null)
                        ready = false;
                }

                if (!ready)
                    System.Threading.Thread.Sleep(1000);
            }

            return projects;
        }

        // todo: do we actually need to return EnvDTE.Project ?
        public EnvDTE.Project ActiveProject()
        {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread

            if (_dte?.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects?.Length > 0)
            {
                var prj = activeSolutionProjects?.GetValue(0) as EnvDTE.Project;
                try
                {
                    ITcSysManager2 systemManager = (prj.Object as dynamic).SystemManager as ITcSysManager2;
                    if (systemManager != null)
                        return prj;
                }
                catch { }
            }

#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

            return null;
        }

        public void Dispose()
        {
            _filter?.Dispose();
            _dte?.Quit();
        }
    }
}
