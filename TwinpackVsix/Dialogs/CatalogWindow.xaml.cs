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

        private ObservableCollection<Models.CatalogItemGetResponse> _catalog = new ObservableCollection<Models.CatalogItemGetResponse>();
        private ObservableCollection<Models.PackageVersionsItemGetResponse> _packageVersions = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
        private Models.PackageGetResponse _package = new Twinpack.Models.PackageGetResponse();
        private Models.PackageGetResponse _packageVersion = new Twinpack.Models.PackageVersionGetResponse();
        
        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 20;
        private double _catalogScrollPosition = 0;
        private double _packageVersionsScrollPosition = 0;
        private bool _isCatalogFetching = false;
        private bool _isPackageVersionsFetching = false;
        private bool _isPackageVersionFetching = false;
        private string _searchText = "";
        private Authentication _auth = new Authentication();

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
                skpPackageVersion.DataContext = _package            
            }
        }
        
        public Models.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                skpPackage.DataContext = _package;            
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
            _context = context;
            DataContext = this;

            InitializeComponent();
            PackageVersionView.Visibility = Visibility.Hidden;
            CatalogView.ItemsSource = _catalog;
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
                await _auth.InitializeAsync();

                _plc = null;
                if (_context.Dte.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects.Length > 0)
                    _plc = activeSolutionProjects.GetValue(0) as EnvDTE.Project;

                if(_plc != null)
                    _plcConfig = await Models.ConfigPlcProjectFactory.MapPlcConfigToPlcProj(_context.Solution, _plc);

                IsPackageVersionPanelEnabled = _plcConfig != null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            finally
            {
                btnLogin.Content = _auth.LoggedIn ? "Logout" : "Login";
            }

            try
            {
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

            var packagePublish = new PublishWindow(_context, null, packageId, packageVersionId, _auth.Username, _auth.Password);
            packagePublish.ShowDialog();
        }

        public async void UpdatePackageButton_Click(object sender, RoutedEventArgs e)
        {
            await AddOrUpdatePackageAsync();
        }

        public async void AddPackageButton_Click(object sender, RoutedEventArgs e)
        {
            await AddOrUpdatePackageAsync();
        }

        public async Task AddOrUpdatePackageAsync()
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IsPackageVersionPanelEnabled = false;

                if (_package.PackageVersionId == null)
                    return;

                var cachePath = $@"{System.IO.Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
                PackageVersion = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, (int)_package.PackageVersionId,
                    includeBinary: true, cachePath: cachePath);

                var plc = (_plc.Object as dynamic);
                var sysManager = plc.SystemManager as ITcSysManager2;
                var suffix = _package.Compiled == 1 ? "compiled-library" : "library";
                var libManager = sysManager.LookupTreeItem(plc.PathName + "^References") as ITcPlcLibraryManager;
                libManager.InstallLibrary("System", $@"{cachePath}\{_package.Target}\{_package.Name}_{_package.Version}.{suffix}", bOverwrite: true);

                TwinpackService.AddReference(libManager, _package.Name, _package.Name, _package.Version, _package.DistributorName);

                IsNewReference = false;
                InstalledPackageVersion = _package.Version;

                // update config
                _plcConfig.Packages = _plcConfig.Packages.Where(x => x.Name != _package.Name && x.Repository == _package.Repository)
                                                         .Append(new Models.ConfigPlcPackage
                                                         {
                                                             Name = _package.Name,
                                                             Repository = _package.Repository,
                                                             Branch = _package.Branch,
                                                             Configuration = _package.Configuration,
                                                             Target = _package.Target,
                                                             Version = _package.Version,
                                                             DistributorName = _package.DistributorName
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
                if (!_auth.LoggedIn)
                {
                    await _auth.LoginAsync();
                    if (!_auth.LoggedIn)
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
                btnLogin.Content = _auth.LoggedIn ? "Logout" : "Login";
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
                return await TwinpackService.GetPackageAsync(_auth.Username, _auth.Password, packageVersionId);
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

                var results = await TwinpackService.GetPackageVersionsAsync(_auth.Username, _auth.Password, packageId, _currentPackageVersionsPage, _itemsPerPage);

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

                var results = await Twinpack.TwinpackService.GetCatalogAsync(_auth.Username, _auth.Password, text, _currentCatalogPage, _itemsPerPage);

                if (reset)
                    _catalog.Clear();
                foreach (var item in results)
                    _catalog.Add(item);

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
                    Package = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, _auth.Username, _packageConfig.Name);
                    
                    PackageVersion = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, _auth.Username,
                        _packageConfig.Name, _packageConfig.Version, _packageConfig.Configuration, _packageConfig.Branch, _packageConfig.Target,
                        includeBinary: false, cachePath: null);
                }
                else
                {
                    Package = new Models.PackageGetResponse();                
                    PackageVersion = new Models.PackageVersionGetResponse();
                }


                var index = 0;
                IsNewReference = _package.PackageVersionId == null;
                if (_package.PackageVersionId != null)
                {
                    InstalledPackageVersion = _package.Version ?? "n/a";
                    index = _packageVersions.IndexOf(_packageVersions.FirstOrDefault(x => x.PackageVersionId == _package.PackageVersionId));
                }

                if (_packageVersions.Any())
                    PackageVersionsView.SelectedIndex = index >= 0 ? index : 0;

                btnEditPackage.Visibility = _catalog.Any() && item.Repository == _auth.Username ? Visibility.Visible : Visibility.Hidden;
                PackageVersionView.Visibility = _catalog.Any() ? Visibility.Visible : Visibility.Hidden;
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

            PackageVersion = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, _auth.Username,
                item.Name, item.Version, item.Configuration, item.Branch, item.Target,
                includeBinary: false, cachePath: null);
            skpPackageVersion.DataContext = PackageVersion;
        }

        public async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            var text = SearchTextBox.Text;

            await LoadFirstCatalogPageAsync(text);
        }

        public async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            _searchText = text;
            await Task.Delay(100);

            PackageVersionView.Visibility = Visibility.Hidden;

            if (_searchText == text)
                await LoadFirstCatalogPageAsync(text);

            PackageVersionView.Visibility = _catalog.Any() ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
