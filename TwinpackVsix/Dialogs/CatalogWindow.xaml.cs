using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Meziantou.Framework.Win32;
using Microsoft.VisualStudio.Threading;

namespace Twinpack.Dialogs
{
    public partial class CatalogWindow : UserControl
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private ObservableCollection<Models.CatalogItemGetResponse> _catalog = new ObservableCollection<Models.CatalogItemGetResponse>();
        private ObservableCollection<Models.PackageVersionsItemGetResponse> _packageVersions = new ObservableCollection<Models.PackageVersionsItemGetResponse>();
        private Models.PackageGetResponse _packageVersion = new Twinpack.Models.PackageGetResponse();
        private int _currentCatalogPage = 1;
        private int _currentPackageVersionsPage = 1;
        private int _itemsPerPage = 20;
        private double _catalogScrollPosition = 0;
        private double _packageVersionsScrollPosition = 0;
        private bool _isCatalogFetching = false;
        private bool _isPackageVersionsFetching = false;
        private bool _isPackageVersionFetching = false;
        private string _searchText = "";
        private string _username;
        private string _password;
        private bool _loggedIn;

        public CatalogWindow()
        {
            DataContext = this;

            InitializeComponent();
            PackageVersionView.Visibility = Visibility.Hidden;
            CatalogView.ItemsSource = _catalog;
            CatalogView.SelectionChanged += Catalog_SelectionChanged;
            CatalogView.Loaded += (s, e) => {
                var sv = GetScrollViewer(CatalogView);
                if (sv != null)
                    sv.ScrollChanged += Catalog_ScrollChanged;
            };

            PackageVersionInstalledTextBox.Text = "n/a";
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
            var credentials = CredentialManager.ReadCredential("Twinpack");
            if (credentials != null)
            {
                try
                {
                    _loggedIn = await TwinpackService.LoginAsync(credentials.UserName, credentials.Password);
                    btnLogin.Content = _loggedIn ? "Logout" : "Login";

                    if (_loggedIn)
                    {
                        _username = credentials.UserName;
                        _password = credentials.Password;
                    }
                }
                catch(Exception ex)
                {
                    _logger.Error(ex.Message);
                }

            }

            await LoadFirstCatalogPageAsync();
        }

        
        public void EditPackageButton_Click(object sender, RoutedEventArgs e)
        {
            var packageId = (CatalogView.SelectedItem as Models.CatalogItemGetResponse)?.PackageId;
            if (packageId == null)
                return;

            var packageVersionId = (PackageVersionsView.SelectedItem as Models.PackageVersionsItemGetResponse)?.PackageVersionId;

            var packagePublish = new PublishWindow(packageId, packageVersionId, _username, _password);
            packagePublish.ShowDialog();
        }

        public async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if(!_loggedIn)
            {
                var credentials = CredentialManager.PromptForCredentials(
                    messageText: $"Login to your Twinpack Server account. Logging in will give you access to additional features. " +
                    $"It enables you to intall packages that are maintained by you, but not yet released. It also allows you to upload a new package into your Twinpack repository.", 
                    captionText: "Twinpack Server login", saveCredential: CredentialSaveOption.Hidden);

                if(credentials != null)
                {
                    try
                    {
                        _loggedIn = await TwinpackService.LoginAsync(credentials.UserName, credentials.Password);
                        if (_loggedIn)
                            CredentialManager.WriteCredential("Twinpack", credentials.UserName, credentials.Password, CredentialPersistence.LocalMachine);
                    }
                    catch (Exceptions.LoginException ex)
                    {
                        try
                        {
                            CredentialManager.DeleteCredential("Twinpack");
                        }
                        catch { }
                        credentials = null;
                        MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch(Exception ex)
                    {
                        _logger.Error(ex.Message);
                    }
                    _username = credentials?.UserName;
                    _password = credentials?.Password;
                }
            }
            else
            {
                try
                {
                    CredentialManager.DeleteCredential("Twinpack");
                }
                catch { }

                _loggedIn = false;
                _username = "";
                _password = "";
            }


            btnLogin.Content = _loggedIn ? "Logout" : "Login";

            await LoadFirstCatalogPageAsync();
        }

        private ScrollViewer GetScrollViewer(ListView view)
        {
            var border = VisualTreeHelper.GetChild(view, 0) as Decorator;
            return border.Child as ScrollViewer;
        }

        private async Task<Twinpack.Models.PackageGetResponse> LoadPackageAsync(int packageVersionId)
        {
            if (_isPackageVersionFetching)
                return null;

            _isPackageVersionFetching = true;

            try
            {
                return await Twinpack.TwinpackService.GetPackageAsync(_username, _password, packageVersionId);
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

                var results = await Twinpack.TwinpackService.GetPackageVersionsAsync(_username, _password, packageId, _currentPackageVersionsPage, _itemsPerPage);

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


        private async Task LoadFirstCatalogPageAsync(string text="")
        {
            if (_isCatalogFetching)
                return;

            await LoadNextCatalogPageAsync(text, true);
        }

        private async Task LoadNextCatalogPageAsync(string text = "", bool reset=false)
        {
            if (_isCatalogFetching)
                return;

            _isCatalogFetching = true;

            try
            {
                if(reset)
                    _currentCatalogPage = 1;

                var results = await Twinpack.TwinpackService.GetCatalogAsync(_username, _password, text, _currentCatalogPage, _itemsPerPage);

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

            PackageDisplayName.Text = item.DisplayName;
            PackageRepository.Text = item.Repository;

            PackageVersionsView.IsEnabled = _packageVersions.Any();
            PackageVersionView.Visibility = Visibility.Hidden;

            if (_packageVersions.Any())
                PackageVersionsView.SelectedIndex = 0;

            btnEditPackage.Visibility = _catalog.Any() && item.Repository == _username ? Visibility.Visible : Visibility.Hidden;
            PackageVersionView.Visibility = _catalog.Any() ? Visibility.Visible : Visibility.Hidden;
            CatalogView.IsEnabled = true;
        }

        private void PackageVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (sender as ComboBox).SelectedItem as Twinpack.Models.PackageVersionsItemGetResponse;
            if (item == null)
                return;

            PackageNotes.Text = item.Notes;
            PackageBranch.Text = item.Branch;
            PackageTarget.Text = item.Target;
            PackageConfiguration.Text = item.Configuration;
            PackageAuthors.Text = item.Authors;
            PackageProjectUrl.Text = item.ProjectUrl;
            PackageVersion.Text = item.Version;
            PackageLicense.Text = item.License;
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
