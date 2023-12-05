using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using TCatSysManagerLib;
using Twinpack.Models;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private CancellationTokenSource _cancellationTokenSource;

        public event PropertyChangedEventHandler PropertyChanged;

        private PackageContext _context;
        private Project _plc;

        private ConfigPlcProject _plcConfig;
        private ConfigPlcPackage _packageConfig;
        private IEnumerable<CatalogItem> _catalog = new List<CatalogItem>();
        private List<CatalogItem> _availablePackages = new List<CatalogItem>();
        private List<CatalogItem> _installedPackages = new List<CatalogItem>();
        private SemaphoreSlim _semaphorePackages = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _semaphoreAction = new SemaphoreSlim(1, 1);
        private List<PackageVersionGetResponse> _packageVersions;

        private CatalogItem _item = null;
        private PackageGetResponse _package = new PackageGetResponse();
        private PackageVersionGetResponse _packageVersion = new PackageVersionGetResponse();

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
        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

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
        private bool _isUpdateAllEnabled;
        private bool _isRestoreAllEnabled;

        private bool _isPackageVersionPanelEnabled;
        private string _installedPackageVersion;

        private bool _forcePackageVersionDownload;
        private bool _addDependenciesAsReferences;
        private bool _forceShowLicense;
        private bool _uninstallDeletes;

        private int _installedPackagesCount;
        private int _updateablePackagesCount;

        private CancellationToken Token
        {
            get
            {
                if(_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
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
            get { return _package; }
            set
            {
                _package = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
            }
        }

        public PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
            }
        }

        public List<PackageVersionGetResponse> Versions
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

        public bool AddDependenciesAsReferences
        {
            get { return _addDependenciesAsReferences; }
            set
            {
                _addDependenciesAsReferences = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddDependenciesAsReferences)));
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

        public bool IsUpdateAllEnabled
        {
            get { return _isUpdateAllEnabled; }
            set
            {
                _isUpdateAllEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAllEnabled)));
            }
        }

        public bool IsRestoreAllEnabled
        {
            get { return _isRestoreAllEnabled; }
            set
            {
                _isRestoreAllEnabled = value;
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
            _auth = new Authentication(_twinpackServer);
            _context = context;

            IsUpdateAllEnabled = true;
            IsRestoreAllEnabled = true;
            InstalledPackagesCount = 0;
            UpdateablePackagesCount = 0;
            ForcePackageVersionDownload = true;
            AddDependenciesAsReferences = true;
            Catalog = new List<Models.CatalogItem>();
            DataContext = this;

            InitializeComponent();

            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            BranchesView.SelectionChanged += PackageFilter_SelectionChanged;
            ConfigurationsView.SelectionChanged += PackageFilter_SelectionChanged;
            TargetsView.SelectionChanged += PackageFilter_SelectionChanged;
            VersionsView.SelectionChanged += PackageVersions_SelectionChanged;

            Loaded += Dialog_Loaded;
        }

        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
            
            try
            {
                IsCatalogLoading = true;
                cmbTwinpackServer.Items.Clear();
                cmbTwinpackServer.Items.Add(_twinpackServer.TwinpackUrlBase);
                cmbTwinpackServer.SelectedIndex = 0;
                _plc = TwinpackUtils.ActivePlc(_context.Dte);

                await _auth.LoginAsync(onlyTry: true, cancellationToken: Token);
                await LoadPlcConfigAsync(Token);

                if(!IsConfigured)
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
                IsCatalogLoading = false;
                IsPackageVersionPanelEnabled = _plcConfig != null;
                IsUpdateAvailable = _twinpackServer.IsClientUpdateAvailable == true;
                btnLogin.Text = _twinpackServer.LoggedIn ? "Logout" : "Login";
                btnRegister.Visibility = _twinpackServer.LoggedIn ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public async Task LoadPlcConfigAsync(CancellationToken cancellationToken)
        {
            if (_plc != null)
            {
                try
                {
                    _isLoadingPlcConfig = true;
                    await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    var config = ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));
                    if (config != null)
                    {
                        _plcConfig = ConfigPlcProjectFactory.MapPlcConfigToPlcProj(config, _plc);
                        IsCreateConfigVisible = false;
                        IsMigrateConfigVisible = false;
                        IsMigrateConfigVisible = _plcConfig?.Packages?.Any() == false && _plcConfig.Frameworks?.Zeugwerk?.References?.Any() == true;
                    }
                    else
                    {
                        IsCreateConfigVisible = true;
                        IsMigrateConfigVisible = false;
                        _plcConfig = await ConfigPlcProjectFactory.CreateAsync(_context.Solution, _plc, _twinpackServer, cancellationToken);
                    }

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
        public async void EditPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var packageId = _item?.PackageId;
            if (packageId == null)
                return;

            var packageVersionId = (VersionsView.SelectedItem as Models.PackageVersionGetResponse)?.PackageVersionId;
            var packagePublish = new PackageVersionWindow(false, _context, _plc, packageId, packageVersionId);
            packagePublish.ShowDialog();

            _twinpackServer.InvalidateCache();

            try
            {
                await LoadInstalledPackagesAsync(Token);
                await LoadAvailablePackagesAsync(SearchTextBox.Text, Token);
                UpdateCatalog();
            }
            catch(Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = _plcConfig != null;
            }
        }

        public async void UninstallPackageButton_Click(object sender, RoutedEventArgs e)
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

                _logger.Info($"Successfully removed {PackageVersion.Name} from {_plc.Name} references");
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

        public async void AddPackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _semaphoreAction.Wait();
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Adding package");

                _context.Dte.ExecuteCommand("File.SaveAll");
                await AddOrUpdatePackageAsync(PackageVersion, showLicenseDialog: true, cancellationToken: Token);
                Package = PackageVersion;
                PackageVersion = PackageVersion;
                InstalledPackageVersion = PackageVersion.Version;

                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig, Token);
                await LoadInstalledPackagesAsync(Token);
                UpdateCatalog();

                _logger.Info($"Successfully added {PackageVersion.Name} to {_plc.Name} references");
                _logger.Info("Finished\n");

            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                Package = new PackageGetResponse();
                PackageVersion = new PackageVersionGetResponse();
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

        public async void RestoreAllPackageButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<Models.CatalogItem> items = new List<Models.CatalogItem>();
            try
            {
                _semaphoreAction.Wait();
                IsRestoreAllEnabled = false;
                IsPackageVersionPanelEnabled = false;

                var locked = await _semaphorePackages.WaitAsync(10000, Token);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Restoring all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");
                items = _installedPackages;
                foreach (var item in items)
                {
                    await AddOrUpdatePackageAsync(item.Installed, showLicenseDialog: false, cancellationToken: Token);
                    if (item.PackageId == PackageVersion.PackageId)
                    {
                        Package = item.Installed;
                        PackageVersion = item.Installed;
                        InstalledPackageVersion = PackageVersion.Version;
                    }
                }

                _context.Dte.ExecuteCommand("File.SaveAll");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                Package = new PackageGetResponse();
                PackageVersion = new PackageVersionGetResponse();
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
                SelectPackageVersionFilter(PackageVersion);
                UpdateCatalog();

                _logger.Info($"Successfully restored {items?.Count()} references");
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
                IsRestoreAllEnabled = true;
                _semaphoreAction.Release();
            }
        }

        public async void UpdateAllPackageButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<Models.CatalogItem> items = new List<Models.CatalogItem>();
            try
            {
                _semaphoreAction.Wait();

                IsPackageVersionPanelEnabled = false;
                IsUpdateAllEnabled = false;

                var locked = await _semaphorePackages.WaitAsync(10000, Token);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Updating all packages");

                _context.Dte.ExecuteCommand("File.SaveAll");
                items = _installedPackages.Where(x => x.IsUpdateable);
                foreach (var item in items)
                {
                    await AddOrUpdatePackageAsync(item.Update, showLicenseDialog: false, cancellationToken: Token);
                    if (item.PackageId == PackageVersion.PackageId)
                    {
                        Package = item.Update;
                        PackageVersion = item.Update;
                        InstalledPackageVersion = PackageVersion.Version;
                    }
                }

                _context.Dte.ExecuteCommand("File.SaveAll");
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                Package = new PackageGetResponse();
                PackageVersion = new PackageVersionGetResponse();
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
                Token.ThrowIfCancellationRequested();
                var config = await WritePlcConfigToConfigAsync(_plcConfig, cancellationToken: Token);
                await LoadInstalledPackagesAsync(cancellationToken: Token);
                UpdateCatalog();

                _logger.Info($"Successfully updated {items?.Count()} references to their latest version");
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
                IsUpdateAllEnabled = true;
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

        public async void ShowCatalog_Click(object sender, RoutedEventArgs e)
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
            // synchronize the list of installed packages with the list of available packages
            var zipped =
            _availablePackages.GroupJoin(_installedPackages,
                item1 => item1.PackageId,
                item2 => item2.PackageId, (item1, matchingItems) => new { Available = item1, Installed = matchingItems.FirstOrDefault() }).
            Union(
            _installedPackages.GroupJoin(_availablePackages,
                item2 => item2.PackageId,
                item1 => item1.PackageId, (item2, matchingItems) => new { Available = matchingItems.FirstOrDefault(), Installed = item2 })
            );

            foreach(var package in zipped)
            {
                if(package.Installed != null && package.Available != null)
                {
                    package.Available.Installed = package.Installed.Installed;
                    package.Available.Update = package.Installed.Update;
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
        }

        public async Task UninstallPackageAsync(CancellationToken cancellationToken)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (PackageVersion.PackageVersionId == null)
                throw new Exception("No packages is selected that could be uninstalled!");

            var plc = _plc.Object as dynamic;
            var sysManager = plc.SystemManager as ITcSysManager2;
            var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;

            _context.Dte.ExecuteCommand("File.SaveAll");
            TwinpackUtils.CloseAllPackageRelatedWindows(_context.Dte, PackageVersion);
            TwinpackUtils.RemoveReference(libManager, Package.Title, Package.Title, PackageVersion.Version, _package.DistributorName);
            _context.Dte.ExecuteCommand("File.SaveAll");

            if (UninstallDeletes)
            {
                _logger.Info($"Uninstalling package {PackageVersion.Name} from system ...");
                TwinpackUtils.UninstallReferenceAsync(libManager, _packageVersion, cancellationToken);
            }

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != PackageVersion.Name).ToList();
            var p = _availablePackages.FirstOrDefault(x => x.PackageId == PackageVersion.PackageId);
            if(p != null)
            {
                p.Installed = null;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public bool IsLicenseDialogRequired(ITcPlcLibraryManager libManager, Models.PackageVersionGetResponse packageVersion, bool showLicenseDialogHint, HashSet<string> shownLicenses)
        {
            var licenseId = TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText);
            return (ForceShowLicense || (showLicenseDialogHint && !TwinpackUtils.IsPackageInstalled(libManager, packageVersion))) &&
                   (!string.IsNullOrEmpty(packageVersion.LicenseBinary) || (!string.IsNullOrEmpty(packageVersion.LicenseTmcBinary) && (ForceShowLicense || !shownLicenses.Contains(licenseId))));
        }

        public async Task AddOrUpdatePackageAsync(PackageVersionGetResponse packageVersion, bool showLicenseDialog = true, CancellationToken cancellationToken = default)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (packageVersion.PackageVersionId == null)
                throw new Exception("No packages is selected that could be installed or updated!");

            var cachePath = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
            var plc = _plc.Object as dynamic;
            var sysManager = plc.SystemManager as ITcSysManager2;
            var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;
            var knownLicenseIds = TwinpackUtils.KnownLicenseIds();
            var shownLicenseIds = new HashSet<string>(knownLicenseIds);

            if(IsLicenseDialogRequired(libManager, packageVersion, showLicenseDialog, shownLicenseIds))
            {
                var licenseDialog = new LicenseWindow(libManager, packageVersion);
                if (licenseDialog.ShowLicense() == false)
                {
                    _logger.Warn($"License for {packageVersion.Name} was declined");
                    return;
                }

                shownLicenseIds.Add(TwinpackUtils.ParseLicenseId(packageVersion.LicenseTmcText));
            }

            foreach (var dependency in packageVersion.Dependencies)
            {
                if (IsLicenseDialogRequired(libManager, dependency, showLicenseDialog, shownLicenseIds))
                {
                    var licenseWindow = new LicenseWindow(libManager, dependency);
                    if (licenseWindow.ShowLicense() == false)
                    {
                        _logger.Warn($"License for {packageVersion.Name} was declined");
                        return;
                    }

                    shownLicenseIds.Add(TwinpackUtils.ParseLicenseId(dependency.LicenseTmcText));
                }
            }

            if(packageVersion.LicenseTmcBinary != null)
            {
                _logger.Info($"Copying license description file to TwinCAT for {packageVersion.Name} ...");
                TwinpackUtils.CopyLicenseTmcIfNeeded(packageVersion, knownLicenseIds);
            }

            foreach (var dependency in packageVersion.Dependencies)
            {
                if (dependency.LicenseTmcBinary != null)
                {
                    _logger.Info($"Copying license description file to TwinCAT for {dependency.Name} ...");
                    TwinpackUtils.CopyLicenseTmcIfNeeded(dependency, knownLicenseIds);
                }
            }

            var downloadPackageVersion = await TwinpackUtils.DownloadPackageVersionAndDependenciesAsync(libManager, packageVersion, _twinpackServer, forceDownload: ForcePackageVersionDownload, cachePath: cachePath, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Close Library Manager and all windows that are related to the library. These windows cause race conditions
            TwinpackUtils.CloseAllPackageRelatedWindows(_context.Dte, packageVersion);
            TwinpackUtils.RemoveReference(libManager, packageVersion.Title, packageVersion.Title, packageVersion.Version, packageVersion.DistributorName);

            _logger.Info($"Installing package {packageVersion.Name} ...");
            await TwinpackUtils.InstallPackageVersionsAsync(libManager, downloadPackageVersion, cachePath: cachePath, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Run(() =>
            {
                TwinpackUtils.AddReference(libManager, packageVersion.Title, packageVersion.Title, packageVersion.Version, packageVersion.DistributorName);

                if(AddDependenciesAsReferences)
                {
                    foreach (var dependency in packageVersion.Dependencies)
                    {
                        TwinpackUtils.AddReference(libManager, dependency.Title, dependency.Title, dependency.Version, dependency.DistributorName);
                    }
                }

            });
            cancellationToken.ThrowIfCancellationRequested();
            IsNewReference = false;

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != packageVersion.Name)
                                                     .Append(new Models.ConfigPlcPackage
                                                     {
                                                          Name = packageVersion.Name,
                                                          Repository = packageVersion.Repository,
                                                          Branch = packageVersion.Branch,
                                                          Configuration = packageVersion.Configuration,
                                                          Target = packageVersion.Target,
                                                          Version = packageVersion.Version,
                                                          DistributorName = packageVersion.DistributorName
                                                     }).ToList();

            if (AddDependenciesAsReferences)
            {
                foreach (var dependency in packageVersion.Dependencies)
                {
                    _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != dependency.Name)
                                                             .Append(new Models.ConfigPlcPackage
                                                             {
                                                                 Name = dependency.Name,
                                                                 Repository = dependency.Repository,
                                                                 Branch = dependency.Branch,
                                                                 Configuration = dependency.Configuration,
                                                                 Target = dependency.Target,
                                                                 Version = dependency.Version,
                                                                 DistributorName = dependency.DistributorName
                                                             }).ToList();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task<Models.Config> WritePlcConfigToConfigAsync(ConfigPlcProject plcConfig, CancellationToken cancellationToken)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var config = ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));

            if(config != null)
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
                var licenseDialog = new LicenseWindow(null, PackageVersion);
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
                var licenseDialog = new LicenseWindow(null, PackageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
        public void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(_twinpackServer.RegisterUrl);
        }

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
                _twinpackServer.InvalidateCache();
                await LoadAvailablePackagesAsync("", Token);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        private async Task LoadFirstPackageVersionsPageAsync(int? packageId, string branch, string configuration, string target, CancellationToken cancellationToken)
        {
            if (packageId == null)
                return;

            await LoadNextPackageVersionsPageAsync((int)packageId, branch, configuration, target, true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task LoadNextPackageVersionsPageAsync(int packageId, string branch, string configuration, string target, bool reset = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (reset)
                    _currentPackageVersionsPage = 1;

                var results = await _twinpackServer.GetPackageVersionsAsync(packageId,
                    branch,
                    configuration,
                    target,
                    _currentPackageVersionsPage,
                    _itemsPerPage,
                    cancellationToken);

                IsPackageVersionsAvailable = results.Item2;

                if (reset)
                    Versions = new List<PackageVersionGetResponse>();
            
                Versions = Versions.Concat(results.Item1).ToList();
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

                var results = await _twinpackServer.GetCatalogAsync(text, _currentCatalogPage, _itemsPerPage, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                IsAvailablePackageAvailable = results.Item2;

                if (reset)
                {
                    _availablePackages.Clear();
                }
                foreach (var item in results.Item1)
                {
                    _availablePackages.Add(new CatalogItem(item));
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
                    _installedPackages.Clear();
                    foreach (var item in _plcConfig.Packages)
                    {
                        CatalogItem catalogItem = new CatalogItem(item);
                        var packageVersion = await _twinpackServer.GetPackageVersionAsync(item.DistributorName, item.Name, null, item.Configuration, item.Branch, item.Target, cancellationToken: cancellationToken);

                        if (packageVersion.PackageVersionId != null)
                            catalogItem = new CatalogItem(packageVersion, item.Version);

                        _installedPackages.Add(catalogItem);
                    }
                }

                IsNewReference = PackageVersion.PackageVersionId == null || !_installedPackages.Any(x => x.PackageId == PackageVersion.PackageId);
                InstalledPackagesCount = _installedPackages.Count();
                UpdateablePackagesCount = _installedPackages.Where(x => x.IsUpdateable).Count();
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                Package = new PackageGetResponse();
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

        private async void ShowMoreAvailablePackagesButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadNextCatalogPageAsync(SearchTextBox.Text, cancellationToken: Token);
        }

        private async void ShowMoreAvailableVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PackageVersion.PackageId == null)
                return;

            var branch = BranchesView.SelectedItem as string;
            var configuration = ConfigurationsView.SelectedItem as string;
            var target = TargetsView.SelectedItem as string;

            try
            {
                await LoadNextPackageVersionsPageAsync((int)PackageVersion.PackageId, branch, configuration, target, cancellationToken: Token);
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
            BranchesView.SelectedIndex = string.IsNullOrEmpty(installed?.Branch) ? 0 : Package.Branches?.FindIndex(x => x == installed.Branch) ?? -1;
            TargetsView.SelectedIndex = string.IsNullOrEmpty(installed?.Target) ? 0 : Package.Targets?.FindIndex(x => x == installed.Target) ?? -1;
            ConfigurationsView.SelectedIndex = string.IsNullOrEmpty(installed?.Configuration) ? 0 : Package.Configurations?.FindIndex(x => x == installed.Configuration) ?? -1;
        }

        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _item = (sender as ListView).SelectedItem as Models.CatalogItem;
            if (_item == null)
                return;

            try
            {
                var locked = await _semaphorePackages.WaitAsync(10000, Token);
                if (!locked)
                    throw new TimeoutException("Timeout occured while waiting for Twinpack server!");

                IsPackageLoading = true;
                IsPackageVersionLoading = true;
                InstalledPackageVersion = "";

                // check if the plc already contains the selected package
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == _item.Name);

                if (_packageConfig != null)
                    Package = await _twinpackServer.GetPackageAsync(_packageConfig.DistributorName, _item.Name, Token);
                else
                    Package = await _twinpackServer.GetPackageAsync(_item.DistributorName, _item.Name, Token);

                var index = 0;
                if (PackageVersion?.PackageVersionId != null && PackageVersion?.PackageId == Package.PackageId)
                {
                    InstalledPackageVersion = PackageVersion.Version ?? "";
                    index = _packageVersions.IndexOf(_packageVersions.FirstOrDefault(x => x.PackageVersionId == PackageVersion?.PackageVersionId));
                }
                else if (_packageConfig != null)
                {
                    InstalledPackageVersion = _packageConfig.Version ?? "";
                }

                BranchesView.Visibility = Package?.Branches.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                TargetsView.Visibility = Package?.Targets.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                ConfigurationsView.Visibility = Package?.Configurations.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;

                SelectPackageVersionFilter(_item?.Installed);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                Package = new PackageGetResponse();
                PackageVersion = new PackageVersionGetResponse();
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

        private async void PackageFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Package.PackageId == null || 
                    BranchesView.SelectedIndex < 0 || TargetsView.SelectedIndex < 0 || ConfigurationsView.SelectedIndex < 0)
                    return;

                IsPackageLoading = Package.PackageId != PackageVersion.PackageId;
                IsPackageVersionLoading = IsPackageLoading;

                var branch = BranchesView.SelectedItem as string;
                var configuration = ConfigurationsView.SelectedItem as string;
                var target = TargetsView.SelectedItem as string;

                await LoadFirstPackageVersionsPageAsync((int)Package.PackageId, branch, configuration, target, Token);

                var index = Versions?.FindIndex(x => x.Version == _item?.Installed?.Version) ?? -1;
                if (index < 0 && _item?.Installed != null)
                    index = 0;

                VersionsView.SelectedIndex = string.IsNullOrEmpty(_item?.Installed?.Version) ? 0 : index;
            }
            catch (OperationCanceledException ex)
            {
                Package = new PackageGetResponse();
                PackageVersion = new PackageVersionGetResponse();
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
            }
        }

        private async void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                IsPackageLoading = Package.PackageId != PackageVersion.PackageId;
                var item = (sender as ComboBox).SelectedItem as Models.PackageVersionGetResponse;

                IsPackageVersionLoading = item?.PackageVersionId != PackageVersion.PackageVersionId;

                if (item != null)
                {
                    PackageVersion = await _twinpackServer.GetPackageVersionAsync(item.DistributorName,
                        item.Name, item.Version, item.Configuration, item.Branch, item.Target,
                        cancellationToken: Token);
                }

                IsNewReference = PackageVersion.PackageVersionId == null || !_installedPackages.Any(x => x.PackageId == Package.PackageId);
            }
            catch (OperationCanceledException ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                PackageVersion = new PackageVersionGetResponse();
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

        public async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsCatalogLoading = true;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info("Reloading catalog");

                _item = null;
                IsPackageLoading = false;
                Package = new PackageGetResponse();
                PackageVersion = new PackageVersionGetResponse();

                _twinpackServer.InvalidateCache();
                _context.Dte.ExecuteCommand("File.SaveAll");

                if(!IsConfigured)
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
            }
        }

        public async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            IsFetchingAvailablePackages = false;
            IsFetchingInstalledPackages = false;
            IsCatalogLoading = false;
            IsPackageLoading = false;
            IsPackageVersionLoading = false;
        }

        public async void CreateConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(Token);
                await _context?.Logger?.ActivateAsync(clear: true, cancellationToken: Token);
                _logger.Info($"Creating package configuration");

                var config = await ConfigFactory.CreateFromSolutionAsync(_context.Solution, _twinpackServer, Token);

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
                IsPackageVersionPanelEnabled = _plcConfig != null;
            }
        }

        public void ShowProjectUrl_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(PackageVersion.ProjectUrl);
        }

        public void UpdateAvailableButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(_twinpackServer?.UserInfo?.UpdateUrl);
        }

        public async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var text = ((TextBox)sender).Text;

                if (IsBrowsingAvailablePackages)
                {
                    _searchText = text;
                    await Task.Delay(250);

                    if (_searchText == text)
                        await LoadAvailablePackagesAsync(text, Token);
                }
                else
                {
                    UpdateCatalog();
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
