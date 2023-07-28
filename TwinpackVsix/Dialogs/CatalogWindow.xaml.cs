using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NLog;
using TCatSysManagerLib;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public event PropertyChangedEventHandler PropertyChanged;

        private PackageContext _context;
        private EnvDTE.Project _plc;

        private Models.ConfigPlcProject _plcConfig;
        private Models.ConfigPlcPackage _packageConfig;
        private IEnumerable<Models.CatalogItem> _catalog = new List<Models.CatalogItem>();
        private List<Models.CatalogItem> _availablePackages = new List<Models.CatalogItem>();
        private List<Models.CatalogItem> _installedPackages = new List<Models.CatalogItem>();
        private List<Models.PackageVersionsItemGetResponse> _packageVersions;

        public ObservableCollection<Models.PackageVersionsItemGetResponse> Targets { get; set; }
        public ObservableCollection<Models.PackageVersionsItemGetResponse> Configurations { get; set; }
        public ObservableCollection<Models.PackageVersionsItemGetResponse> Branches { get; set; }
        public ObservableCollection<Models.PackageVersionsItemGetResponse> Versions { get; set; }

        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Twinpack.Models.PackageVersionGetResponse();

        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 20;
        private double _catalogScrollPosition = 0;
        private double _packageVersionsScrollPosition = 0;

        private bool _isFetchingInstalledPackages = false;
        private bool _isFetchingAvailablePackages = false;
        private bool _isFetchingPackageVersions = false;
        private bool _isFetchingPackageVersion = false;

        private int? _selectedPackageId = null;
        private string _searchText = "";
        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

        private bool _isBrowsingAvailablePackages;
        private bool _isBrowsingInstalledPackages;
        private bool _isBrowsingUpdatablePackages;

        private bool _isCatalogLoading;
        private bool _isPackageLoading;
        private bool _isNewReference;
        private bool _isConfigured;
        private bool _isCreateConfigVisible;
        private bool _isUpdateAllVisible;
        private bool _isUpdateAllEnabled;

        private bool _isPackageVersionPanelEnabled;
        private string _installedPackageVersion;

        private bool _installReinstalls;
        private bool _uninstallDeletes;

        private int _installedPackagesCount;
        private int _updateablePackagesCount;

        public IEnumerable<Models.CatalogItem> Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catalog)));
            }
        }

        public bool IsFetchingAvailablePackages
        {
            get { return _isFetchingAvailablePackages; }
            set
            {
                _isFetchingAvailablePackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFetchingAvailablePackages)));

                if (_isBrowsingAvailablePackages || _isBrowsingUpdatablePackages)
                {
                    if (_isFetchingAvailablePackages)
                        IsCatalogLoading = true;
                    else
                        UpdateCatalog();
                }
            }
        }

        public bool IsFetchingInstalledPackages
        {
            get { return _isFetchingInstalledPackages; }
            set
            {
                _isFetchingInstalledPackages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFetchingInstalledPackages)));

                if (_isBrowsingInstalledPackages)
                {
                    if (_isFetchingInstalledPackages)
                        IsCatalogLoading = true;
                    else
                        UpdateCatalog();
                }
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

        public Models.PackageGetResponse Package
        {
            get { return _package; }
            set
            {
                _package = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
            }
        }

        public Models.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
            }
        }

        public bool ForcePackageVersionDownload
        {
            get { return _installReinstalls; }
            set
            {
                _installReinstalls = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForcePackageVersionDownload)));
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
            }
        }

        public bool IsPackageLoading
        {
            get { return _isPackageLoading; }
            set
            {
                _isPackageLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPackageLoading)));
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

        public bool IsUpdateAllEnabled
        {
            get { return _isUpdateAllEnabled; }
            set
            {
                _isUpdateAllEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAllEnabled)));
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

        public CatalogWindow(PackageContext context)
        {
            _auth = new Authentication(_twinpackServer);
            _context = context;

            _isBrowsingAvailablePackages = true;

            InstalledPackagesCount = 0;
            UpdateablePackagesCount = 0;
            Catalog = new List<Models.CatalogItem>();
            Targets = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
            Configurations = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
            Branches = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
            Versions = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
            DataContext = this;

            InitializeComponent();

            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            CatalogView.Loaded += (s, e) =>
            {
                var sv = GetScrollViewer(CatalogView);
                if (sv != null)
                    sv.ScrollChanged += Catalog_ScrollChanged;
            };

            VersionsView.SelectionChanged += PackageVersions_SelectionChanged;
            BranchesView.SelectionChanged += PackageVersions_SelectionChanged;
            ConfigurationsView.SelectionChanged += PackageVersions_SelectionChanged;
            TargetsView.SelectionChanged += PackageVersions_SelectionChanged;

            //PackageVersionsView.Loaded += (s, e) => {
            //    var sv = FindVisualChild<ScrollViewer>(PackageVersionsView)
            //    if (sv != null)
            //        sv.ScrollChanged += PackageVersions_ScrollChanged;
            //};

            Loaded += Dialog_Loaded;
        }

        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();


            try
            {
                await _twinpackServer.LoginAsync();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                btnLogin.Content = _twinpackServer.LoggedIn ? "Logout" : "Login";
            }

            try
            {
                _plc = TwinpackUtils.ActivePlc(_context.Dte);

                cmbTwinpackServer.Items.Add(_twinpackServer.TwinpackUrl);
                cmbTwinpackServer.SelectedIndex = 0;

                _isBrowsingAvailablePackages = true;

                await LoadPlcConfigAsync();
                await LoadAvailablePackagesAsync();
                await LoadInstalledPackagesAsync();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async Task LoadPlcConfigAsync()
        {
            if (_plc != null)
            {
                try
                {
                    var config = Models.ConfigFactory.Load(System.IO.Path.GetDirectoryName(_context.Solution.FullName));
                    if (config != null)
                    {
                        _plcConfig = Models.ConfigPlcProjectFactory.MapPlcConfigToPlcProj(config, _plc);
                        IsCreateConfigVisible = false;
                    }
                    else
                    {
                        IsCreateConfigVisible = true;
                        _plcConfig = await Models.ConfigPlcProjectFactory.CreateAsync(_context.Solution, _plc, _twinpackServer);
                    }

                    IsConfigured = _plcConfig != null;
                }
                catch (Exception ex)
                {
                    IsCreateConfigVisible = true;
                    IsConfigured = false;
                    _plcConfig = null;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
            }
            else
            {
                IsCreateConfigVisible = false;
                IsConfigured = false;
            }
        }
        public async void EditPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var packageId = (CatalogView.SelectedItem as Models.CatalogItemGetResponse)?.PackageId;
            if (packageId == null)
                return;

            var packageVersionId = (VersionsView.SelectedItem as Models.PackageVersionsItemGetResponse)?.PackageVersionId;
            var packagePublish = new PackageVersionWindow(false, _context, _plc, packageId, packageVersionId);
            packagePublish.ShowDialog();

            _twinpackServer.InvalidateCache();

            await LoadInstalledPackagesAsync();
            await LoadAvailablePackagesAsync(SearchTextBox.Text);
        }

        public async void UninstallPackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync();

                _context.Dte.ExecuteCommand("File.SaveAll");

                await UninstallPackageAsync();

                IsNewReference = true;
                InstalledPackageVersion = null;

                // update config
                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                await LoadInstalledPackagesAsync();
                await _context.WriteStatusAsync($"Successfully removed {PackageVersion.Name} from {_plc.Name} references");
                _logger.Info("Finished\n");

            }
            catch (Exception ex)
            {
                _logger.Info("Failed\n");

                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
            }
        }

        public async void AddPackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync();

                _context.Dte.ExecuteCommand("File.SaveAll");
                await AddOrUpdatePackageAsync(PackageVersion);
                Package = PackageVersion;
                PackageVersion = PackageVersion;
                InstalledPackageVersion = PackageVersion.Version;

                _context.Dte.ExecuteCommand("File.SaveAll");
                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                await _context.WriteStatusAsync($"Successfully added {PackageVersion.Name} to {_plc.Name} references");
                _logger.Info("Finished\n");

            }
            catch (Exception ex)
            {
                _logger.Info("Failed\n");

                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
            }
        }

        public void UpdatePackageButton_Click(object sender, RoutedEventArgs e)
        {
            AddPackageButton_Click(sender, e);
        }

        public async void UpdateAllPackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsPackageVersionPanelEnabled = false;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync();

                _context.Dte.ExecuteCommand("File.SaveAll");
                IsUpdateAllEnabled = false;
                var items = _installedPackages.Where(x => x.IsUpdateable);
                foreach (var item in items)
                {
                    await AddOrUpdatePackageAsync(item.Update, showLicenseDialog: false);
                    if (item.PackageId == PackageVersion.PackageId)
                    {
                        Package = item.Update;
                        PackageVersion = item.Update;
                        InstalledPackageVersion = PackageVersion.Version;
                    }
                }

                _context.Dte.ExecuteCommand("File.SaveAll");

                var config = await WritePlcConfigToConfigAsync(_plcConfig);
                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
                await _context.WriteStatusAsync($"Successfully updated {items.Count()} references to their latest version");

                _logger.Info("Finished\n");
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                _logger.Info("Failed\n");
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
                IsUpdateAllEnabled = true;
            }
        }

        public void ShowUpdateablePackages_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingAvailablePackages = false;
            _isBrowsingUpdatablePackages = true;
            _isBrowsingInstalledPackages = false;
            UpdateCatalog();
        }

        public void ShowInstalledPackages_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingAvailablePackages = false;
            _isBrowsingUpdatablePackages = false;
            _isBrowsingInstalledPackages = true;
            UpdateCatalog();
        }

        public void ShowCatalog_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingAvailablePackages = true;
            _isBrowsingUpdatablePackages = false;
            _isBrowsingInstalledPackages = false;
            UpdateCatalog();
        }

        public void UpdateCatalog()
        {
            if (_isBrowsingAvailablePackages)
            {
                Catalog = new List<Models.CatalogItem>(_availablePackages);
                IsCatalogLoading = IsFetchingAvailablePackages;
            }
            else if (_isBrowsingInstalledPackages)
            {
                Catalog = new List<Models.CatalogItem>(_installedPackages);
                IsCatalogLoading = IsFetchingInstalledPackages;
            }
            else if (_isBrowsingUpdatablePackages)
            {
                Catalog = _installedPackages.Where(x => x.IsUpdateable).ToList();
                IsCatalogLoading = IsFetchingInstalledPackages;
            }

            IsUpdateAllVisible = _isBrowsingUpdatablePackages && Catalog.Any();
        }

        public async Task UninstallPackageAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await _context?.Logger?.ActivateAsync();

            IsPackageVersionPanelEnabled = false;

            if (PackageVersion.PackageVersionId == null)
                throw new Exception("No packages is selected that could be uninstalled!");

            var plc = (_plc.Object as dynamic);
            var sysManager = plc.SystemManager as ITcSysManager2;
            var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;

            _context.Dte.ExecuteCommand("File.SaveAll");
            await _context.WriteStatusAsync($"Removing package {PackageVersion.Name} from references ...");
            TwinpackUtils.RemoveReference(libManager, Package.Name, Package.Name, PackageVersion.Version, _package.DistributorName);
            _context.Dte.ExecuteCommand("File.SaveAll");

            if (UninstallDeletes)
            {
                await _context.WriteStatusAsync($"Uninstalling package {PackageVersion.Name} from system ...");
                TwinpackUtils.UninstallReferenceAsync(libManager, _packageVersion);
            }

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != PackageVersion.Name && x.Repository == PackageVersion.Repository).ToList();
        }

        public async Task AddOrUpdatePackageAsync(Models.PackageVersionGetResponse pv, bool showLicenseDialog = true)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (pv.PackageVersionId == null)
                throw new Exception("No packages is selected that could be installed or updated!");

            var cachePath = $@"{System.IO.Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
            var plc = _plc.Object as dynamic;
            var sysManager = plc.SystemManager as ITcSysManager2;
            var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;

            if (showLicenseDialog)
            {
                if (!string.IsNullOrEmpty(pv.License) && !string.IsNullOrEmpty(pv.LicenseBinary))
                {
                    var licenseDialog = new LicenseWindow(libManager, pv);
                    if (licenseDialog.ShowLicense() == false)
                    {
                        _logger.Warn($"License for {pv.Name} was declined");
                        return;
                    }
                }


                foreach (var d in pv.Dependencies)
                {
                    if (!string.IsNullOrEmpty(pv.License) && !string.IsNullOrEmpty(pv.LicenseBinary))
                    {
                        var licenseWindow = new LicenseWindow(libManager, d);
                        if (licenseWindow.ShowLicense() == false)
                        {
                            _logger.Warn($"License for {pv.Name} was declined");
                            return;
                        }
                    }
                }
            }

            await _context.WriteStatusAsync($"Installing package {pv.Name} ...");

            var downloadPackageVersion = await TwinpackUtils.DownloadPackageVersionAndDependenciesAsync(libManager, pv, _twinpackServer, forceDownload: ForcePackageVersionDownload, cachePath: cachePath);
            await TwinpackUtils.InstallPackageVersionsAsync(libManager, downloadPackageVersion, cachePath: cachePath);
            await _context.WriteStatusAsync($"Adding package {pv.Name} to references ...");
            TwinpackUtils.AddReference(libManager, pv.Name, pv.Name, pv.Version, pv.DistributorName);
            IsNewReference = false;

            // update config
            _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != pv.Name && x.Repository == pv.Repository)
                                                        .Append(new Models.ConfigPlcPackage
                                                        {
                                                            Name = pv.Name,
                                                            Repository = pv.Repository,
                                                            Branch = pv.Branch,
                                                            Configuration = pv.Configuration,
                                                            Target = pv.Target,
                                                            Version = pv.Version,
                                                            DistributorName = pv.DistributorName
                                                        }).ToList();
        }

        public async Task<Models.Config> WritePlcConfigToConfigAsync(Models.ConfigPlcProject plcConfig)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var config = Models.ConfigFactory.Load(System.IO.Path.GetDirectoryName(_context.Solution.FullName));

            if (config != null)
            {
                _logger.Info($"Updating package configuration {Path.GetFullPath(config.FilePath)}");

                var projectIndex = config.Projects.FindIndex(x => x.Name == plcConfig.ProjectName);
                if (projectIndex < 0)
                {
                    config.Projects.Add(new Models.ConfigProject { Name = plcConfig.ProjectName, Plcs = new List<Models.ConfigPlcProject> { plcConfig } });
                }
                else
                {
                    var plcIndex = config.Projects[projectIndex].Plcs.FindIndex(x => x.Name == plcConfig.Name);

                    if (plcIndex < 0)
                        config.Projects[projectIndex].Plcs.Add(plcConfig);
                    else
                        config.Projects[projectIndex].Plcs[plcIndex] = plcConfig;
                }

                Models.ConfigFactory.Save(config);
            }
            else
            {
                _logger.Warn($"The solution doesn't have a package configuration");
            }

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

        public async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync();

                if (!_twinpackServer.LoggedIn)
                {
                    await _auth.LoginAsync();
                    if (!_twinpackServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful! Go to https://twinpack.dev/wp-login.php to register");
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
                btnLogin.Content = _twinpackServer.LoggedIn ? "Logout" : "Login";
            }

            try
            {
                _twinpackServer.InvalidateCache();
                await LoadAvailablePackagesAsync();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        private ScrollViewer GetScrollViewer(ListView view)
        {
            var border = VisualTreeHelper.GetChild(view, 0) as Decorator;
            return border.Child as ScrollViewer;
        }

        private async Task<Models.PackageGetResponse> LoadPackageAsync(int packageVersionId)
        {
            if (_isFetchingPackageVersion)
                return null;

            _isFetchingPackageVersion = true;

            try
            {
                return await _twinpackServer.GetPackageAsync(packageVersionId);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _isFetchingPackageVersion = false;
            }

            _isFetchingPackageVersion = false;
            return null;
        }

        private async Task LoadFirstPackageVersionsPageAsync(int? packageId)
        {
            if (_isFetchingPackageVersions || packageId == null)
                return;

            await LoadNextPackageVersionsPageAsync((int)packageId, true);
        }

        private async Task LoadNextPackageVersionsPageAsync(int packageId, bool reset = false)
        {
            if (_isFetchingPackageVersions)
                return;

            _isFetchingPackageVersions = true;
            _packageVersions = new List<Models.PackageVersionsItemGetResponse>();

            try
            {
                if (reset)
                    _currentPackageVersionsPage = 1;

                _packageVersions.AddRange(await _twinpackServer.GetPackageVersionsAsync(packageId, _currentPackageVersionsPage, _itemsPerPage));

                if (reset)
                {
                    Targets.Clear();
                    Configurations.Clear();
                    Branches.Clear();
                    Versions.Clear();
                }


                foreach (var item in _packageVersions.GroupBy(x => x.Branch).Select(x => x.FirstOrDefault()).Distinct())
                    Branches.Add(item);

                foreach (var item in _packageVersions.GroupBy(x => x.Target).Select(x => x.FirstOrDefault()).Distinct())
                    Targets.Add(item);

                foreach (var item in _packageVersions.GroupBy(x => x.Configuration).Select(x => x.FirstOrDefault()).Distinct())
                    Configurations.Add(item);

                _currentPackageVersionsPage++;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                _isFetchingPackageVersions = false;
            }
        }

        private async Task LoadAvailablePackagesAsync(string text = "")
        {
            await LoadNextCatalogPageAsync(text, true);
        }

        private async Task LoadNextCatalogPageAsync(string text = "", bool reset = false)
        {
            if (IsFetchingAvailablePackages)
                return;

            try
            {
                IsFetchingAvailablePackages = true;

                if (reset)
                    _currentCatalogPage = 1;

                var results = await _twinpackServer.GetCatalogAsync(text, _currentCatalogPage, _itemsPerPage);

                if (reset)
                {
                    _availablePackages.Clear();
                }
                foreach (var item in results)
                {
                    var catalogItem = new Models.CatalogItem(item);
                    _availablePackages.Add(catalogItem);
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
                IsFetchingAvailablePackages = false;
            }

        }

        private async Task LoadInstalledPackagesAsync()
        {
            if (IsFetchingInstalledPackages)
                return;

            try
            {
                IsFetchingInstalledPackages = true;
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (_plcConfig != null)
                {
                    _installedPackages.Clear();
                    foreach (var item in _plcConfig.Packages)
                    {
                        Models.CatalogItem catalogItem = new Models.CatalogItem(item);
                        var packageVersion = await _twinpackServer.GetPackageVersionAsync(item.Repository, item.Name, null, item.Configuration, item.Branch, item.Target);

                        if (packageVersion.PackageVersionId != null)
                        {
                            catalogItem = new Models.CatalogItem(packageVersion);
                            catalogItem.InstalledVersion = item.Version;
                            catalogItem.Update = packageVersion;
                        }

                        _installedPackages.Add(catalogItem);
                    }
                }

                IsNewReference = PackageVersion.PackageVersionId == null || !_installedPackages.Any(x => x.PackageId == PackageVersion.PackageId);
                IsPackageVersionPanelEnabled = _plcConfig != null;
                InstalledPackagesCount = _installedPackages.Count();
                UpdateablePackagesCount = _installedPackages.Where(x => x.IsUpdateable).Count();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsFetchingInstalledPackages = false;
            }
        }

        private async void PackageVersions_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var packageId = (CatalogView.SelectedItem as Models.CatalogItemGetResponse)?.PackageId;
            if (packageId == null)
                return;

            var scrollViewer = (ScrollViewer)sender;
            var position = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
            if (!_isFetchingPackageVersions && scrollViewer.VerticalOffset > _packageVersionsScrollPosition && position >= scrollViewer.ExtentHeight)
            {
                await LoadNextPackageVersionsPageAsync((int)packageId);
                _packageVersionsScrollPosition = scrollViewer.VerticalOffset;
            }
        }

        private async void Catalog_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var text = SearchTextBox.Text;
            var scrollViewer = (ScrollViewer)sender;
            var position = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
            if (!IsFetchingAvailablePackages && scrollViewer.VerticalOffset > _catalogScrollPosition && position >= scrollViewer.ExtentHeight)
            {
                await LoadNextCatalogPageAsync(text);
            }

            _catalogScrollPosition = scrollViewer.VerticalOffset;
        }

        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var item = (sender as ListView).SelectedItem as Models.CatalogItemGetResponse;
                if (item == null)
                    return;

                IsPackageLoading = true;
                _selectedPackageId = item.PackageId;
                await LoadFirstPackageVersionsPageAsync(_selectedPackageId);

                // check if the plc already contains the selected package
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Name);

                Models.PackageGetResponse package = null;
                if (_packageConfig != null)
                    package = await _twinpackServer.GetPackageAsync(_packageConfig.Repository, item.Name);
                else
                    package = await _twinpackServer.GetPackageAsync(item.Repository, item.Name);

                // check if selection has changed in the meanwhile
                if (package.PackageId != _selectedPackageId)
                    return;

                Package = package;

                var index = 0;
                IsNewReference = PackageVersion.PackageVersionId == null || !_installedPackages.Any(x => x.PackageId == PackageVersion.PackageId);
                if (PackageVersion.PackageVersionId != null)
                {
                    InstalledPackageVersion = PackageVersion.Version ?? "n/a";
                    index = _packageVersions.IndexOf(_packageVersions.FirstOrDefault(x => x.PackageVersionId == PackageVersion.PackageVersionId));
                }
                else if (_packageConfig != null)
                {
                    InstalledPackageVersion = _packageConfig.Version ?? "n/a";
                }

                BranchesView.Visibility = Branches.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                TargetsView.Visibility = Targets.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                ConfigurationsView.Visibility = Configurations.Count() > 1 ? Visibility.Visible : Visibility.Collapsed;
                BranchesView.SelectedIndex = 0;
                TargetsView.SelectedIndex = 0;
                ConfigurationsView.SelectedIndex = 0;

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

        private void FilterPackageVersions()
        {
            Versions.Clear();
            if (BranchesView.SelectedItem == null || ConfigurationsView.SelectedItem == null || TargetsView.SelectedItem == null)
                return;

            foreach (var item in _packageVersions.Where(x => x.Branch == (BranchesView.SelectedItem as Models.PackageVersionsItemGetResponse).Branch &&
                                                        x.Configuration == (ConfigurationsView.SelectedItem as Models.PackageVersionsItemGetResponse).Configuration &&
                                                        x.Target == (TargetsView.SelectedItem as Models.PackageVersionsItemGetResponse).Target))
                Versions.Add(item);
        }

        private async void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                IsPackageLoading = true;

                if (sender as ComboBox != VersionsView)
                {
                    FilterPackageVersions();
                    VersionsView.SelectedIndex = 0;
                }
                else
                {
                    var item = (sender as ComboBox).SelectedItem as Models.PackageVersionsItemGetResponse;
                    if (item != null)
                    {
                        PackageVersion = await _twinpackServer.GetPackageVersionAsync(item.Repository,
                            item.Name, item.Version, item.Configuration, item.Branch, item.Target,
                            includeBinary: false, cachePath: null);
                    }

                }
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

        public async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _context.Logger = LogManager.Configuration.FindTargetByName<VsOutputWindowTarget>("VsOutputWindowTarget");

                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync();

                IsPackageLoading = false;
                Package = new Models.PackageGetResponse();
                PackageVersion = new Models.PackageVersionGetResponse();

                _twinpackServer.InvalidateCache();
                _context.Dte.ExecuteCommand("File.SaveAll");

                await LoadInstalledPackagesAsync();
                await LoadAvailablePackagesAsync(SearchTextBox.Text);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async void CreateConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _context?.Logger?.ActivateAsync();

                var config = await Models.ConfigFactory.CreateFromSolutionAsync(_context.Solution, _twinpackServer);
                if (config == null)
                {
                    throw new Exception("Generating the configuration file failed, please create the configuration file manually!");
                }
                else
                {
                    IsCreateConfigVisible = false;
                    var path = Models.ConfigFactory.Save(config);

                    if (MessageBoxResult.Yes == MessageBox.Show($"The configuration file was successfully created " +
                        $"in {config.FilePath} for your TwinCAT solution, do you want to " +
                        $"review and/or edit it?", "Configuration", MessageBoxButton.YesNo))
                    {
                        Process process = new Process
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
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Configuration failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ShowProjectUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = PackageVersion.ProjectUrl,
                        UseShellExecute = true
                    }
                };

                process.Start();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }


        public async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isBrowsingAvailablePackages)
            {
                var text = ((TextBox)sender).Text;
                _searchText = text;
                await Task.Delay(100);

                if (_searchText == text)
                    await LoadAvailablePackagesAsync(text);
            }
            else
            {

            }
        }
    }
}
