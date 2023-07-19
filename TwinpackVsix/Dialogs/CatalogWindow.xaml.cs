using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Threading;
using TCatSysManagerLib;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private PackageContext _context;
        private EnvDTE.Project _plc;
        private Models.ConfigPlcProject _plcConfig;
        private Models.ConfigPlcPackage _packageConfig;

        private List<Models.CatalogItem> _availablePackages = new List<Models.CatalogItem>();
        private List<Models.CatalogItem> _installedPackages = new List<Models.CatalogItem>();
        private ObservableCollection<Models.CatalogItem> _catalog = new ObservableCollection<Models.CatalogItem>();

        private ObservableCollection<Models.PackageVersionsItemGetResponse> _packageVersions = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Twinpack.Models.PackageVersionGetResponse();
        
        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 20;
        private double _catalogScrollPosition = 0;
        private double _packageVersionsScrollPosition = 0;
        private bool _isCatalogFetching = false;
        private bool _isPackageVersionsFetching = false;
        private bool _isPackageVersionFetching = false;
        private string _searchText = "";
        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

        private bool _isBrowsingCatalog;
        private bool _isBrowsingInstalledPackages;
        private bool _isBrowsingUpdatablePackages;

        private bool _isNewReference;
        private bool _isPackageVersionPanelEnabled;
        private string _installedPackageVersion;

        public event PropertyChangedEventHandler PropertyChanged;

        public Models.PackageGetResponse Package
        {
            get { return _package; }
            set
            {
                _package = value;
                skpPackage.DataContext = _package;
            }
        }
        
        public Models.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                skpPackageVersion.DataContext = _packageVersion;            
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

        public ObservableCollection<Models.CatalogItem> Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catalog)));
            }
        }

        public CatalogWindow(PackageContext context)
        {
            _auth = new Authentication(_twinpackServer);
            _context = context;
            _isBrowsingCatalog = true;
            DataContext = this;

            InitializeComponent();
            PackageVersionView.Visibility = Visibility.Hidden;

            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            CatalogView.Loaded += (s, e) =>
            {
                var sv = GetScrollViewer(CatalogView);
                if (sv != null)
                    sv.ScrollChanged += Catalog_ScrollChanged;
            };

            FilterByInstalledSettingsCheck.IsEnabled = false;
            FilterByInstalledSettingsCheck.IsChecked = true;
            PackageVersionsView.ItemsSource = _packageVersions;
            PackageVersionsView.SelectionChanged += PackageVersions_SelectionChanged;
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
                _logger.Error(ex.Message);
            }
            finally
            {
                btnLogin.Content = _twinpackServer.LoggedIn ? "Logout" : "Login";
            }

            try
            {
                await ReloadPlcConfigAsync();
                await LoadFirstCatalogPageAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
        }


        public void EditPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var packageId = (CatalogView.SelectedItem as Models.CatalogItemGetResponse)?.PackageId;
            if (packageId == null)
                return;

            var packageVersionId = (PackageVersionsView.SelectedItem as Models.PackageVersionsItemGetResponse)?.PackageVersionId;
            var packagePublish = new PublishWindow(_context, _plc, packageId, packageVersionId);
            packagePublish.ShowDialog();
        }

        public async void AddPackageButton_Click(object sender, RoutedEventArgs e)
        {
            await AddOrUpdatePackageAsync();
        }

        public async void UpdatePackageButton_Click(object sender, RoutedEventArgs e)
        {
            await AddOrUpdatePackageAsync();
        }

        public void ShowUpdateablePackages_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingCatalog = false;
            _isBrowsingUpdatablePackages = true;
            _isBrowsingInstalledPackages = false;

            Catalog.Clear();
            foreach (var p in _installedPackages.Where(x => x.InstalledVersion != x.UpdateVersion))
            {
                Catalog.Add(p);
            }
        }

        public void ShowInstalledPackages_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingCatalog = false;
            _isBrowsingUpdatablePackages = false;
            _isBrowsingInstalledPackages = true;
            Catalog.Clear();
            foreach (var p in _installedPackages)
            {
                Catalog.Add(p);
            }

        }

        public void ShowCatalog_Click(object sender, RoutedEventArgs e)
        {
            _isBrowsingCatalog = true;
            _isBrowsingUpdatablePackages = false;
            _isBrowsingInstalledPackages = false;

            Catalog.Clear();
            foreach(var p in _availablePackages)
            {
                Catalog.Add(p);
            }
        }

        public async Task AddOrUpdatePackageAsync()
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IsPackageVersionPanelEnabled = false;

                if (PackageVersion.PackageVersionId == null)
                    return;

                var cachePath = $@"{System.IO.Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
                PackageVersion = await _twinpackServer.GetPackageVersionAsync((int)_packageVersion.PackageVersionId,
                    includeBinary: true, cachePath: cachePath);

                var plc = (_plc.Object as dynamic);
                var sysManager = plc.SystemManager as ITcSysManager2;
                var suffix = PackageVersion.Compiled == 1 ? "compiled-library" : "library";
                var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;
                libManager.InstallLibrary("System", $@"{cachePath}\{PackageVersion.Target}\{PackageVersion.Name}_{PackageVersion.Version}.{suffix}", bOverwrite: true);

                TwinpackUtils.AddReference(libManager, Package.Name, Package.Name, PackageVersion.Version, _package.DistributorName);

                IsNewReference = false;
                InstalledPackageVersion = PackageVersion.Version;

                // update config
                _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != _package.Name && x.Repository == _package.Repository)
                                                         .Append(new Models.ConfigPlcPackage
                                                         {
                                                             Name = Package.Name,
                                                             Repository = _package.Repository,
                                                             Branch = PackageVersion.Branch,
                                                             Configuration = PackageVersion.Configuration,
                                                             Target = PackageVersion.Target,
                                                             Version = PackageVersion.Version,
                                                             DistributorName = Package.DistributorName
                                                         });
            }
            catch (Exception ex)
            {
                _logger.Trace(ex.Message);
            }
            finally
            {
                IsPackageVersionPanelEnabled = true;
            }
        }

        public async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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
                _logger.Error(ex.Message);
            }
            finally
            {
                btnLogin.Content = _twinpackServer.LoggedIn ? "Logout" : "Login";
            }

            try
            {
                await LoadFirstCatalogPageAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private ScrollViewer GetScrollViewer(ListView view)
        {
            var border = VisualTreeHelper.GetChild(view, 0) as Decorator;
            return border.Child as ScrollViewer;
        }

        private async Task<Models.PackageGetResponse> LoadPackageAsync(int packageVersionId)
        {
            if (_isPackageVersionFetching)
                return null;

            _isPackageVersionFetching = true;

            try
            {
                return await _twinpackServer.GetPackageAsync(packageVersionId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            finally
            {
                _isPackageVersionFetching = false;
            }

            _isPackageVersionFetching = false;
            return null;
        }

        private async Task LoadFirstPackageVersionsPageAsync(int? packageId)
        {
            if (_isPackageVersionsFetching || packageId == null)
                return;

            await LoadNextPackageVersionsPageAsync((int)packageId, true);
        }

        private async Task LoadNextPackageVersionsPageAsync(int packageId, bool reset = false)
        {
            if (_isPackageVersionsFetching)
                return;

            _isPackageVersionsFetching = true;

            try
            {
                if (reset)
                    _currentPackageVersionsPage = 1;

                var results = await _twinpackServer.GetPackageVersionsAsync(packageId, _currentPackageVersionsPage, _itemsPerPage);

                if (reset)
                    _packageVersions.Clear();
                foreach (var item in results)
                    _packageVersions.Add(item);

                _currentPackageVersionsPage++;

            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            finally
            {
                _isPackageVersionsFetching = false;
            }

            _isPackageVersionsFetching = false;
        }


        private async Task LoadFirstCatalogPageAsync(string text = "")
        {
            if (_isCatalogFetching)
                return;

            await LoadNextCatalogPageAsync(text, true);
        }

        private async Task LoadNextCatalogPageAsync(string text = "", bool reset = false)
        {
            if (_isCatalogFetching)
                return;

            _isCatalogFetching = true;

            try
            {
                if (reset)
                    _currentCatalogPage = 1;

                var results = await _twinpackServer.GetCatalogAsync(text, _currentCatalogPage, _itemsPerPage);

                if (reset)
                    _availablePackages.Clear();
                foreach (var item in results)
                    _availablePackages.Add(new Models.CatalogItem(item));

                _currentCatalogPage++;

            }
            catch (Exception ex)
            {
                // Handle API request error
                Console.WriteLine($"Error fetching items: {ex.Message}");
            }
            finally
            {
                _isCatalogFetching = false;
            }

            _isCatalogFetching = false;
        }

        private async Task ReloadPlcConfigAsync()
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _plc = null;
            if (_context.Dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
                _plc = activeSolutionProjects.GetValue(0) as EnvDTE.Project;

            if (_plc != null)
            {
                _plcConfig = await Models.ConfigPlcProjectFactory.MapPlcConfigToPlcProjAsync(_context.Solution, _plc, _twinpackServer);
            }

            if (_plcConfig != null)
            {
                _installedPackages.Clear();
                foreach (var item in _plcConfig.Packages)
                {
                    Models.CatalogItem catalogItem = new Models.CatalogItem(item);
                    var packageVersion = await _twinpackServer.GetPackageVersionAsync(item.Repository, item.Name, item.Version, item.Configuration, item.Branch, item.Target);

                    // todo: check for updates here
                    //if (packageVersion.PackageVersionId != null)
                    //    packageVersion = await _twinpackServer.ResolvePackageVersionAsync()

                    _installedPackages.Add(catalogItem);
                }
            }

            IsPackageVersionPanelEnabled = _plcConfig != null;
        }

        private async void PackageVersions_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var packageId = (CatalogView.SelectedItem as Twinpack.Models.CatalogItemGetResponse)?.PackageId;
            if (packageId == null)
                return;

            var scrollViewer = (ScrollViewer)sender;
            var position = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
            if (!_isCatalogFetching && scrollViewer.VerticalOffset > _packageVersionsScrollPosition && position >= scrollViewer.ExtentHeight)
            {
                await LoadNextPackageVersionsPageAsync((int)packageId);
            }

            _packageVersionsScrollPosition = scrollViewer.VerticalOffset;
        }

        private async void Catalog_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var text = SearchTextBox.Text;
            var scrollViewer = (ScrollViewer)sender;
            var position = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight;
            if (!_isCatalogFetching && scrollViewer.VerticalOffset > _catalogScrollPosition && position >= scrollViewer.ExtentHeight)
            {
                await LoadNextCatalogPageAsync(text);
            }

            _catalogScrollPosition = scrollViewer.VerticalOffset;
        }

        private async void Catalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var item = (sender as ListView).SelectedItem as Twinpack.Models.CatalogItemGetResponse;
                if (item == null)
                {
                    PackageVersionsView.IsEnabled = false;
                    btnEditPackage.Visibility = Visibility.Hidden;
                    return;
                }

                int? packageId = item.PackageId;

                CatalogView.IsEnabled = false;
                await LoadFirstPackageVersionsPageAsync(packageId);

                PackageVersionsView.IsEnabled = _packageVersions.Any();
                PackageVersionView.Visibility = Visibility.Hidden;

                // check if the plc already contains the selected package
                _packageConfig = _plcConfig?.Packages?.FirstOrDefault(x => x.Name == item.Name);

                if (_packageConfig != null)
                {
                    Package = await _twinpackServer.GetPackageAsync(_packageConfig.Repository, item.Name);
                    PackageVersion = await _twinpackServer.GetPackageVersionAsync(_packageConfig.Repository,
                        _packageConfig.Name, _packageConfig.Version, _packageConfig.Configuration, _packageConfig.Branch, _packageConfig.Target,
                        includeBinary: false, cachePath: null);
                }
                else
                {
                    Package = await _twinpackServer.GetPackageAsync(item.Repository, item.Name);
                    PackageVersion = new Models.PackageVersionGetResponse();
                }


                var index = 0;
                IsNewReference = PackageVersion.PackageVersionId == null;
                if (PackageVersion.PackageVersionId != null)
                {
                    InstalledPackageVersion = PackageVersion.Version ?? "n/a";
                    index = _packageVersions.IndexOf(_packageVersions.FirstOrDefault(x => x.PackageVersionId == PackageVersion.PackageVersionId));
                }

                if (_packageVersions.Any())
                    PackageVersionsView.SelectedIndex = index >= 0 ? index : 0;

                btnEditPackage.Visibility = _availablePackages.Any() && item.Repository == _twinpackServer.Username ? Visibility.Visible : Visibility.Hidden;
                PackageVersionView.Visibility = _availablePackages.Any() ? Visibility.Visible : Visibility.Hidden;
                CatalogView.IsEnabled = true;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex.Message);
            }

        }

        private async void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (sender as ComboBox).SelectedItem as Twinpack.Models.PackageVersionsItemGetResponse;
            if (item == null)
                return;

            PackageVersion = await _twinpackServer.GetPackageVersionAsync(item.Repository,
                item.Name, item.Version, item.Configuration, item.Branch, item.Target,
                includeBinary: false, cachePath: null);
        }

        public async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            var text = SearchTextBox.Text;

            try
            {
                if (_isBrowsingInstalledPackages || _isBrowsingUpdatablePackages)
                    await ReloadPlcConfigAsync();
                else if (_isBrowsingCatalog)
                    await LoadFirstCatalogPageAsync(text);
            }
            catch(Exception ex)
            {
                _logger.Trace(ex);

            }
        }

        public async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(_isBrowsingCatalog)
            {
                var text = ((TextBox)sender).Text;
                _searchText = text;
                await Task.Delay(100);

                PackageVersionView.Visibility = Visibility.Hidden;

                if (_searchText == text)
                    await LoadFirstCatalogPageAsync(text);
            }
            else
            {

            }

            PackageVersionView.Visibility = _availablePackages.Any() ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
