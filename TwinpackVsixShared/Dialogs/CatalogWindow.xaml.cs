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
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Models.Api;
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

        private Config _config;
        private ConfigPlcProject _plcConfig;
        private ConfigPlcPackage _packageConfig;
        private IEnumerable<PackageItem> _catalog = new List<PackageItem>();

        private List<PlcVersion> _packageVersions = new List<PlcVersion>();
        private SemaphoreSlim _semaphorePackages = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _semaphoreAction = new SemaphoreSlim(1, 1);

        private PackageItem _catalogItem = null;

        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 10;

        private bool _isUpdateAvailable = false;
        private bool _isLoadingPlcConfig = false;
        private bool _isFetchingInstalledPackages = false;
        private bool _isPackageVersionsAvailable = false;
        private bool _isFetchingAvailablePackages = false;

        private string _searchTerm = "";
        public TwinpackService _twinpack;

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
        private bool _addDependencies;
        private bool _forceShowLicense;
        private bool _uninstallDeletes;

        private int _installedPackagesCount;
        private int _updateablePackagesCount;

        public string Version
        {
            get
            {
                return _context?.Version;
            }
        }
        private CancellationToken Token
        {
            get
            {
                if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                    _cancellationTokenSource = new CancellationTokenSource();

                return _cancellationTokenSource.Token;
            }
        }

        public IEnumerable<PackageItem> Catalog
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
                return IsCatalogLoading || IsPackageLoading || IsPackageVersionLoading;
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
            get { return _catalogItem.Package; }
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
            get { return _catalogItem.PackageVersion; }
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

        
        public bool AddDependencies
        {
            get { return _addDependencies; }
            set
            {
                _addDependencies = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddDependencies)));
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
                _isRestoreAllEnabled = !_twinpack.HasUnknownPackages;
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
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers, new VisualStudio(_context.Dte, _context.Solution).AutomationInterface);

            _packageServerChange = new SelectionChangedEventHandler(Reload);

            IsInitializing = true;
            IsCatalogEnabled = true;
            InstalledPackagesCount = 0;
            UpdateablePackagesCount = 0;
            ForcePackageVersionDownload = true;
            Catalog = new List<PackageItem>();
            DataContext = this;

            InitializeComponent();

            _catalogItem.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_catalogItem.Package))
                    PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
                else if (e.PropertyName == nameof(_catalogItem.PackageVersion))
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
                _twinpack.InvalidateCache();
                await UpdateCatalogAsync();

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
                }
                else
                {
                    IsBrowsingInstalledPackages = _plcConfig?.Packages?.Any() == true;
                    IsBrowsingAvailablePackages = !IsBrowsingInstalledPackages;
                }

                await _twinpack.RetrieveAvailablePackagesAsync(_searchTerm, 10);
                await UpdateCatalogAsync();
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
                IsUpdateAvailable = _twinpack.IsClientUpdateAvailable;
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
                        _plcConfig = await ConfigPlcProjectFactory.CreateAsync(_context.Solution, _activeProject, _twinpack.PackageServers.Where(x => x.Connected), cancellationToken);
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
                    _config = null;
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

            _config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { _plcConfig } } } };
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

                await _twinpack.RemovePackageAsync(_catalogItem, uninstall: UninstallDeletes);

                IsNewReference = true;
                InstalledPackageVersion = null;

                // update config
                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig, Token);
                await UpdateCatalogAsync();

                _logger.Info($"Successfully removed {_catalogItem.PackageVersion.Name} from {_activeProject.Name} references");
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

                _catalogItem.Config.Options = Options;
                await AddOrUpdatePackageAsync(new List<PackageItem> { _catalogItem }, showLicenseDialog: true, cancellationToken: Token);
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == _catalogItem.PackageVersion.Name);
                _catalogItem.Package = _catalogItem.PackageVersion;
                _catalogItem.PackageVersion = _catalogItem.PackageVersion;

                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig, Token);
                await UpdateCatalogAsync();

                _logger.Info($"Successfully added {_catalogItem.PackageVersion.Name} to {_activeProject.Name} references");
                _logger.Info("Finished\n");

            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _catalogItem.Invalidate();
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
                var installedPackages = await _twinpack.RetrieveUsedPackagesAsync(_config, _searchTerm, token: Token);

                var packages = installedPackages.Select(x => new PackageItem { PackageVersion = x.Used, Config = x.Config }).ToList();
                await AddOrUpdatePackageAsync(packages, showLicenseDialog: false, cancellationToken: Token);

                installedPackages = await _twinpack.RetrieveUsedPackagesAsync(_config, _searchTerm, token: Token);
                var item = installedPackages.Where(x => x.Name == _catalogItem.PackageVersion.Name).FirstOrDefault();
                if (item != null)
                {
                    _catalogItem.Package = item.Update;
                    _catalogItem.PackageVersion = item.Update;
                    _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Update.Name);
                }

                _context.Dte.ExecuteCommand("File.SaveAll");
                _logger.Info($"Successfully restored all references");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _catalogItem.Invalidate();
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
                SelectPackageVersionFilter(_catalogItem.PackageVersion);
                await UpdateCatalogAsync();

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
                var installedPackages = await _twinpack.RetrieveUsedPackagesAsync(_config, _searchTerm, token: Token);

                var items = installedPackages.Where(x => x.IsUpdateable);
                var packages = items.Select(x => new PackageItem { PackageVersion = x.Update, Config = x.Config }).ToList();
                await AddOrUpdatePackageAsync(packages, showLicenseDialog: false, cancellationToken: Token);

                installedPackages = await _twinpack.RetrieveUsedPackagesAsync(_config, _searchTerm, token: Token);
                var item = installedPackages.Where(x => x.Name == _catalogItem.PackageVersion.Name).FirstOrDefault();
                if (item != null)
                {
                    _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Update.Name);
                    _catalogItem.Package = item.Update;
                    _catalogItem.PackageVersion = item.Update;
                }
                _context.Dte.ExecuteCommand("File.SaveAll");
                _logger.Info($"Successfully updated references to their latest version");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _catalogItem.Invalidate();
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
                await UpdateCatalogAsync();

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

        public async void ShowUpdateablePackages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsBrowsingAvailablePackages = false;
                IsBrowsingUpdatablePackages = true;
                IsBrowsingInstalledPackages = false;
                await UpdateCatalogAsync();
            }
            catch(Exception ex)
            {
                _logger.Error(ex.Message);
                _logger.Trace(ex);
            }

        }

        public async void ShowInstalledPackages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsBrowsingAvailablePackages = false;
                IsBrowsingUpdatablePackages = false;
                IsBrowsingInstalledPackages = true;
                await UpdateCatalogAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                _logger.Trace(ex);
            }
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
                if (_searchTerm != SearchTextBox.Text)
                {
                    await UpdateCatalogAsync(SearchTextBox.Text);
                    _searchTerm = SearchTextBox.Text;
                }
                else
                {
                    await UpdateCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async Task UpdateCatalogAsync(string searchText=null, int maxNewPackages = 0)
        {
            if (searchText == null)
                searchText = _searchTerm;

            var installedPackages = await _twinpack.RetrieveUsedPackagesAsync(_config, searchText, token: Token);
            var availablePackages = await _twinpack.RetrieveAvailablePackagesAsync(searchText, maxNewPackages, 5, Token);

            // synchronize the list of installed packages with the list of available packages
            var zipped =
            availablePackages.GroupJoin(installedPackages,
                item1 => item1.Name,
                item2 => item2.Name, (item1, matchingItems) => new { Available = item1, Installed = matchingItems.FirstOrDefault() }).
            Union(
            installedPackages.GroupJoin(availablePackages,
                item2 => item2.Name,
                item1 => item1.Name, (item2, matchingItems) => new { Available = matchingItems.FirstOrDefault(), Installed = item2 })
            );

            foreach (var package in zipped)
            {
                if (package.Installed != null && package.Available != null)
                {
                    package.Available.Used = package.Installed.Used;
                    package.Available.Update = package.Installed.Update;
                    package.Available.IsPlaceholder = package.Installed.IsPlaceholder;
                }
            }

            if (IsBrowsingAvailablePackages)
            {
                Catalog = availablePackages.Where(x =>
                     x.DisplayName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                IsCatalogLoading = _isLoadingPlcConfig;
            }
            else if (IsBrowsingInstalledPackages)
            {
                Catalog = installedPackages.Where(x =>
                     x.DisplayName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();
                IsCatalogLoading = _isLoadingPlcConfig;
            }
            else if (IsBrowsingUpdatablePackages)
            {
                Catalog = installedPackages.Where(x => x.IsUpdateable &&
                    (x.DisplayName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.DistributorName.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                     x.Name.IndexOf(searchText, StringComparison.InvariantCultureIgnoreCase) >= 0)).ToList();
                IsCatalogLoading = _isLoadingPlcConfig;
            }

            IsUpdateAllVisible = IsBrowsingUpdatablePackages && Catalog.Any();
            IsRestoreAllVisible = IsBrowsingInstalledPackages && Catalog.Any();

            // remap catalogItem
            if(_catalogItem != null)
            {
                _catalogItem = Catalog.FirstOrDefault(x => x.Name == _catalogItem.Name);
                if (_catalogItem?.Name == null || (_catalogItem.Name != _catalogItem.Package.Name || _catalogItem.Name != _catalogItem.PackageVersion.Name))
                {
                    InstalledPackageVersion = null;
                    _catalogItem.Invalidate();
                }
                else if (_catalogItem != null)
                {
                    InstalledPackageVersion = _catalogItem.IsPlaceholder ? _catalogItem.InstalledVersion + "*" : _catalogItem.InstalledVersion;
                }
            }
        }

        public bool IsLicenseDialogRequired(PackageVersionGetResponse packageVersion, bool showLicenseDialogHint, HashSet<string> shownLicenses)
        {
            var licenseId = TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText);
            return (ForceShowLicense || (showLicenseDialogHint && !TwinpackUtils.IsPackageInstalled(_libraryManager, packageVersion.DistributorName, packageVersion.Title))) &&
                   (!string.IsNullOrEmpty(packageVersion.LicenseBinary) || (!string.IsNullOrEmpty(packageVersion.LicenseTmcBinary) && (ForceShowLicense || !shownLicenses.Contains(licenseId))));
        }

        public HashSet<string> ShowLicensesIfNeeded(IEnumerable<PackageItem> packages, HashSet<string> knownLicenseIds, bool showLicenseDialog, HashSet<string> shownLicenseIds = null)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            shownLicenseIds = shownLicenseIds ?? (ForceShowLicense ? new HashSet<string>() : new HashSet<string>(knownLicenseIds));

            // todo: flatten dependencies and versions and iterate over this
            foreach (var package in packages)
            {
                var licenseId = TwinpackUtils.ParseLicenseId(package.PackageVersion.LicenseTmcText);
                if (!shownLicenseIds.Any(x => x == licenseId) && IsLicenseDialogRequired(package.PackageVersion, showLicenseDialog, shownLicenseIds))
                {
                    var licenseDialog = new LicenseWindow(_libraryManager, package.PackageVersion);
                    if (licenseDialog.ShowLicense() == false)
                    {
                        throw new Exception($"License for {package.PackageVersion.Name} was declined");
                    }

                    if (licenseId != null)
                        shownLicenseIds.Add(licenseId);
                }

                if (package.PackageVersion.Dependencies != null)
                {
                    shownLicenseIds = ShowLicensesIfNeeded(package.PackageVersion.Dependencies.Select(x => new PackageItem { PackageVersion = x }) , knownLicenseIds, showLicenseDialog, shownLicenseIds);
                }
            }

            return shownLicenseIds;
        }

        public async Task AddOrUpdatePackageAsync(List<PackageItem> packages, bool showLicenseDialog = true, CancellationToken cancellationToken = default)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // show licenses and wait for accept
            var knownLicenseIds = _twinpack.KnownLicenseIds();
            ShowLicensesIfNeeded(packages, knownLicenseIds, showLicenseDialog);

            // add packages
            await _twinpack.AddPackagesAsync(packages, new TwinpackService.AddPackageOptions { ForceDownload=ForcePackageVersionDownload, AddDependencies=AddDependencies }, cancellationToken);

            IsNewReference = false;
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
                var licenseDialog = new LicenseWindow(null, _catalogItem.PackageVersion);
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
                var licenseDialog = new LicenseWindow(null, _catalogItem.PackageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

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

                var results = await _catalogItem.PackageServer.GetPackageVersionsAsync(
                    new PlcLibrary {
                        DistributorName = _catalogItem.Package.DistributorName,
                        Name = _catalogItem.Package.Name
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
                            VersionDisplayText = "Latest " + ( (branch == "main" || _catalogItem.Package.Branches.Count == 1) && (results?.Item1.Any() == true) ? "(" + results.Item1.First().Version + ")" : "*")
                        }
                    };

                    // add already installed item to the list if the branch, target and configuration of the 
                    // installed package is selected
                    if (_catalogItem?.Used != null && 
                        !results.Item1.Any(x => x.Version == _catalogItem?.Used.Version) &&
                         branch == _catalogItem?.Used.Branch &&
                         configuration == _catalogItem?.Used.Configuration &&
                         target == _catalogItem?.Used.Target)
                    {
                        Versions.Insert(1, new PlcVersion
                        {
                            Version = _catalogItem.Used.Version,
                            VersionDisplayText = _catalogItem.Used.Version
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

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void ShowMoreAvailablePackagesButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            await _twinpack.RetrieveAvailablePackagesAsync(_searchTerm, 5);
            await UpdateCatalogAsync();
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void ShowMoreAvailableVersionsButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            if (_catalogItem.PackageVersion.Name == null)
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

        private void SelectPackageVersionFilter(Models.Api.PackageVersionGetResponse installed)
        {
            ConfigurationsView.SelectedIndex = -1;
            BranchesView.SelectedIndex = string.IsNullOrEmpty(installed?.Branch) ? 0 : _catalogItem.Package.Branches?.FindIndex(x => x == installed.Branch) ?? -1;
            TargetsView.SelectedIndex = string.IsNullOrEmpty(installed?.Target) ? 0 : _catalogItem.Package.Targets?.FindIndex(x => x == installed.Target) ?? -1;
            ConfigurationsView.SelectedIndex = string.IsNullOrEmpty(installed?.Configuration) ? 0 : _catalogItem.Package.Configurations?.FindIndex(x => x == installed.Configuration) ?? -1;
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            _catalogItem = (sender as ListView).SelectedItem as Models.PackageItem;
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

                _catalogItem.PackageServer = _catalogItem.PackageServer;
                _catalogItem.Config = _catalogItem.Config;
                _catalogItem.Package = package;
                _catalogItem.ProjectName = _activeProject.Name; // todo: fixme
                _catalogItem.PlcName = _activeProject.Name;
                Options = _packageConfig?.Options ?? new AddPlcLibraryOptions();

                BranchesView.Visibility = _catalogItem.Package?.Branches.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                TargetsView.Visibility = _catalogItem.Package?.Targets.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                ConfigurationsView.Visibility = _catalogItem.Package?.Configurations.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;

                SelectPackageVersionFilter(_catalogItem?.Used);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _catalogItem.Invalidate();
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
            if (_catalogItem.Package.Name == null ||
                BranchesView.SelectedIndex < 0 || TargetsView.SelectedIndex < 0 || ConfigurationsView.SelectedIndex < 0)
                return;

            try
            {
                IsPackageLoading = _catalogItem.Package.Name != _catalogItem.PackageVersion.Name;
                IsPackageVersionLoading = IsPackageLoading;

                await LoadFirstPackageVersionsPageAsync(Token);

                if(Versions?.Any(x => x.Version != null) == true)
                {
                    var index = Versions?.FindIndex(x => x.Version == _catalogItem?.Used?.Version) ?? -1;
                    if (_catalogItem?.IsPlaceholder == true)
                        index = 0;
                    else if (index < 0 && _catalogItem?.Used != null)
                        index = Versions.Count > 1 ? 1 : 0;
                    else if (index == 0 && _catalogItem?.Used == null)
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
                _catalogItem.Invalidate();
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
                IsPackageLoading = _catalogItem.Package.Name != _catalogItem.PackageVersion.Name;
                var item = (sender as ComboBox).SelectedItem as PlcVersion;

                IsPackageVersionLoading = IsPackageLoading || item?.Version != _catalogItem.PackageVersion.Version;

                if (item != null)
                {
                    _catalogItem.Config.Branch = BranchesView.SelectedItem as string;
                    _catalogItem.Config.Configuration = ConfigurationsView.SelectedItem as string;
                    _catalogItem.Config.Target = TargetsView.SelectedItem as string;
                    await _twinpack.ResolvePackageAsync(_catalogItem);
                }

                if ((sender as ComboBox).SelectedIndex == 0)
                    _catalogItem.PackageVersion.Version = null;

                IsNewReference = _catalogItem.PackageVersion.Name == null || 
                    !_twinpack.UsedPackages.Any(x => x.Name == _catalogItem.Package.Name);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _catalogItem.Invalidate();
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

            foreach(var packageServer in _twinpack.PackageServers)
                PackagingServersComboBox.Items.Add(packageServer.Name);

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

                _twinpack.InvalidateCache();
                await UpdateCatalogAsync();
                _context.Dte.ExecuteCommand("File.SaveAll");

                await LoadPlcConfigAsync(Token);
                await UpdateCatalogAsync(null, 10);
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

                var config = await ConfigFactory.CreateFromSolutionAsync(_context.Solution, _twinpack.PackageServers.Where(x => x.Connected), cancellationToken: Token);

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
                    await _twinpack.RetrieveUsedPackagesAsync(_config, _searchTerm, token: Token);
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
            Process.Start(_catalogItem.PackageVersion.ProjectUrl);
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
                await UpdateCatalogAsync();

                // this will only add additional items
                if (IsBrowsingAvailablePackages)
                {
                    _searchTerm = text;
                    await Task.Delay(250);

                    if (_searchTerm == text)
                        await UpdateCatalogAsync();
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
