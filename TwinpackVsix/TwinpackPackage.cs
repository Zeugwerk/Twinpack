using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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

namespace Twinpack
{
    public class PackageContext
    {
        public static string UrlLibraryRepository = "https://zeugwerk.dev/Zeugwerk_Twinpack/Repositories";

        public Solution Solution { get; set; }
        public DTE2 Dte { get; set; }
        public string Version
        {
            get
            {
                VsixManifest manifest = VsixManifest.GetManifest();
                return manifest.Version;
            }
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
    [ProvideToolWindow(typeof(Dialogs.CatalogWindowPane))]
    [ProvideToolWindow(typeof(Dialogs.TwinpackPublishPane))]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class TwinpackPackage : AsyncPackage, IVsSolutionEvents
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// TwinpackPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "26e0356d-ac0e-4e6a-a50d-dd2a812f6f23";

        #region Package Members
        private List<Commands.ICommand> _commands;
        private uint _solutionEventsCookie;

        public TwinpackPackage() : base()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
            NLog.LogManager.Setup();

            _logger.Debug("Twinpack constructed");
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
            await Commands.CatalogCommand.InitializeAsync(this);
            await Commands.PublishCommand.InitializeAsync(this);


            // Liste aller Kommandos in der Extension --> wichtig für gemeinsame Initialisierung
            _commands = new List<Commands.ICommand>();
            _commands.Add(Commands.CatalogCommand.Instance);
            _commands.Add(Commands.CatalogCommand.Instance);


            // all context information, which is needed in all classes in the extension
            Context = new PackageContext();

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (await GetServiceAsync(typeof(SVsSolution)) is IVsSolution vssolution_)
                vssolution_.AdviseSolutionEvents(this, out _solutionEventsCookie);

            InitPackage();
        }

        private void InitPackage()
        {
            // Initialisierung bereits durchgeführt?
            if (IsInitialized == true)
                return;
            _logger.Error("InitPackage");
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Context == null)
                return;

            try
            {
                _logger.Error("Getting DTE");

                if (Context.Dte == null)
                    Context.Dte = GetService(typeof(DTE)) as DTE2;

                if (Context.Dte == null)
                {
                    _logger.Error("DTE Service konnte nicht geladen werden.");
                    return;
                }

                _logger.Error("Getting Solution");

                Context.Solution = Context.Dte.Solution;
                var projects = Context.Solution.Projects;

                _logger.Error("Projects");

                if (projects.Count == 0)
                    return;

                ITcSysManager sysManager = null;
                foreach (Project prj in projects)
                {
                    try
                    {
                        _logger.Error("Found System Manager");

                        sysManager = (ITcSysManager)prj.Object;
                        break;
                    }
                    catch (Exception) { }
                }

                // TcSysManager konnte nicht initialisiert werden, da kein TwinCAT Projekt geladen ist
                if (sysManager == null)
                    return;

                IsInitialized = true;
                ActivateCommands();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex);
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
                    _logger.Error($"Exception {cmd}: {ex.Message}");
                }
            }
        }

        private void ResetPackage()
        {
            IsInitialized = false;

            Context = null;

            foreach (var cmd in _commands)
                cmd.PackageReset();
        }


        #endregion

        public PackageContext Context { get; private set; }

        public bool IsInitialized { get; private set; }

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
