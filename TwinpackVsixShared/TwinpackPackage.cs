using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NLog;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using TCatSysManagerLib;
using Task = System.Threading.Tasks.Task;
using FontAwesome.WPF;
using NLog.Layouts;
using Twinpack.Core;

namespace Twinpack
{
    public class PackageContext
    {
        public static string UrlLibraryRepository = "https://zeugwerk.dev/Zeugwerk_Twinpack/Repositories";

        public Solution Solution { get; set; }
        public DTE2 Dte { get; set; }
        public VisualStudio VisualStudio { get; set; }
        public string Version
        {
            get
            {
                VsixManifest manifest = VsixManifest.GetManifest();
                return manifest.Version;
            }
        }

        public IVsStatusbar Statusbar { get; set; }
        public VsOutputWindowTarget Logger { get; set; }

        public async Task WriteStatusAsync(string message)
        {
            if (Statusbar == null)
                return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Statusbar?.SetText(message);
        }
    }

    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(TwinpackPackage.PackageGuidString)]
    [ProvideToolWindow(typeof(Dialogs.CatalogPane), 
        Orientation = ToolWindowOrientation.Left,
        MultiInstances = false,
        Transient = true,
        Style = VsDockStyle.Tabbed)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class TwinpackPackage : AsyncPackage, IVsSolutionEvents
    {
        private static Logger _logger;

        /// <summary>
        /// TwinpackPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "26e0356d-ac0e-4e6a-a50d-dd2a812f6f23";

        #region Package Members
        private List<Commands.ICommand> _commands;
        private uint _solutionEventsCookie;

        public TwinpackPackage() : base()
        {
            // all context information, which is needed in all classes in the extension
            Context = new PackageContext
            {
                Statusbar = (IVsStatusbar)GetGlobalService(typeof(SVsStatusbar)),
                Logger = new VsOutputWindowTarget { Layout = @"${uppercase:${level:padding=6:fixedLength=true}} ${message}" }
            };

            var config = LogManager.Configuration ?? new NLog.Config.LoggingConfiguration();
            var logFileTraceTarget = new NLog.Targets.FileTarget("Twinpack")
            {
                FileName = @"${specialfolder:folder=LocalApplicationData}\Zeugwerk\logs\Twinpack\Twinpack.debug.log",
                MaxArchiveFiles = 7,
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Day,
                ArchiveFileName = @"${specialfolder:folder=LocalApplicationData}\Zeugwerk\logs\Twinpack\Twinpack.debug{#}.log",
                ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Rolling,
                KeepFileOpen = false
            };
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logFileTraceTarget, "Twinpack.*");
            config.AddRule(LogLevel.Info, LogLevel.Fatal, Context.Logger, "Twinpack.*");
            LogManager.Configuration = config;

            _logger = LogManager.GetCurrentClassLogger();

            // This is needed to vsix is loading the assembly for FontAwesome.WPF
            _logger.Trace(FontAwesomeIcon.FolderOpen.ToString());
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            _logger.Info($"Twinpack {Context.Version}");

            await Commands.CatalogCommand.InitializeAsync(this);
            await Commands.TwinpackMenuCommand.InitializeAsync(this);
            await Commands.ModifyCommand.InitializeAsync(this);
            await Commands.PublishCommand.InitializeAsync(this);


            // Liste aller Kommandos in der Extension --> wichtig für gemeinsame Initialisierung
            _commands = new List<Commands.ICommand>
            {
                Commands.CatalogCommand.Instance,
                Commands.TwinpackMenuCommand.Instance,
                Commands.PublishCommand.Instance,
                Commands.ModifyCommand.Instance
            };

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (await GetServiceAsync(typeof(SVsSolution)) is IVsSolution vssolution_)
                vssolution_.AdviseSolutionEvents(this, out _solutionEventsCookie);

            await Protocol.PackagingServerRegistry.InitializeAsync();

            InitPackage();
        }

        private void InitPackage()
        {
            if (IsInitialized == true)
                return;

            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Context.Dte = GetService(typeof(DTE)) as DTE2;

                if (Context.Dte == null)
                {
                    _logger.Error("Twinpack initialization failed, couldn't locate DTE");
                    return;
                }

                Context.Solution = Context.Dte.Solution;
                Context.VisualStudio = new VisualStudio(Context.Dte, Context.Solution);
                var projects = Context.Solution.Projects;

                if (projects.Count == 0)
                    return;

                ITcSysManager sysManager = null;
                foreach (Project prj in projects)
                {
                    try
                    {
                        sysManager = (ITcSysManager)prj.Object;
                        break;
                    }
                    catch (Exception) { }
                }

                // TcSysManager konnte nicht initialisiert werden, da kein TwinCAT Projekt geladen ist
                if (sysManager == null)
                {
                    _logger.Error("Twinpack initialization failed, this is not a TwinCAT project, no systemmanager detected");
                    return;
                }

                IsInitialized = true;
                ActivateCommands();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }

        }

        private void ActivateCommands()
        {
            // Kommandos "freischalten" für die Verwendung
            foreach (var cmd in _commands)
            {
                try
                {
                    cmd.PackageReady();
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
            }
        }

        private void ResetPackage()
        {
            IsInitialized = false;
            

            foreach (var cmd in _commands)
                cmd.PackageReset();
        }


        #endregion

        public PackageContext Context { get; private set; }

        public bool IsInitialized { get; private set; }

        protected override WindowPane InstantiateToolWindow(Type toolWindowType)
        {
            if (toolWindowType == typeof(Dialogs.CatalogPane))
                return new Dialogs.CatalogPane(Context);
            return base.InstantiateToolWindow(toolWindowType);
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            InitPackage();
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            ResetPackage();
            return VSConstants.S_OK;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            InitPackage();
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            ResetPackage();
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }
    }
}
