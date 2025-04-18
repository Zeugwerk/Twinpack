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
using Microsoft.VisualStudio.Threading;
using NLog;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using Twinpack.Protocol;
using Twinpack.Configuration;
using System.Text.RegularExpressions;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl, INotifyPropertyChanged
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private CancellationTokenSource _cancellationTokenSource;

        public event PropertyChangedEventHandler PropertyChanged;
        SelectionChangedEventHandler _packageServerSelectionChanged;

        private PackageContext _context;

        private string _configFilePath;
        private ConfigPlcProject _plcConfig;

        private List<PlcVersion> _packageVersions = new List<PlcVersion>();
        private SemaphoreSlim _semaphoreAction = new SemaphoreSlim(1, 1);
        private PackageItem _catalogItem = new PackageItem();

        private int _currentPackageVersionsPage = 1;
        private const int _itemsPerPage = 10;

        private bool _isUpdateAvailable = false;
        private bool _isPackageVersionsAvailable = false;

        private string _searchTerm = "";
        public TwinpackService _twinpack;

        private bool _isBrowsingAvailablePackages;
        private bool _isBrowsingInstalledPackages;
        private bool _isBrowsingUpdatablePackages;

        private bool _isDialogLoaded;
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

        public string Version
        {
            get
            {
                return _context?.Version;
            }
        }

        private IEnumerable<PackageItem> _catalog = new List<PackageItem>();
        public IEnumerable<PackageItem> Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledPackagesCount)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateablePackagesCount)));
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

        bool _hasMoreAvailablePackages;
        public bool HasMoreAvailablePackages
        {
            get { return _hasMoreAvailablePackages; }
            set
            {
                _hasMoreAvailablePackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMoreAvailablePackages)));
            }
        }

        public int InstalledPackagesCount { get => _twinpack?.UsedPackages.Count() ?? 0; }

        public int UpdateablePackagesCount { get => _twinpack?.UsedPackages.Count(x => x.IsUpdateable) ?? 0; }

        public PackageGetResponse Package { get =>  _catalogItem.Package; }

        public PackageVersionGetResponse PackageVersion { get => _catalogItem.PackageVersion; }

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

        private bool _addDependencies;
        public bool AddDependencies
        {
            get { return _addDependencies; }
            set
            {
                _addDependencies = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddDependencies)));
            }
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
                _isRestoreAllEnabled = !_twinpack?.HasUnknownPackages == true;
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

            _packageServerSelectionChanged = new SelectionChangedEventHandler(PackageServers_SelectionChanged);

            IsCatalogEnabled = true;
            ForcePackageVersionDownload = true;
            AddDependencies = true;

            Catalog = new List<PackageItem>();
            DataContext = this;

            InitializeComponent();

            _catalogItem.PropertyChanged += CatalogItemPackage_Changed;
            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            BranchesView.SelectionChanged += PackageFilter_SelectionChanged;
            ConfigurationsView.SelectionChanged += PackageFilter_SelectionChanged;
            TargetsView.SelectionChanged += PackageFilter_SelectionChanged;
            VersionsView.SelectionChanged += PackageVersions_SelectionChanged;
            PackageServersComboBox.SelectionChanged += _packageServerSelectionChanged;

            Loaded += Dialog_Loaded;
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                ResetServerSelection();

                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await InitializeInternalAsync(_cancellationTokenSource.Token);
                _isDialogLoaded = true;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async Task InitializeAsync()
        {
            if (!_isDialogLoaded)
                return;

            try
            {
                ResetServerSelection();

                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await InitializeInternalAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        protected async Task InitializeInternalAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                IsInitializing = true;
                var servers = PackageServersComboBox.SelectedIndex > 0 && PackagingServerRegistry.Servers.Any()
                    ? new PackageServerCollection { PackagingServerRegistry.Servers.Where(x => x.Connected).ToList()[PackageServersComboBox.SelectedIndex - 1] }
                    : PackagingServerRegistry.Servers;
                var activePlc = _context.VisualStudio.ActivePlc();

                CatalogView.SelectionChanged -= Catalog_SelectionChanged;
                Catalog = new List<PackageItem>();

                _catalogItem.Invalidate();
                servers.InvalidateCache();
                var config = await LoadConfigAsync(activePlc?.Name, servers, cancellationToken);
                _twinpack = new TwinpackService(servers, _context.VisualStudio.AutomationInterface, config, plcName: activePlc?.Name);

                if (IsBrowsingAvailablePackages == false && IsBrowsingInstalledPackages == false && _isBrowsingUpdatablePackages == false)
                {
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
                }

                CatalogView.SelectionChanged += Catalog_SelectionChanged;
                await UpdateCatalogAsync(_searchTerm, maxNewPackages: 10, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                IsBrowsingAvailablePackages = true;
            }
            finally
            {
                IsInitializing = false;
                IsCatalogEnabled = true;
                IsPackageVersionPanelEnabled = _plcConfig != null;
                IsUpdateAvailable = _twinpack?.IsClientUpdateAvailable == true;
            }
        }

        public async Task<Config> LoadConfigAsync(string plcName, List<IPackageServer> packageServers, CancellationToken cancellationToken)
        {
            // make sure that the sln is up-to-date
            _context.Dte.ExecuteCommand("File.SaveAll");

            if (plcName == null)
            {
                IsCreateConfigVisible = false;
                IsMigrateConfigVisible = false;
                IsConfigured = false;
                return null;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var config = ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));
                if(config == null)
                {
                    config = await ConfigFactory.CreateFromSolutionFileAsync(Path.GetDirectoryName(_context.Solution.FullName), continueWithoutSolution: false, packageServers: packageServers.Where(x => x.Connected), cancellationToken: cancellationToken);
                    _configFilePath = config.FilePath;
                    config.FilePath = null; // we don't want to save to a file
                }
                _plcConfig = ConfigPlcProjectFactory.MapPlcConfigToPlcProj(config, plcName);

                IsCreateConfigVisible = config.FilePath == null;
                IsMigrateConfigVisible = config != null && _plcConfig?.Packages?.Any() == false && _plcConfig?.Frameworks?.Zeugwerk?.References?.Any() == true;
                IsConfigured = _plcConfig != null;

                cancellationToken.ThrowIfCancellationRequested();
                return config;
            }
            catch (OperationCanceledException ex)
            {
                IsCreateConfigVisible = true;
                IsMigrateConfigVisible = false;
                IsConfigured = false;
                _plcConfig = null;
                throw;
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
            }

            return null;
        }

        public async void EditServersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                var dialog = new PackagingServerDialog();
                dialog.Owner = Application.Current.MainWindow;
                dialog.ShowDialog();

                if (dialog.DialogResult == true)
                {
                    ResetServerSelection();
                    await InitializeInternalAsync(_cancellationTokenSource.Token);

                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async void PackageServers_SelectionChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await InitializeInternalAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void UninstallPackageButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                _semaphoreAction.Wait();
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_cancellationTokenSource.Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: _cancellationTokenSource.Token);
                _logger.Info("Uninstalling package");

                _context.Dte.ExecuteCommand("File.SaveAll");

                await _twinpack.RemovePackagesAsync(new List<PackageItem> { _catalogItem }, uninstall: UninstallDeletes, cancellationToken: _cancellationTokenSource.Token);

                IsNewReference = true;
                InstalledPackageVersion = null;

                // update config
                _context.Dte.ExecuteCommand("File.SaveAll");
                await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);

                _logger.Info($"Successfully removed {_catalogItem.PackageVersion?.Name}");
                _logger.Info("Finished\n");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                _semaphoreAction.Wait();
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_cancellationTokenSource.Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: _cancellationTokenSource.Token);
                _logger.Info("Adding package");

                _context.Dte.ExecuteCommand("File.SaveAll");

                _catalogItem.Config.Options = Options;

                // show licenses and wait for accept
                var affectedPackages = await _twinpack.AffectedPackagesAsync(new List<PackageItem> { _catalogItem }, includeDependencies: true, cancellationToken: _cancellationTokenSource.Token);
                if (ConfirmLicensesIfNeeded(affectedPackages, true))
                    await _twinpack.AddPackagesAsync(affectedPackages, new TwinpackService.AddPackageOptions { ForceDownload = ForcePackageVersionDownload, IncludeDependencies = AddDependencies }, _cancellationTokenSource.Token);

                IsNewReference = false;

                _catalogItem.Package = _catalogItem.PackageVersion;
                _catalogItem.PackageVersion = _catalogItem.PackageVersion;

                _context.Dte.ExecuteCommand("File.SaveAll");
                await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);

                _logger.Info($"Successfully added {_catalogItem.PackageVersion?.Name}");
                _logger.Info("Finished\n");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                _semaphoreAction.Wait();
                IsCatalogEnabled = false;
                IsPackageVersionPanelEnabled = false;

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_cancellationTokenSource.Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: _cancellationTokenSource.Token);
                _logger.Info("Restoring all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");

                await _twinpack.RestorePackagesAsync(new TwinpackService.RestorePackageOptions { ForceDownload = ForcePackageVersionDownload, IncludeDependencies = AddDependencies, IncludeProvidedPackages = true }, cancellationToken: _cancellationTokenSource.Token);
                await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);

                _context.Dte.ExecuteCommand("File.SaveAll");
                _logger.Info($"Successfully restored all references");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsCatalogEnabled = true;
                IsPackageVersionPanelEnabled = true;
                _semaphoreAction.Release();
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        public async void UpdateAllPackageButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                _semaphoreAction.Wait();
                IsPackageVersionPanelEnabled = false;
                IsCatalogEnabled = false;

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_cancellationTokenSource.Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: _cancellationTokenSource.Token);
                _logger.Info("Updating all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");

                await _twinpack.UpdatePackagesAsync(
                    new TwinpackService.UpdatePackageFilters { },
                    new TwinpackService.UpdatePackageOptions
                    { 
                        ForceDownload = ForcePackageVersionDownload, 
                        IncludeDependencies = AddDependencies, 
                        IncludeProvidedPackages = true 
                    }, 
                    _cancellationTokenSource.Token);
                await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);

                _context.Dte.ExecuteCommand("File.SaveAll");
                _logger.Info($"Successfully updated references to their latest version");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                IsBrowsingAvailablePackages = false;
                IsBrowsingUpdatablePackages = true;
                IsBrowsingInstalledPackages = false;
                await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async void ShowInstalledPackages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                IsBrowsingAvailablePackages = false;
                IsBrowsingUpdatablePackages = false;
                IsBrowsingInstalledPackages = true;
                await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                if (_searchTerm != SearchTextBox.Text)
                {
                    await UpdateCatalogAsync(SearchTextBox.Text, cancellationToken: _cancellationTokenSource.Token);
                    _searchTerm = SearchTextBox.Text;
                }
                else
                {
                    await UpdateCatalogAsync(cancellationToken: _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async Task UpdateCatalogAsync(string searchTerm=null, int maxNewPackages = 0, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                IsCatalogLoading = true;

                if (searchTerm == null)
                    searchTerm = _searchTerm;

                var installedPackages = await _twinpack.RetrieveUsedPackagesAsync(searchTerm, token: cancellationToken);
                var availablePackages = await _twinpack.RetrieveAvailablePackagesAsync(searchTerm, maxNewPackages, 5, cancellationToken);

                // synchronize the list of installed packages with the list of available packages
                var zipped =
                availablePackages.GroupJoin(installedPackages,
                    item1 => item1.Catalog?.Name,
                    item2 => item2.Catalog?.Name, (item1, matchingItems) => new { Available = item1, Installed = matchingItems.FirstOrDefault() }).
                Union(
                installedPackages.GroupJoin(availablePackages,
                    item2 => item2.Catalog?.Name,
                    item1 => item1.Catalog?.Name, (item2, matchingItems) => new { Available = matchingItems.FirstOrDefault(), Installed = item2 })
                );

                foreach (var package in zipped)
                {
                    if (package.Installed != null && package.Available != null)
                    {
                        package.Available.Used = package.Installed.Used;
                        package.Available.Update = package.Installed.Update;
                        package.Available.Config = package.Installed.Config;
                    }
                }

                Regex rx = searchTerm != null ? new Regex(searchTerm.Replace(" ", "."), RegexOptions.Compiled) : null;
                if (IsBrowsingAvailablePackages)
                {
                    Catalog = availablePackages.Where(x =>
                            rx?.Match(x.Catalog?.Name).Success == true ||
                            rx?.Match(x.Catalog?.DisplayName).Success == true ||
                            rx?.Match(x.Catalog?.DistributorName).Success == true
                         ).ToList();
                }
                else if (IsBrowsingInstalledPackages)
                {
                    Catalog = installedPackages.Where(x =>
                            rx?.Match(x.Catalog?.Name).Success == true ||
                            rx?.Match(x.Catalog?.DisplayName).Success == true ||
                            rx?.Match(x.Catalog?.DistributorName).Success == true
                         )
                        .Where(x => PackageServersComboBox.SelectedIndex <= 0 || x.PackageServer != null) // if there is no explicit filter how all packages even if the can not be resolved
                        .ToList();
                }
                else if (IsBrowsingUpdatablePackages)
                {
                    Catalog = installedPackages
                        .Where(x => x.IsUpdateable)
                        .Where(x =>
                            rx?.Match(x.Catalog?.Name).Success == true ||
                            rx?.Match(x.Catalog?.DisplayName).Success == true ||
                            rx?.Match(x.Catalog?.DistributorName).Success == true
                         )
                        .Where(x => PackageServersComboBox.SelectedIndex <= 0 || x.PackageServer != null) // if there is no explicit filter how all packages even if the can not be resolved
                        .ToList();
                }

                IsUpdateAllVisible = IsBrowsingUpdatablePackages && Catalog.Any();
                IsRestoreAllVisible = IsBrowsingInstalledPackages && Catalog.Any();
                HasMoreAvailablePackages = _twinpack.HasMoreAvailablePackages;

                // remap catalogItem
                if (_catalogItem != null)
                {
                    _catalogItem.Catalog = Catalog.FirstOrDefault(x => x.Catalog?.Name == _catalogItem.Catalog?.Name)?.Catalog;
                    if (_catalogItem?.Catalog?.Name == null || (_catalogItem.Catalog?.Name != _catalogItem.Package.Name || _catalogItem.Catalog?.Name != _catalogItem.PackageVersion?.Name))
                    {
                        InstalledPackageVersion = null;
                        _catalogItem.Invalidate();
                    }
                    else if (_catalogItem != null)
                    {
                        InstalledPackageVersion = _catalogItem.IsPlaceholder ? _catalogItem.InstalledVersion + "*" : _catalogItem.InstalledVersion;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                IsCatalogLoading = false;
            }
        }

        public bool ConfirmLicensesIfNeeded(IEnumerable<PackageItem> packages, bool showLicenseDialog)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            var shownLicenseIds = ForceShowLicense ? new HashSet<string>() : new HashSet<string>(_twinpack.KnownRuntimeLicenseIds());

            foreach (var package in packages)
            {
                var licenseId = TwinpackService.ParseRuntimeLicenseIdFromTmc(package.PackageVersion?.LicenseTmcText);
                if (!shownLicenseIds.Any(x => x == licenseId) &&
                   (ForceShowLicense || (showLicenseDialog && !_twinpack.IsPackageInstalled(package))) &&
                   (!string.IsNullOrEmpty(package.PackageVersion?.LicenseBinary) || (!string.IsNullOrEmpty(package.PackageVersion?.LicenseTmcBinary) && (ForceShowLicense))))
                {
                    var licenseDialog = new LicenseWindow(package.PackageVersion) { IsInstalling = true };
                    if (licenseDialog.ShowLicense() == false)
                        return false;

                    if (licenseId != null)
                        shownLicenseIds.Add(licenseId);
                }
            }

            return true;
        }

        public void ShowLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseDialog = new LicenseWindow(_catalogItem.PackageVersion);
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
                var licenseDialog = new LicenseWindow(_catalogItem.PackageVersion);
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
                cancellationToken.ThrowIfCancellationRequested();

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
                }

                cancellationToken.ThrowIfCancellationRequested();

                Versions = Versions.Concat(results.Item1.Select(x => new PlcVersion { Version = x.Version, VersionDisplayText = x.Version })).ToList();
                _currentPackageVersionsPage++;
            }
            catch (OperationCanceledException ex)
            {
                throw;
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
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await UpdateCatalogAsync(maxNewPackages: 10, cancellationToken: _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void ShowMoreAvailableVersionsButton_Click(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            if (_catalogItem.PackageVersion?.Name == null)
                return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await LoadNextPackageVersionsPageAsync(reset: false, cancellationToken: _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        private void CatalogItemPackage_Changed(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_catalogItem.Package))
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
            else if (e.PropertyName == nameof(_catalogItem.PackageVersion))
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                var packageItem = ((sender as ListView).SelectedItem as Models.PackageItem);
                _catalogItem.Catalog = packageItem?.Catalog;
                _catalogItem.ProjectName = packageItem?.ProjectName ?? _plcConfig?.ProjectName;
                _catalogItem.PlcName = packageItem?.PlcName ?? _plcConfig?.Name;
                _catalogItem.PackageServer = packageItem?.PackageServer;
                _catalogItem.Config = packageItem?.Config;
                _catalogItem.Used = packageItem?.Used;
                _catalogItem.Update = packageItem?.Update;
                _catalogItem.Package = packageItem?.Package;
                _catalogItem.PackageVersion = packageItem?.PackageVersion;

                if (_catalogItem?.Catalog == null)
                    return;

                InstalledPackageVersion = _catalogItem.IsPlaceholder ? _catalogItem.InstalledVersion + "*" : _catalogItem.InstalledVersion;

                IsPackageLoading = true;
                IsPackageVersionLoading = true;

                await _twinpack.FetchPackageAsync(_catalogItem, _cancellationTokenSource.Token);
                    
                Options = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == _catalogItem.Catalog?.Name)?.Options ?? new AddPlcLibraryOptions();

                BranchesView.Visibility = _catalogItem.Package?.Branches?.Any() == true ? Visibility.Visible : Visibility.Collapsed;
                TargetsView.Visibility = _catalogItem.Package?.Targets?.Any() == true ? Visibility.Visible : Visibility.Collapsed;
                ConfigurationsView.Visibility = _catalogItem.Package?.Configurations?.Any() == true ? Visibility.Visible : Visibility.Collapsed;

                ConfigurationsView.SelectedIndex = -1;
                BranchesView.SelectedIndex = string.IsNullOrEmpty(_catalogItem.Used?.Branch) ? 0 : _catalogItem.Package?.Branches?.FindIndex(x => x == _catalogItem.Used?.Branch) ?? -1;
                TargetsView.SelectedIndex = string.IsNullOrEmpty(_catalogItem.Used?.Target) ? 0 : _catalogItem.Package?.Targets?.FindIndex(x => x == _catalogItem.Used?.Target) ?? -1;
                ConfigurationsView.SelectedIndex = string.IsNullOrEmpty(_catalogItem.Used?.Configuration) ? 0 : _catalogItem.Package?.Configurations?.FindIndex(x => x == _catalogItem.Used?.Configuration) ?? -1;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
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
        private async void PackageFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            if (_catalogItem.Package?.Name == null ||
                BranchesView.SelectedIndex < 0 || TargetsView.SelectedIndex < 0 || ConfigurationsView.SelectedIndex < 0)
                return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                IsPackageVersionLoading = _catalogItem.Package?.Name == null || _catalogItem.Package?.Name != _catalogItem.PackageVersion?.Name;

                VersionsView.SelectionChanged -= PackageVersions_SelectionChanged;
                await LoadFirstPackageVersionsPageAsync(_cancellationTokenSource.Token);
                VersionsView.SelectionChanged += PackageVersions_SelectionChanged;

                if (Versions?.Any(x => x.Version != null) == true)
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
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionLoading = false;
            }
        }
#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                var item = (sender as ComboBox).SelectedItem as PlcVersion;

                IsPackageVersionLoading = _catalogItem.PackageVersion?.Name == null || _catalogItem.Package?.Name != _catalogItem.PackageVersion?.Name || item?.Version != _catalogItem.PackageVersion?.Version;

                if (item != null)
                {
                    _catalogItem.Config.Version = item?.Version;
                    _catalogItem.Config.Branch = BranchesView.SelectedItem as string;
                    _catalogItem.Config.Configuration = ConfigurationsView.SelectedItem as string;
                    _catalogItem.Config.Target = TargetsView.SelectedItem as string;
                    await _twinpack.FetchPackageAsync(_catalogItem, cancellationToken: _cancellationTokenSource.Token);
                }

                if ((sender as ComboBox).SelectedIndex == 0 && _catalogItem.PackageVersion != null)
                    _catalogItem.PackageVersion.Version = null;

                IsNewReference = _catalogItem.PackageVersion?.Name == null || 
                    !_twinpack.UsedPackages.Any(x => x.Catalog?.Name == _catalogItem.Package.Name);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionLoading = false;
            }
        }

        void ResetServerSelection()
        {
            PackageServersComboBox.SelectionChanged -= _packageServerSelectionChanged;

            PackageServersComboBox.Items.Clear();
            PackageServersComboBox.Items.Add("All repositories");

            foreach(var packageServer in PackagingServerRegistry.Servers.Where(x => x.Connected))
                PackageServersComboBox.Items.Add(packageServer.Name);

            PackageServersComboBox.SelectedIndex = 0;
            PackageServersComboBox.SelectionChanged += _packageServerSelectionChanged;
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                IsInitializing = true;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_cancellationTokenSource.Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: _cancellationTokenSource.Token);
                _logger.Info("Reloading catalog");

                IsPackageLoading = false;

                var packageCount = Math.Max(10, _twinpack.AvailablePackages.Count());
                _catalogItem.Invalidate();
                await _twinpack.InvalidateCacheAsync(_cancellationTokenSource.Token);
                await UpdateCatalogAsync(searchTerm: _searchTerm, maxNewPackages: packageCount, cancellationToken: _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = _plcConfig != null;
                IsCatalogEnabled = true;
                IsInitializing = false;
            }
        }

        public void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: _cancellationTokenSource.Token);
                await _twinpack.SaveAsync(_configFilePath);
                IsCreateConfigVisible = false;

                _logger.Info($"Created package configuration in {_configFilePath}");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public void ShowProjectUrl_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(_catalogItem.PackageVersion?.ProjectUrl);
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
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                _searchTerm = ((TextBox)sender).Text;
                await UpdateCatalogAsync(searchTerm: _searchTerm, maxNewPackages: 10, cancellationToken: _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
    }
}
