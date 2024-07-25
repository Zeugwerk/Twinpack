using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Threading;
using NLog;
using TCatSysManagerLib;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl, INotifyPropertyChanged
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource _cancellationTokenSource;

        public event PropertyChangedEventHandler PropertyChanged;
        SelectionChangedEventHandler _packageServerChange;

        private PackageContext _context;
        private EnvDTE.Project _activeProject;
        private ITcPlcLibraryManager _libraryManager;

        private ConfigPlcProject _plcConfig;
        private ConfigPlcPackage _packageConfig;
        private IEnumerable<CatalogItem> _catalog = new List<CatalogItem>();
        private List<CatalogItem> _availablePackages = new List<CatalogItem>();
        private List<CatalogItem> _installedPackages = new List<CatalogItem>();
        private List<PlcVersion> _packageVersions = new List<PlcVersion>();
        private SemaphoreSlim _semaphorePackages = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _semaphoreAction = new SemaphoreSlim(1, 1);

        private CatalogItem _catalogItem = null;
        private PackageItem _packageItem = new PackageItem();

        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 10;

        private bool _isUpdateAvailable = false;
        private bool _isLoadingPlcConfig = false;
        private bool _isFetchingInstalledPackages = false;
        private bool _isAvailablePackageAvailable = false;
        private bool _isPackageVersionsAvailable = false;
        private bool _isFetchingAvailablePackages = false;

        private string _searchText = "";
        private Core.PackageServerCollection _packageServers;

        private bool _isBrowsingAvailablePackages;
        private bool _isBrowsingInstalledPackages;
        private bool _isBrowsingUpdatablePackages;

        private bool _isCatalogLoading;
        private bool _isPackageLoading;
        private bool _isPackageVersionLoading;
        private bool _isNewReference;
        private bool _isConfigured;
        private bool _isCreateConfigVisible;
        private bool _isMigrateConfigVisible;
        private bool _isUpdateAllVisible;
        private bool _isRestoreAllVisible;
        private bool _isInitializing;
        private bool _isCatalogEnabled;
        private bool _isRestoreAllEnabled;

        private bool _isPackageVersionPanelEnabled;
        private string _installedPackageVersion;

        private bool _forcePackageVersionDownload;
        private bool _forceShowLicense;
        private bool _uninstallDeletes;

        private int _installedPackagesCount;
        private int _updateablePackagesCount;

        private CancellationToken Token
        {
            get
            {
                if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource = new CancellationTokenSource();

                return _cancellationTokenSource.Token;
            }
        }

        public IEnumerable<CatalogItem> Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catalog)));
            }
        }

        public bool IsBusy
        {
            get
            {
                return IsFetchingAvailablePackages || IsFetchingInstalledPackages || IsCatalogLoading || IsPackageLoading || IsPackageVersionLoading;
            }
        }

        public bool IsFetchingAvailablePackages
        {
            get { return _isFetchingAvailablePackages; }
            set
            {
                _isFetchingAvailablePackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFetchingAvailablePackages)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));

                if (IsBrowsingAvailablePackages || IsBrowsingUpdatablePackages)
                {
                    if (_isFetchingAvailablePackages)
                        IsCatalogLoading = true;
                    else
                        UpdateCatalog();
                }
            }
        }

        public bool IsUpdateAvailable
        {
            get { return _isUpdateAvailable; }
            set
            {
                _isUpdateAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
            }
        }

        public bool IsFetchingInstalledPackages
        {
            get { return _isFetchingInstalledPackages; }
            set
            {
                _isFetchingInstalledPackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFetchingInstalledPackages)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));

                if (IsBrowsingInstalledPackages)
                {
                    if (_isFetchingInstalledPackages)
                        IsCatalogLoading = true;
                    else
                        UpdateCatalog();
                }
            }
        }

        public bool IsAvailablePackageAvailable
        {
            get { return _isAvailablePackageAvailable; }
            set
            {
                _isAvailablePackageAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailablePackageAvailable)));
            }
        }

        public bool IsPackageVersionsAvailable
        {
            get { return _isPackageVersionsAvailable; }
            set
            {
                _isPackageVersionsAvailable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageVersionsAvailable)));
            }
        }

        public int InstalledPackagesCount
        {
            get { return _installedPackagesCount; }
            set
            {
                _installedPackagesCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledPackagesCount)));
            }
        }

        public int UpdateablePackagesCount
        {
            get { return _updateablePackagesCount; }
            set
            {
                _updateablePackagesCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateablePackagesCount)));
            }
        }

        public PackageGetResponse Package
        {
            get { return _packageItem.Package; }
        }

        AddPlcLibraryOptions _options;
        public AddPlcLibraryOptions Options
        {
            get { return _options; }
            set
            {
                _options = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Options)));
            }
        }

        public PackageVersionGetResponse PackageVersion
        {
            get { return _packageItem.PackageVersion; }
        }

        public List<PlcVersion> Versions
        {
            get { return _packageVersions; }
            set
            {
                _packageVersions = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Versions)));
            }
        }

        public bool ForcePackageVersionDownload
        {
            get { return _forcePackageVersionDownload; }
            set
            {
                _forcePackageVersionDownload = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForcePackageVersionDownload)));
            }
        }

        public bool ForceShowLicense
        {
            get { return _forceShowLicense; }
            set
            {
                _forceShowLicense = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForceShowLicense)));
            }
        }

        public bool UninstallDeletes
        {
            get { return _uninstallDeletes; }
            set
            {
                _uninstallDeletes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UninstallDeletes)));
            }
        }

        public bool IsCatalogLoading
        {
            get { return _isCatalogLoading; }
            set
            {
                _isCatalogLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCatalogLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
            }
        }

        public bool IsPackageLoading
        {
            get { return _isPackageLoading; }
            set
            {
                _isPackageLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
            }
        }
        public bool IsPackageVersionLoading
        {
            get { return _isPackageVersionLoading; }
            set
            {
                _isPackageVersionLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageVersionLoading)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
            }
        }

        public bool IsUpdateAllVisible
        {
            get { return _isUpdateAllVisible; }
            set
            {
                _isUpdateAllVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAllVisible)));
            }
        }

        public bool IsRestoreAllVisible
        {
            get { return _isRestoreAllVisible; }
            set
            {
                _isRestoreAllVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestoreAllVisible)));
            }
        }

        public bool IsRestoreAllEnabled
        {
            get { return _isRestoreAllEnabled; }
        }

        public bool IsInitializing
        {
            get { return _isInitializing; }
            set
            {
                _isInitializing = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInitializing)));
            }
        }

        public bool IsCatalogEnabled
        {
            get { return _isCatalogEnabled; }
            set
            {
                _isCatalogEnabled = value;
                _isRestoreAllEnabled = _installedPackages.Any(x => x.Name == null) == false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCatalogEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestoreAllEnabled)));
            }
        }

        public bool IsCreateConfigVisible
        {
            get { return _isCreateConfigVisible; }
            set
            {
                _isCreateConfigVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCreateConfigVisible)));
            }
        }

        public bool IsMigrateConfigVisible
        {
            get { return _isMigrateConfigVisible; }
            set
            {
                _isMigrateConfigVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMigrateConfigVisible)));
            }
        }

        public bool IsConfigured
        {
            get { return _isConfigured; }
            set
            {
                _isConfigured = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConfigured)));
            }
        }

        public bool IsNewReference
        {
            get { return _isNewReference; }
            set
            {
                _isNewReference = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewReference)));
            }
        }

        public bool IsPackageVersionPanelEnabled
        {
            get { return _isPackageVersionPanelEnabled; }
            set
            {
                _isPackageVersionPanelEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageVersionPanelEnabled)));
            }
        }

        public string InstalledPackageVersion
        {
            get { return _installedPackageVersion; }
            set
            {
                _installedPackageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledPackageVersion)));
            }
        }

        public bool IsBrowsingInstalledPackages
        {
            get { return _isBrowsingInstalledPackages; }
            set
            {
                _isBrowsingInstalledPackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBrowsingInstalledPackages)));
            }
        }

        public bool IsBrowsingAvailablePackages
        {
            get { return _isBrowsingAvailablePackages; }
            set
            {
                _isBrowsingAvailablePackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBrowsingAvailablePackages)));
            }
        }

        public bool IsBrowsingUpdatablePackages
        {
            get { return _isBrowsingUpdatablePackages; }
            set
            {
                _isBrowsingUpdatablePackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBrowsingUpdatablePackages)));
            }
        }

        public CatalogWindow(PackageContext context)
        {
            _context = context;
            _packageServers = Protocol.PackagingServerRegistry.Servers;
            _packageServerChange = new SelectionChangedEventHandler(Reload);

            IsInitializing = true;
            IsCatalogEnabled = true;
            InstalledPackagesCount = 0;
            UpdateablePackagesCount = 0;
            ForcePackageVersionDownload = true;
            Catalog = new List<CatalogItem>();
            DataContext = this;

            InitializeComponent();

            _packageItem.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_packageItem.Package))
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
                else if (e.PropertyName == nameof(_packageItem.PackageVersion))
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
            };

            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            BranchesView.SelectionChanged += PackageFilter_SelectionChanged;
            ConfigurationsView.SelectionChanged += PackageFilter_SelectionChanged;
            TargetsView.SelectionChanged += PackageFilter_SelectionChanged;
            VersionsView.SelectionChanged += PackageVersions_SelectionChanged;

            Loaded += Dialog_Loaded;
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);

            try
            {
                _installedPackages.Clear();
                _availablePackages.Clear();
                UpdateCatalog();

                IsCatalogLoading = true;
                ResetServerSelection();

                _activeProject = TwinpackUtils.ActiveProject(_context.Dte);

                var projectItemAdapter = _activeProject?.Object as dynamic; // TwinCAT.XAE.Automation.TcProjectItemAdapter
                _libraryManager = projectItemAdapter?.LookupChild("References") as ITcPlcLibraryManager;

                await LoadPlcConfigAsync(Token);

                if (!IsConfigured)
                {
                    IsBrowsingInstalledPackages = false;
                    IsBrowsingAvailablePackages = true;
                    await LoadAvailablePackagesAsync("", Token);
                    await LoadInstalledPackagesAsync(Token);
                }
                else
                {
                    IsBrowsingInstalledPackages = _plcConfig?.Packages?.Any() == true;
                    IsBrowsingAvailablePackages = !IsBrowsingInstalledPackages;
                    await LoadInstalledPackagesAsync(Token);
                    await LoadAvailablePackagesAsync("", Token);
                }

                UpdateCatalog();
            }
            catch (Exception ex)
            {
                IsBrowsingAvailablePackages = true;
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsInitializing = false;
                IsCatalogLoading = false;
                IsCatalogEnabled = true;
                IsPackageVersionPanelEnabled = _plcConfig != null;
                IsUpdateAvailable = _packageServers.Any(x => (x as TwinpackServer)?.IsClientUpdateAvailable == true);
            }
        }

        public async Task LoadPlcConfigAsync(CancellationToken cancellationToken)
        {
            if (_activeProject != null)
            {
                try
                {
                    _isLoadingPlcConfig = true;
                    await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    var config = ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));
                    if (config != null)
                    {
                        _plcConfig = ConfigPlcProjectFactory.MapPlcConfigToPlcProj(config, _activeProject);
                    }
                    else
                    {
                        _plcConfig = await ConfigPlcProjectFactory.CreateAsync(_context.Solution, _activeProject, _packageServers.Where(x => x.Connected), cancellationToken);
                    }

                    IsCreateConfigVisible = config == null || _plcConfig == null;
                    IsMigrateConfigVisible = config != null && _plcConfig?.Packages?.Any() == false && _plcConfig?.Frameworks?.Zeugwerk?.References?.Any() == true;
                    IsConfigured = _plcConfig != null;
                }
                catch (Exception ex)
                {
                    IsCreateConfigVisible = true;
                    IsMigrateConfigVisible = false;
                    IsConfigured = false;
                    _plcConfig = null;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
                finally
                {
                    _isLoadingPlcConfig = false;
                }
            }
            else
            {
                _isLoadingPlcConfig = false;
                IsCreateConfigVisible = false;
                IsMigrateConfigVisible = false;
                IsConfigured = false;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public void EditServersButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PackagingServerDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();

            if (dialog.DialogResult == true)
            {
                ResetServerSelection();
                Reload(sender, e);
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void UninstallPackageButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _semaphoreAction.Wait();
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Uninstalling package");

                _context.Dte.ExecuteCommand("File.SaveAll");

                await UninstallPackageAsync(Token);

                IsNewReference = true;
                InstalledPackageVersion = null;

                // update config
                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig, Token);
                await LoadInstalledPackagesAsync(Token);
                UpdateCatalog();

                _logger.Info($"Successfully removed {_packageItem.PackageVersion.Name} from {_activeProject.Name} references");
                _logger.Info("Finished\n");

            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                _semaphoreAction.Release();
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void AddPackageButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _semaphoreAction.Wait();
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Adding package");

                _context.Dte.ExecuteCommand("File.SaveAll");

                // todo: overwrite all options
                var options = new List<AddPlcLibraryOptions> { Options };

                await AddOrUpdatePackageAsync(new List<PackageVersionGetResponse> { _packageItem.PackageVersion }, options, showLicenseDialog: true, cancellationToken: Token);
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == _packageItem.PackageVersion.Name);
                _packageItem.Package = _packageItem.PackageVersion;
                _packageItem.PackageVersion = _packageItem.PackageVersion;

                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig, Token);
                await LoadInstalledPackagesAsync(Token);
                UpdateCatalog();

                _logger.Info($"Successfully added {_packageItem.PackageVersion.Name} to {_activeProject.Name} references");
                _logger.Info("Finished\n");

            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _packageItem.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                _semaphoreAction.Release();
            }
        }

        public void UpdatePackageButton_Click(object sender, RoutedEventArgs e)
        {
            AddPackageButton_Click(sender, e);
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void RestoreAllPackageButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _semaphoreAction.Wait();
                IsCatalogEnabled = false;
                IsPackageVersionPanelEnabled = false;

                var locked = await _semaphorePackages.WaitAsync(10000, Token);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Restoring all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");
                var packageVersions = _installedPackages.Select(x => x.IsPlaceholder ? new PackageVersionGetResponse(x.Installed) { Version = null } : x.Installed).ToList();
                var options = _plcConfig.Packages.Where(x => packageVersions.Any(y => x.Name == y.Name) == true).Select(x => x.Options);
                await AddOrUpdatePackageAsync(packageVersions, options, showLicenseDialog: false, cancellationToken: Token);

                var item = _installedPackages.Where(x => x.Name == _packageItem.PackageVersion.Name).FirstOrDefault();
                if (item != null)
                {
                    _packageItem.Package = item.Update;
                    _packageItem.PackageVersion = item.Update;
                    _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Update.Name);
                }

                _context.Dte.ExecuteCommand("File.SaveAll");
                _logger.Info($"Successfully restored all references");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _packageItem.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _semaphorePackages.Release();
            }

            try
            {
                var config = await WritePlcConfigToConfigAsync(_plcConfig, Token);
                await LoadInstalledPackagesAsync(Token);
                SelectPackageVersionFilter(_packageItem.PackageVersion);
                UpdateCatalog();

                _logger.Info("Finished\n");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                IsCatalogEnabled = true;
                _semaphoreAction.Release();
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void UpdateAllPackageButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            IEnumerable<CatalogItem> items = new List<Models.CatalogItem>();
            try
            {
                _semaphoreAction.Wait();

                IsPackageVersionPanelEnabled = false;
                IsCatalogEnabled = false;

                var locked = await _semaphorePackages.WaitAsync(10000, Token);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Updating all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");
                items = _installedPackages.Where(x => x.IsUpdateable);

                var packageVersions = items.Select(x => x.IsPlaceholder ? new PackageVersionGetResponse(x.Update) { Version = null} : x.Update).ToList();
                var options = _plcConfig.Packages.Where(x => packageVersions.Any(y => x.Name == y.Name) == true).Select(x => x.Options);
                await AddOrUpdatePackageAsync(packageVersions, options, showLicenseDialog: false, cancellationToken: Token);

                var item = items.Where(x => x.Name == _packageItem.PackageVersion.Name).FirstOrDefault();
                if (item != null)
                {
                    _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Update.Name);
                    _packageItem.Package = item.Update;
                    _packageItem.PackageVersion = item.Update;
                }
                _context.Dte.ExecuteCommand("File.SaveAll");
                _logger.Info($"Successfully updated references to their latest version");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _packageItem.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _semaphorePackages.Release();
            }

            try
            {
                var config = await WritePlcConfigToConfigAsync(_plcConfig, cancellationToken: Token);
                await LoadInstalledPackagesAsync(cancellationToken: Token);
                UpdateCatalog();

                _logger.Info("Finished\n");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                IsCatalogEnabled = true;
                _semaphoreAction.Release();
            }
        }

        public void ShowUpdateablePackages_Click(object sender, RoutedEventArgs e)
        {
            IsBrowsingAvailablePackages = false;
            IsBrowsingUpdatablePackages = true;
            IsBrowsingInstalledPackages = false;
            UpdateCatalog();
        }

        public void ShowInstalledPackages_Click(object sender, RoutedEventArgs e)
        {
            IsBrowsingAvailablePackages = false;
            IsBrowsingUpdatablePackages = false;
            IsBrowsingInstalledPackages = true;
            UpdateCatalog();
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void ShowCatalog_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            IsBrowsingAvailablePackages = true;
            IsBrowsingUpdatablePackages = false;
            IsBrowsingInstalledPackages = false;

            try
            {
                if (_searchText != SearchTextBox.Text)
                {
                    await LoadAvailablePackagesAsync(SearchTextBox.Text, cancellationToken: Token);
                    _searchText = SearchTextBox.Text;
                }
                UpdateCatalog();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public void UpdateCatalog()
        {
            _installedPackages.Sort((x, y) => string.Compare(x.DisplayName, y.DisplayName));
            _availablePackages.Sort((x, y) => string.Compare(x.DisplayName, y.DisplayName));

            // synchronize the list of installed packages with the list of available packages
            var zipped =
            _availablePackages.GroupJoin(_installedPackages,
                item1 => item1.Name,
                item2 => item2.Name, (item1, matchingItems) => new { Available = item1, Installed = matchingItems.FirstOrDefault() }).
            Union(
            _installedPackages.GroupJoin(_availablePackages,
                item2 => item2.Name,
                item1 => item1.Name, (item2, matchingItems) => new { Available = matchingItems.FirstOrDefault(), Installed = item2 })
            );

            foreach (var package in zipped)
            {
                if (package.Installed != null && package.Available != null)
                {
                    package.Available.Installed = package.Installed.Installed;
                    package.Available.Update = package.Installed.Update;
                    package.Available.IsPlaceholder = package.Installed.IsPlaceholder;
                }
            }

            var text = SearchTextBox.Text;
            if (IsBrowsingAvailablePackages)
            {
                Catalog = _availablePackages.Where(x =>
                     x.DisplayName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                IsCatalogLoading = IsFetchingAvailablePackages || _isLoadingPlcConfig;
            }
            else if (IsBrowsingInstalledPackages)
            {
                Catalog = _installedPackages.Where(x =>
                     x.DisplayName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                IsCatalogLoading = IsFetchingInstalledPackages || _isLoadingPlcConfig;
            }
            else if (IsBrowsingUpdatablePackages)
            {
                Catalog = _installedPackages.Where(x => x.IsUpdateable &&
                    (x.DisplayName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0)).ToList();
                IsCatalogLoading = IsFetchingInstalledPackages || _isLoadingPlcConfig;
            }

            IsUpdateAllVisible = IsBrowsingUpdatablePackages && Catalog.Any();
            IsRestoreAllVisible = IsBrowsingInstalledPackages && Catalog.Any();

            // remap catalogItem
            if(_catalogItem != null)
            {
                _catalogItem = Catalog.FirstOrDefault(x => x.Name == _catalogItem.Name);
                if (_catalogItem?.Name == null || (_catalogItem.Name != _packageItem.Package.Name || _catalogItem.Name != _packageItem.PackageVersion.Name))
                {
                    InstalledPackageVersion = null;
                    _packageItem.Invalidate();
                }
                else if (_catalogItem != null)
                {
                    InstalledPackageVersion = _catalogItem.IsPlaceholder ? _catalogItem.InstalledVersion + "*" : _catalogItem.InstalledVersion;
                }
            }
        }

        public async Task UninstallPackageAsync(CancellationToken cancellationToken)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_packageItem.PackageVersion.Name == null)
                throw new Exception("No packages is selected that could be uninstalled!");

            _context.Dte.ExecuteCommand("File.SaveAll");
            TwinpackUtils.CloseAllPackageRelatedWindows(_context.Dte, _packageItem.PackageVersion);
            TwinpackUtils.RemoveReference(_libraryManager, _packageItem.PackageVersion.Title);
            _context.Dte.ExecuteCommand("File.SaveAll");

            if (UninstallDeletes)
            {
                _logger.Info($"Uninstalling package {_packageItem.PackageVersion.Name} from system ...");
                TwinpackUtils.UninstallReference(_libraryManager, _packageItem.PackageVersion, cancellationToken);
            }

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != _packageItem.PackageVersion.Name).ToList();
            var p = _availablePackages.FirstOrDefault(x => x.Name == _packageItem.PackageVersion.Name);
            if (p != null)
            {
                p.Installed = null;
            }

            _installedPackages.RemoveAll(x => x.Name == _packageItem.PackageVersion.Name);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public bool IsLicenseDialogRequired(PackageVersionGetResponse packageVersion, bool showLicenseDialogHint, HashSet<string> shownLicenses)
        {
            var licenseId = TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText);
            return (ForceShowLicense || (showLicenseDialogHint && !TwinpackUtils.IsPackageInstalled(_libraryManager, packageVersion.DistributorName, packageVersion.Title))) &&
                   (!string.IsNullOrEmpty(packageVersion.LicenseBinary) || (!string.IsNullOrEmpty(packageVersion.LicenseTmcBinary) && (ForceShowLicense || !shownLicenses.Contains(licenseId))));
        }

        public HashSet<string> ShowLicensesIfNeeded(IEnumerable<PackageVersionGetResponse> packageVersions, HashSet<string> knownLicenseIds, bool showLicenseDialog, HashSet<string> shownLicenseIds = null)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            shownLicenseIds = shownLicenseIds ?? (ForceShowLicense ? new HashSet<string>() : new HashSet<string>(knownLicenseIds));

            // todo: flatten dependencies and versions and iterate over this
            foreach (var packageVersion in packageVersions)
            {
                var licenseId = TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText);
                if (!shownLicenseIds.Any(x => x == licenseId) && IsLicenseDialogRequired(packageVersion, showLicenseDialog, shownLicenseIds))
                {
                    var licenseDialog = new LicenseWindow(_libraryManager, packageVersion);
                    if (licenseDialog.ShowLicense() == false)
                    {
                        throw new Exception($"License for {packageVersion.Name} was declined");
                    }

                    if (licenseId != null)
                        shownLicenseIds.Add(licenseId);
                }

                if (packageVersion.Dependencies != null)
                {
                    shownLicenseIds = ShowLicensesIfNeeded(packageVersion.Dependencies, knownLicenseIds, showLicenseDialog, shownLicenseIds);
                }
            }

            return shownLicenseIds;
        }

        public async Task AddOrUpdatePackageAsync(List<PackageVersionGetResponse> packageVersions, IEnumerable<AddPlcLibraryOptions> options, bool showLicenseDialog = true, CancellationToken cancellationToken = default)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (packageVersions.Any(x => x.Name == null) == true)
                throw new Exception("Invalid package(s) should be added or updated!");

            var cachePath = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
            var knownLicenseIds = TwinpackUtils.KnownLicenseIds();

            // license handling
            ShowLicensesIfNeeded(packageVersions, knownLicenseIds, showLicenseDialog);
            TwinpackUtils.CopyLicenseTmcIfNeeded(packageVersions, knownLicenseIds);



            // download packages and close Library Manager and all windows that are related to the library. These windows cause race conditions
            var downloadedPackageVersions = new List<PackageVersionGetResponse>();
            foreach (var packageVersion in packageVersions)
            {
                packageVersion.Dependencies = await TwinpackUtils.ResolvePackageDependenciesAsync(packageVersion, _packageServers, cancellationToken);
                downloadedPackageVersions = await TwinpackUtils.DownloadPackageVersionAndDependenciesAsync(_libraryManager, packageVersion, _packageServers.Where(x => x.Connected), downloadedPackageVersions, forceDownload: ForcePackageVersionDownload, cachePath: cachePath, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                TwinpackUtils.CloseAllPackageRelatedWindows(_context.Dte, packageVersion);
                TwinpackUtils.RemoveReference(_libraryManager, packageVersion.Title);
            }

            // install packages
            TwinpackUtils.InstallPackageVersions(_libraryManager, downloadedPackageVersions, cachePath: cachePath);
            cancellationToken.ThrowIfCancellationRequested();

            // add references
            TwinpackUtils.AddReferences(_libraryManager, packageVersions, options);
            cancellationToken.ThrowIfCancellationRequested();
            IsNewReference = false;

            // update config
            for (int i=0; i<packageVersions.Count(); i++)
            {
                var packageVersion = packageVersions.ElementAt(i);
                var option = options.ElementAt(i);
                var parameters = _plcConfig.Packages.FirstOrDefault(x => x.Name == packageVersion.Name)?.Parameters;

                _installedPackages.RemoveAll(x => string.Equals(x.Name, packageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                var packagesCopy = _plcConfig.Packages.ToList();
                var packageIndex = packagesCopy.FindIndex(x => x.Name == packageVersion.Name);
                var package = new ConfigPlcPackage
                {
                    Name = packageVersion.Name,
                    Repository = packageVersion.Repository,
                    Branch = packageVersion.Branch,
                    Configuration = packageVersion.Configuration,
                    Target = packageVersion.Target,
                    Version = packageVersion.Version,
                    DistributorName = packageVersion.DistributorName,
                    Options = option,
                    Parameters = parameters
                };

                if (packageIndex >= 0)
                {
                    packagesCopy[packageIndex] = package;
                    _plcConfig.Packages = packagesCopy;
                }
                else
                {
                    _plcConfig.Packages = _plcConfig.Packages.Append(package).ToList();
                }


                if (option?.AddDependenciesAsReferences == true)
                {
                    foreach (var dependency in packageVersion.Dependencies ?? new List<PackageVersionGetResponse>())
                    {
                        _installedPackages.RemoveAll(x => string.Equals(x.Name, dependency.Name, StringComparison.InvariantCultureIgnoreCase));
                        var dependencyParameters = _plcConfig.Packages.FirstOrDefault(x => x.Name == dependency.Name)?.Parameters;

                        packagesCopy = _plcConfig.Packages.ToList();
                        packageIndex = packagesCopy.FindIndex(x => x.Name == dependency.Name);
                        package = new ConfigPlcPackage
                        {
                            Name = dependency.Name,
                            Repository = dependency.Repository,
                            Branch = dependency.Branch,
                            Configuration = dependency.Configuration,
                            Target = dependency.Target,
                            Version = dependency.Version,
                            DistributorName = dependency.DistributorName,
                            Options = option,
                            Parameters = dependencyParameters
                        };

                        if (packageIndex >= 0)
                        {
                            packagesCopy[packageIndex] = package;
                            _plcConfig.Packages = packagesCopy;
                        }
                        else
                        {
                            _plcConfig.Packages = _plcConfig.Packages.Append(package).ToList();
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public async Task<Models.Config> WritePlcConfigToConfigAsync(ConfigPlcProject plcConfig, CancellationToken cancellationToken)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var config = ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));

            if (config != null)
            {
                ConfigFactory.UpdatePlcProject(config, plcConfig);
                ConfigFactory.Save(config);
            }
            else
            {
                _logger.Warn($"The solution doesn't have a package configuration");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return config;
        }

        public void ShowLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseDialog = new LicenseWindow(null, _packageItem.PackageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public void ShowLicenseTmcButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseDialog = new LicenseWindow(null, _packageItem.PackageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
        /*
        public async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true);

                if (!_twinpackServer.LoggedIn)
                {
                    await _auth.LoginAsync(onlyTry: false, cancellationToken: Token);
                }
                else
                {
                    _auth.Logout();
                }
            }
            catch (Exceptions.LoginException ex)
            {
                MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsUpdateAvailable = _twinpackServer.IsClientUpdateAvailable;
                btnLogin.Text = _twinpackServer.LoggedIn ? "Logout" : "Login";
                btnRegister.Visibility = _twinpackServer.LoggedIn ? Visibility.Collapsed : Visibility.Visible;
            }

            try
            {
                _availablePackages.Clear();
                _twinpackServer.InvalidateCache();
                UpdateCatalog();
                await LoadAvailablePackagesAsync("", Token);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
        */
        private async Task LoadFirstPackageVersionsPageAsync(CancellationToken cancellationToken)
        {
            await LoadNextPackageVersionsPageAsync(true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task LoadNextPackageVersionsPageAsync(bool reset = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var branch = BranchesView.SelectedItem as string;
                var configuration = ConfigurationsView.SelectedItem as string;
                var target = TargetsView.SelectedItem as string;

                if (reset)
                    _currentPackageVersionsPage = 1;

                var results = await _packageItem.PackageServer.GetPackageVersionsAsync(
                    new PlcLibrary {
                        DistributorName = _packageItem.Package.DistributorName,
                        Name = _packageItem.Package.Name
                    },
                    branch,
                    configuration,
                    target,
                    _currentPackageVersionsPage,
                    _itemsPerPage,
                    cancellationToken);

                IsPackageVersionsAvailable = results.Item2;

                if (reset)
                {
                    Versions = new List<PlcVersion>()
                    {
                        new PlcVersion
                        {
                            Version = null,
                            VersionDisplayText = "Latest " + ( (branch == "main" || _packageItem.Package.Branches.Count == 1) && (results?.Item1.Any() == true) ? "(" + results.Item1.First().Version + ")" : "*")
                        }
                    };

                    // add already installed item to the list if the branch, target and configuration of the 
                    // installed package is selected
                    if (_catalogItem?.Installed != null && 
                        !results.Item1.Any(x => x.Version == _catalogItem?.Installed.Version) &&
                         branch == _catalogItem?.Installed.Branch &&
                         configuration == _catalogItem?.Installed.Configuration &&
                         target == _catalogItem?.Installed.Target)
                    {
                        Versions.Insert(1, new PlcVersion
                        {
                            Version = _catalogItem.Installed.Version,
                            VersionDisplayText = _catalogItem.Installed.Version
                        });
                    }

                    VersionsView.SelectedIndex = -1;
                }

                Versions = Versions.Concat(results.Item1.Select(x => new PlcVersion { Version = x.Version, VersionDisplayText = x.Version })).ToList();
                _currentPackageVersionsPage++;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task LoadAvailablePackagesAsync(string text = "", CancellationToken cancellationToken = default)
        {
            await LoadNextCatalogPageAsync(text, true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task LoadNextCatalogPageAsync(string text = "", bool reset = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var locked = await _semaphorePackages.WaitAsync(10000, cancellationToken);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                IsFetchingAvailablePackages = true;

                if (reset)
                    _currentCatalogPage = 1;

                // todo: parallelize
                var packageServers = PackagingServersComboBox.SelectedIndex == 0 ? _packageServers : new List<IPackageServer> { _packageServers.ElementAt(PackagingServersComboBox.SelectedIndex - 1) };
                foreach (var packageServer in packageServers.Where(x => x.Connected))
                {
                    var results = await packageServer.GetCatalogAsync(text, _currentCatalogPage, _itemsPerPage, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    IsAvailablePackageAvailable = results.Item2;
                    foreach (var item in results.Item1)
                    {
                        if (!_availablePackages.Any(x => x.Name == item.Name))
                        {
                            _availablePackages.Add(new CatalogItem(packageServer, item));
                        }
                    }
                }
                _currentCatalogPage++;
            }
            catch (Exception ex)
            {
                // Handle API request error
                Console.WriteLine($"Error fetching items: {ex.Message}");
            }
            finally
            {
                _semaphorePackages.Release();
                IsFetchingAvailablePackages = false;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        private async Task LoadInstalledPackagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsFetchingInstalledPackages = true;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                var locked = await _semaphorePackages.WaitAsync(10000, cancellationToken);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                if (_plcConfig != null)
                {
                    _installedPackages.RemoveAll(x => x.Name == null);
                    foreach (var item in _plcConfig.Packages)
                    {
                        CatalogItem catalogItem = new CatalogItem(item);

                        var packageServers = _packageServers.Where(x => x.Connected);
                        var lastPackageServer = packageServers.Last();
                        foreach (var packageServer in packageServers)
                        {
                            if (_installedPackages.Any(x =>
                                    string.Equals(x.DistributorName, item.DistributorName, StringComparison.InvariantCultureIgnoreCase) &&
                                    string.Equals(x.Name,item.Name, StringComparison.InvariantCultureIgnoreCase)))
                                break;

                            catalogItem.PackageServer = packageServer;

                            // try to get the installed package, if we can't find it at least try to resolve it
                            var packageVersion = await packageServer.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name, Version = item.Version },
                                                                                              item.Branch, item.Configuration, item.Target,
                                                                                              cancellationToken: cancellationToken);

                            if(packageVersion != null && item.Version == null) 
                            {
                                var effectiveVersion = TwinpackUtils.ResolveEffectiveVersion(_libraryManager, packageVersion.Title);
                                packageVersion = await packageServer.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name, Version = effectiveVersion },
                                                                                                  item.Branch, item.Configuration, item.Target,
                                                                                                  cancellationToken: cancellationToken);
                            }

                            var packageVersionLatest = await packageServer.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name },
                                                                                              item.Branch, item.Configuration, item.Target,
                                                                                              cancellationToken: cancellationToken);

                            // force the packageVersion references version even if the version was not found
                            if (packageVersion.Name != null)
                            {
                                catalogItem = new CatalogItem(packageServer, packageVersion);
                                catalogItem.Installed = packageVersion;
                                catalogItem.IsPlaceholder = item.Version == null;
                            }

                            // a package might be updateable but not available on Twinpack
                            if (packageVersionLatest.Name != null)
                            {
                                catalogItem.Update = packageVersionLatest;
                                catalogItem.PackageServer = packageServer;
                            }

                            var packageName = catalogItem.Name ?? packageVersionLatest.Name;
                            if (packageName == null)
                            {
                                _installedPackages.Add(catalogItem);
                                _logger.Warn($"Package {item.Name} (version: {item.Version}, distributor: {item.DistributorName}) referenced in the configuration can not be found on any package server");
                            }
                            else if(packageVersionLatest.Name != null || packageServer == lastPackageServer)
                            {
                                _installedPackages.RemoveAll(x => !string.IsNullOrEmpty(x.Name) && x.Name == catalogItem.Name);
                                _installedPackages.Add(catalogItem);
                                _logger.Info($"Package {item.Name} (version: {item.Version}, distributor: {item.DistributorName}) located on {packageServer.UrlBase}");
                            }
                        }
                    }
                }

                IsNewReference = _packageItem.PackageVersion?.Name == null || !_installedPackages.Any(x => x.Name == _packageItem.PackageVersion.Name);
                InstalledPackagesCount = _installedPackages.Count();
                UpdateablePackagesCount = _installedPackages.Where(x => x.IsUpdateable).Count();
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _packageItem.Invalidate();
                throw ex;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _semaphorePackages.Release();
                IsFetchingInstalledPackages = false;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void ShowMoreAvailablePackagesButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            await LoadNextCatalogPageAsync(SearchTextBox.Text, cancellationToken: Token);
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void ShowMoreAvailableVersionsButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            if (_packageItem.PackageVersion.Name == null)
                return;

            try
            {
                await LoadNextPackageVersionsPageAsync(reset: false, cancellationToken: Token);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
            }
        }

        private void SelectPackageVersionFilter(Models.PackageVersionGetResponse installed)
        {
            ConfigurationsView.SelectedIndex = -1;
            BranchesView.SelectedIndex = string.IsNullOrEmpty(installed?.Branch) ? 0 : _packageItem.Package.Branches?.FindIndex(x => x == installed.Branch) ?? -1;
            TargetsView.SelectedIndex = string.IsNullOrEmpty(installed?.Target) ? 0 : _packageItem.Package.Targets?.FindIndex(x => x == installed.Target) ?? -1;
            ConfigurationsView.SelectedIndex = string.IsNullOrEmpty(installed?.Configuration) ? 0 : _packageItem.Package.Configurations?.FindIndex(x => x == installed.Configuration) ?? -1;
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            _catalogItem = (sender as ListView).SelectedItem as Models.CatalogItem;
            if (_catalogItem == null)
                return;

            InstalledPackageVersion = _catalogItem.IsPlaceholder ? _catalogItem.InstalledVersion + "*" : _catalogItem.InstalledVersion;

            try
            {
                var locked = await _semaphorePackages.WaitAsync(10000, Token);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                IsPackageLoading = true;
                IsPackageVersionLoading = true;

                // check if the plc already contains the selected package
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == _catalogItem.Name);

                PackageGetResponse package;
                if (_packageConfig != null)
                    package = await _catalogItem.PackageServer.GetPackageAsync(_packageConfig.DistributorName, _catalogItem.Name, Token);
                else
                    package = await _catalogItem.PackageServer.GetPackageAsync(_catalogItem.DistributorName, _catalogItem.Name, Token);

                _packageItem.PackageServer = _catalogItem.PackageServer;
                _packageItem.Package = package;
                Options = _packageConfig?.Options ?? new AddPlcLibraryOptions();

                BranchesView.Visibility = _packageItem.Package?.Branches.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                TargetsView.Visibility = _packageItem.Package?.Targets.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                ConfigurationsView.Visibility = _packageItem.Package?.Configurations.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;

                SelectPackageVersionFilter(_catalogItem?.Installed);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _packageItem.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _semaphorePackages.Release();
                IsPackageLoading = false;
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void PackageFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            if (_packageItem.Package.Name == null ||
                BranchesView.SelectedIndex < 0 || TargetsView.SelectedIndex < 0 || ConfigurationsView.SelectedIndex < 0)
                return;

            try
            {
                IsPackageLoading = _packageItem.Package.Name != _packageItem.PackageVersion.Name;
                IsPackageVersionLoading = IsPackageLoading;

                await LoadFirstPackageVersionsPageAsync(Token);

                if(Versions?.Any(x => x.Version != null) == true)
                {
                    var index = Versions?.FindIndex(x => x.Version == _catalogItem?.Installed?.Version) ?? -1;
                    if (_catalogItem?.IsPlaceholder == true)
                        index = 0;
                    else if (index < 0 && _catalogItem?.Installed != null)
                        index = Versions.Count > 1 ? 1 : 0;
                    else if (index == 0 && _catalogItem?.Installed == null)
                        index = 1;

                    VersionsView.IsEnabled = true;
                    VersionsView.SelectedIndex = index;
                }
                else
                {
                    VersionsView.IsEnabled = false;
                    VersionsView.SelectedIndex = -1;
                }
            }
            catch (OperationCanceledException ex)
            {
                _packageItem.Invalidate();
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageLoading = false;
                IsPackageVersionLoading = false;
            }
        }
#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                IsPackageLoading = _packageItem.Package.Name != _packageItem.PackageVersion.Name;
                var item = (sender as ComboBox).SelectedItem as PlcVersion;

                IsPackageVersionLoading = IsPackageLoading || item?.Version != _packageItem.PackageVersion.Version;

                if (item != null)
                {
                    _packageItem.PackageVersion = await _packageItem.PackageServer.GetPackageVersionAsync(
                        new PlcLibrary
                        {
                            DistributorName = _packageItem.Package.DistributorName, 
                            Name = _packageItem.Package.Name, 
                            Version = item.Version 
                        }, 
                        BranchesView.SelectedItem as string,
                        ConfigurationsView.SelectedItem as string, 
                        TargetsView.SelectedItem as string,
                        cancellationToken: Token);
                }

                if ((sender as ComboBox).SelectedIndex == 0)
                    _packageItem.PackageVersion.Version = null;

                IsNewReference = _packageItem.PackageVersion.Name == null || !_installedPackages.Any(x => x.Name == _packageItem.Package.Name);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _packageItem.Invalidate();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageLoading = false;
                IsPackageVersionLoading = false;
            }
        }

        void ResetServerSelection()
        {
            PackagingServersComboBox.SelectionChanged -= _packageServerChange;

            PackagingServersComboBox.Items.Clear();
            PackagingServersComboBox.Items.Add("All repositories");
            _packageServers.ForEach(x => PackagingServersComboBox.Items.Add(x.Name));

            PackagingServersComboBox.SelectedIndex = 0;
            PackagingServersComboBox.SelectionChanged += _packageServerChange;
        }

        public void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            Reload(sender, e); 
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void Reload(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                IsInitializing = true;
                IsCatalogLoading = true;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Reloading catalog");

                _catalogItem = null;
                IsPackageLoading = false;

                _packageServers.InvalidateCache();
                _installedPackages.Clear();
                _availablePackages.Clear();
                UpdateCatalog();
                _context.Dte.ExecuteCommand("File.SaveAll");

                await LoadPlcConfigAsync(Token);
                await LoadInstalledPackagesAsync(Token);
                await LoadAvailablePackagesAsync(SearchTextBox.Text, Token);
                UpdateCatalog();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = _plcConfig != null;
                IsCatalogLoading = false;
                IsCatalogEnabled = true;
                IsInitializing = false;
            }
        }

        public void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            IsFetchingAvailablePackages = false;
            IsFetchingInstalledPackages = false;
            IsCatalogLoading = false;
            IsInitializing = false;
            IsPackageLoading = false;
            IsPackageVersionLoading = false;
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void CreateConfig_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden

        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info($"Creating package configuration");

                var config = await ConfigFactory.CreateFromSolutionAsync(_context.Solution, _packageServers.Where(x => x.Connected), cancellationToken: Token);

                if (config == null)
                {
                    throw new Exception("Generating the configuration file failed, please create the configuration file manually!");
                }
                else
                {
                    _logger.Info($"Detected {config?.Projects?.SelectMany(x => x.Plcs)?.SelectMany(x => x.Packages)?.Count()} Twinpack packages and {config?.Projects?.SelectMany(x => x.Plcs)?.SelectMany(x => x.References)?.Count()} other references");

                    IsCreateConfigVisible = false;
                    var path = Models.ConfigFactory.Save(config);

                    if (MessageBoxResult.Yes == MessageBox.Show($"The configuration file was successfully created " +
                        $"in {config.FilePath} for your TwinCAT solution, do you want to " +
                        $"review and/or edit it?", "Configuration", MessageBoxButton.YesNo))
                    {
                        var process = new System.Diagnostics.Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "notepad.exe",
                                Arguments = path,
                                UseShellExecute = true
                            }
                        };

                        process.Start();
                        process.WaitForExit();
                    }

                    await LoadPlcConfigAsync(Token);
                    await LoadInstalledPackagesAsync(Token);
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsCatalogEnabled = true;
                IsPackageVersionPanelEnabled = _plcConfig != null;
            }
        }

        public void ShowProjectUrl_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(_packageItem.PackageVersion.ProjectUrl);
        }

        public void UpdateAvailableButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Zeugwerk/Twinpack");
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden

        {
            try
            {
                var text = ((TextBox)sender).Text;
                UpdateCatalog();

                // this will only add additional items
                if (IsBrowsingAvailablePackages)
                {
                    _searchText = text;
                    await Task.Delay(250);

                    if (_searchText == text)
                        await LoadAvailablePackagesAsync(text, Token);
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
    }
}
