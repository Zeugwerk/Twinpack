using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Meziantou.Framework.Win32;
using Microsoft.VisualStudio.Threading;
using TCatSysManagerLib;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace Twinpack.Dialogs
{
    public partial class PublishWindow : Window, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private PackageContext _context;
        private EnvDTE.Project _plc;
        private Models.ConfigPlcProject _plcConfig;

        private bool _isConfigured;
        private bool _isLoading;
        private string _loadingText;

        private bool _isGeneralDataReadOnly;
        private bool _isVersionWrongFormat;
        private bool _isNewUser;
        private bool _isNewPackage;
        private bool _isNewPackageVersion;
        private string _iconFile;
        private BitmapImage _iconImage;

        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

        private IEnumerable<Models.PackageVersionGetResponse> _dependencies;
        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Models.PackageVersionGetResponse();
        private Models.PackageVersionGetResponse _packageVersionLatest = new Models.PackageVersionGetResponse();

        public event PropertyChangedEventHandler PropertyChanged;

        public PublishWindow(PackageContext context, EnvDTE.Project plc = null, int? packageId = null, int? packageVersionId = null, string username = "", string password = "")
        {
            _auth = new Authentication(_twinpackServer);
            _context = context;
            _plc = plc;
            DataContext = this;

            IsNewPackage = packageId == null;
            IsGeneralDataReadOnly = false;

            _package.PackageId = packageId;
            _packageVersion.PackageVersionId = packageVersionId;
            _packageVersion.Target = "TC3.1";
            _packageVersion.Configuration = "Release";

            Loaded += Window_Loaded;
            InitializeComponent();
        }

        private async Task LoginAsync()
        {
            while (!_twinpackServer.LoggedIn)
            {
                var message = "";
                try
                {
                    await _auth.LoginAsync();
                    if (!_twinpackServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful!");
                }
                catch (Exceptions.LoginException ex)
                {
                    message = ex.Message;
                    _logger.Error(ex.Message);
                }
                catch (Exception)
                {
                    message = "You have to login to the Twinpack server to publish packages.";
                }

                if(!_twinpackServer.LoggedIn)
                {
                    if (MessageBox.Show($@"{message}\n\n Do you want to register or reset your password=", "Login failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        Process.Start("https://twinpack.dev/wp-login.php");
                    }
                    else
                    {
                        Close();
                        return;
                    }
                }
            }

            IsNewUser = _twinpackServer.UserInfo.DistributorName == null;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IsEnabled = false;
                IsLoading = true;
                LoadingText = "Loading ...";
                await LoginAsync();

                IsNewUser = _twinpackServer.UserInfo.DistributorName == null;
                IsGeneralDataReadOnly = false;

                try
                {
                    if (_plc != null)
                    {
                        var config = Models.ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));
                        if (config != null)
                        {
                            LoadingText = "Loading package configuration ...";
                            _plcConfig = Models.ConfigPlcProjectFactory.MapPlcConfigToPlcProj(config, _plc);
                        }
                        else
                        {
                            LoadingText = "Creating temporary package configuration ...";
                            _plcConfig = await Models.ConfigPlcProjectFactory.CreateAsync(_context.Solution, _plc, _twinpackServer);
                        }

                        IsConfigured = _plcConfig != null;
                    }
                }
                catch (Exception ex)
                {
                    IsConfigured = false;
                    _plcConfig = null;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }


                LoadingText = "Retrieving package ...";
                if (_plcConfig != null)
                {
                    _package = await _twinpackServer.GetPackageAsync(_twinpackServer.Username, _plcConfig.Name);
                    _plcConfig.DistributorName = _twinpackServer.UserInfo.DistributorName ?? _plcConfig.DistributorName;
                }

                if(_plcConfig == null && _package.PackageId != null)
                    _package = await _twinpackServer.GetPackageAsync((int)_package.PackageId);

                LoadingText = "Retrieving package version ...";
                if (_packageVersion.PackageVersionId == null && _package.PackageId != null)
                {
                    // try to get the specific version
                    IsNewPackageVersion = false;
                    _packageVersion = await _twinpackServer.GetPackageVersionAsync(_package.Repository, _package.Name, _packageVersion.Version, "Release", "main", "TC3.1", false, null);

                    // fallback to the latest available version
                    if(_packageVersion.PackageVersionId == null)
                    {
                        IsNewPackageVersion = true;
                        _packageVersion.PackageVersionId = (await _twinpackServer.GetPackageVersionsAsync((int)_package.PackageId, 1, 1)).FirstOrDefault()?.PackageVersionId;
                    }
                }
                else if(_plcConfig != null)
                {
                    IsNewPackageVersion = true;
                }

                if (_packageVersion.PackageVersionId != null)
                { 
                    try
                    {
                        _packageVersion = await _twinpackServer.GetPackageVersionAsync((int)_packageVersion.PackageVersionId, includeBinary: false);
                        _packageVersionLatest = await _twinpackServer.GetPackageVersionAsync(_packageVersion.Repository, _packageVersion.Name, null, _packageVersion.Configuration, _packageVersion.Branch, _packageVersion.Target); 
                        Dependencies = _packageVersion.Dependencies;                                                
                    }
                    catch (Exceptions.GetException ex)
                    {
                        MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message);
                    }
                }

                PackageName = _package?.Name ?? _plcConfig.Name;
                DisplayName = _package?.DisplayName ?? _plcConfig.DisplayName;
                Description = _package?.Description ?? _plcConfig.Description;
                Entitlement = _package?.Entitlement ?? _plcConfig.Entitlement;
                ProjectUrl = _package?.ProjectUrl ?? _plcConfig.ProjectUrl;
                DistributorName = _package?.DistributorName ?? _plcConfig.DistributorName;
                License = _package?.License ?? _plcConfig.License;
                LicenseFile = _plcConfig?.LicenseFile;
                Version = _packageVersion?.Version ?? _plcConfig?.Version;
                Authors = _packageVersion?.Authors ?? _plcConfig?.Authors;
                Entitlement = _packageVersion?.Entitlement ?? _plcConfig?.Entitlement;
                License = _packageVersion?.License ?? _plcConfig?.License;
                LicenseFile = _plcConfig?.LicenseFile;
                IconFile = _plcConfig?.IconFile;
                IconImage = TwinpackUtils.IconImage(_package?.IconUrl);
                //Configuration = "Release";
                //Target = "TC3.1";
                //Branch = "main";
                Notes = _packageVersion?.Notes;
                Version = _packageVersion?.Version;
                LatestVersion = _packageVersionLatest?.Version;
                Dependencies = _plcConfig?.Packages?.Select(x => new Models.PackageVersionGetResponse
                {
                    Repository = x.Repository,
                    DistributorName = x.DistributorName,
                    Name = x.Name,
                    Branch = x.Branch,
                    Target = x.Target,
                    Configuration = x.Configuration,
                    Version = x.Version
                }) ?? new List<Models.PackageVersionGetResponse>();

                IsConfigured = _plcConfig != null && _plcConfig.Name == _package.Name && _plcConfig.DistributorName == _package.DistributorName && _package.Repository == _twinpackServer.Username;
                IsNewPackage = _package.PackageId == null;
                IsGeneralDataReadOnly = _package.Repository != _twinpackServer.Username;
             }
            catch(Exception ex)
            {
                _logger.Trace(ex);
            }
            finally
            {
                IsEnabled = true;
                IsLoading = false;
            }
        }

        public string LoadingText
        {
            get { return _loadingText; }
            set
            {
                _loadingText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadingText)));
            }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set
            {
                _isLoading = value;
                if (_isLoading)
                    LoadingText = "Loading ...";
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
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

        public bool IsNewUser
        {
            get { return _isNewUser; }
            set
            {
                _isNewUser = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewUser)));
            }
        }

        public bool IsNewPackage
        {
            get { return _isNewPackage; }
            set
            {
                _isNewPackage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewPackage)));
            }
        }

        public bool IsNewPackageVersion
        {
            get { return _isNewPackageVersion; }
            set
            {
                _isNewPackageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewPackageVersion)));
            }
        }

        public bool IsGeneralDataReadOnly
        {
            get { return _isGeneralDataReadOnly; }
            set
            {
                _isGeneralDataReadOnly = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGeneralDataReadOnly)));
            }
        }

        public string PackageName
        {
            get { return _package.Name; }
            set
            {
                _package.Name = value;
                if (_plcConfig != null)
                    _plcConfig.Name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageName)));
            }
        }

        public string DisplayName
        {
            get { return _package.DisplayName; }
            set
            {
                _package.DisplayName = value;
                if (_plcConfig != null)
                    _plcConfig.DisplayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public string Entitlement
        {
            get { return _package.Entitlement; }
            set
            {
                _package.Entitlement = value;
                if (_plcConfig != null)
                    _plcConfig.Entitlement = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Entitlement)));
            }
        }

        public string Description
        {
            get { return _package.Description; }
            set
            {
                _package.Description = value;
                if (_plcConfig != null)
                    _plcConfig.Description = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            }
        }

        public string ProjectUrl
        {
            get { return _package.ProjectUrl; }
            set
            {
                _package.ProjectUrl = value;
                if (_plcConfig != null)
                    _plcConfig.ProjectUrl = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProjectUrl)));
            }
        }

        public string IconFile
        {
            get { return _iconFile; }
            set
            {
                _iconFile = value;
                 if(_plcConfig != null && !string.IsNullOrEmpty(_iconFile))
                    _plcConfig.IconFile = Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, _iconFile);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconFile)));
            }
        }

        public BitmapImage IconImage
        {
            get { return _iconImage; }
            set
            {
                _iconImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconImage)));
            }
        }        

        public string Version
        {
            get { return _packageVersion.Version; }
            set
            {
                IsVersionWrongFormat = !Regex.Match(value, @"^\d+\.\d+\.\d+\.\d+$").Success;
                _packageVersion.Version = value;

                if (_plcConfig != null)
                    _plcConfig.Version = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
            }
        }


        public string DistributorName
        {
            get { return _packageVersion.DistributorName; }
            set
            {
                _packageVersion.DistributorName = value;
                if (_plcConfig != null)
                    _plcConfig.DistributorName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DistributorName)));
            }
        }

        public string Notes
        {
            get { return _packageVersion.Notes; }
            set
            {
                _packageVersion.Notes = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Notes)));
            }
        }

        public string License
        {
            get { return _packageVersion.License; }
            set
            {
                _packageVersion.License = value;
                if (_plcConfig != null)
                    _plcConfig.License = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(License)));
            }
        }

        public string Authors
        {
            get { return _packageVersion.Authors; }
            set
            {
                _packageVersion.Authors = value;
                if (_plcConfig != null)
                    _plcConfig.Authors = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Authors)));
            }
        }

        public string LatestVersion
        {
            get { return _packageVersionLatest.Version; }
            set
            {
                _packageVersionLatest.Version = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LatestVersion)));
            }
        }

        public bool IsVersionWrongFormat
        {
            get { return _isVersionWrongFormat; }
            set
            {
                _isVersionWrongFormat = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVersionWrongFormat)));
            }
        }
        
        public string LicenseFile
        {
            get { return _plcConfig?.LicenseFile; }
            set
            {
                try
                {
                    if (_plcConfig != null)
                    {
                        _plcConfig.LicenseFile = Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, value);
                        _packageVersion.LicenseBinary = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_plcConfig.RootPath, _plcConfig.LicenseFile)));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseFile)));
                    }
                }
                catch(Exception ex)
                {
                    _logger.Trace(ex);
                }
            }
        }

        public IEnumerable<Models.PackageVersionGetResponse> Dependencies
        {
            get { return _dependencies; }
            set
            {
                _dependencies = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Dependencies)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasDependencies)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DependencyCount)));
            }
        }

        public bool HasDependencies
        {
            get { return _dependencies?.Any() == true; }
        }

        public int DependencyCount
        {
            get { return _dependencies != null ? _dependencies.Count() : 0; }
        }

        private void ChangeLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = false;
                openFileDialog.Filter = "Text files (*.txt;*.md)|*.txt;*.md";
                openFileDialog.InitialDirectory = _plcConfig?.RootPath ?? Environment.CurrentDirectory;
                if (openFileDialog.ShowDialog() == true)
                {
                    LicenseFile = openFileDialog.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "License", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.Trace(ex.Message);
            }
        }


        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = false;
                openFileDialog.Filter = "Image files (*.png;*.jpeg,*.jpg)|*.png;*.jpeg;*.jpg";
                openFileDialog.InitialDirectory = _plcConfig?.RootPath ??  Environment.CurrentDirectory;
                if (openFileDialog.ShowDialog() == true)
                {
                    IconFile = openFileDialog.FileName;
                    IconImage = TwinpackUtils.IconImage(openFileDialog.FileName);            
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex.Message);
            }
        }

        private async Task<bool> PatchPackageAsync()
        {
            if (_package.PackageId == null)
                return false;

            var package = new Models.PackagePatchRequest()
            {
                PackageId = (int)_package.PackageId,
                DisplayName = DisplayName,
                Description = Description,
                ProjectUrl = ProjectUrl,
                Authors = Authors,
                License = License,
                LicenseBinary = _plcConfig != null && !string.IsNullOrEmpty(LicenseFile) && File.Exists(Path.Combine(_plcConfig.RootPath, LicenseFile)) ? Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_plcConfig.RootPath, LicenseFile))) : null,
                IconFilename = _plcConfig != null && !string.IsNullOrEmpty(IconFile) && File.Exists(Path.Combine(_plcConfig.RootPath, IconFile)) ? IconFile : null,
                IconBinary = _plcConfig != null && !string.IsNullOrEmpty(IconFile) && File.Exists(Path.Combine(_plcConfig.RootPath, IconFile)) ? Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_plcConfig.RootPath, IconFile))) : null
            };

            try
            {
                await LoginAsync();
                var packageResult = await _twinpackServer.PutPackageAsync(package);

                DisplayName = packageResult.DisplayName;
                Description = packageResult.Description;
                ProjectUrl = packageResult.ProjectUrl;
                Authors = packageResult.Authors;
                License = packageResult.License;
                IconImage = TwinpackUtils.IconImage(packageResult.IconUrl);
            }
            catch (Exceptions.GetException ex)
            {
                MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exceptions.LoginException ex)
            {
                MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return true;
        }

        private async Task<bool> PatchPackageVersionAsync()
        {
            if (_packageVersion.PackageVersionId == null)
                return false;

            var packageVersion = new Models.PackageVersionPatchRequest()
            {
                PackageVersionId = (int)_packageVersion.PackageVersionId,
                Notes = Notes
            };

            try
            {
                await LoginAsync();
                var packageVersionResult = await _twinpackServer.PutPackageVersionAsync(packageVersion);

                Authors = packageVersionResult.Authors;
                License = packageVersionResult.License;
                Notes = packageVersionResult.Notes;
            }
            catch (Exceptions.GetException ex)
            {
                MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exceptions.LoginException ex)
            {
                MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return true;
        }

        private async void Publish_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IsEnabled = false;
                IsLoading = true;
                LoadingText = "Checking all objects ...";

                var path = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries\{(_packageVersion.Target ?? "TC3.1")}\{_plcConfig.Name}_{_plcConfig.Version}.library";
                var systemManager = (_plc.Object as dynamic).SystemManager as ITcSysManager2;
                var iec = (_plc.Object as dynamic) as ITcPlcIECProject2;

                await Task.Run(() =>
                {
                    TwinpackUtils.SyncPlcProj(iec, _plcConfig);
                    _logger.Info($"Checking all objects of PLC {_plcConfig.Name}");
                    if (!iec.CheckAllObjects())
                    {
                        if (TwinpackUtils.BuildErrorCount(_context.Dte) > 0)
                            _logger.Error($"{_plcConfig.Name} does not compile!");
                    }

                    _logger.Info($"Saving and installing to {path}");
                    LoadingText = "Saving as library ...";
                    iec.SaveAsLibrary(path, false);
                });

                try
                {
                    await LoginAsync();

                    LoadingText = "Uploading to Twinpack ...";
                    _packageVersion = await _twinpackServer.PostPackageVersionAsync(_plcConfig, "Release", "main", "TC3.1", Notes,
                        false, cachePath: $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries");
                    _package = _packageVersion;
                    _packageVersionLatest.Version = _packageVersion.Version;

                    IsNewPackage = false;
                    IsNewPackageVersion = false;
                    LatestVersion = _packageVersion.Version;
                }
                catch (Exceptions.LoginException ex)
                {
                    MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exceptions.PostException ex)
                {
                    MessageBox.Show(ex.Message, "Push failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);
                }

            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Publish failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
                IsLoading = false;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsEnabled = false;
                if (await PatchPackageAsync())
                    await PatchPackageVersionAsync();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Editing failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        public async void VersionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            Version = text;
            await Task.Delay(100);

            if(Version != text || _packageVersion.PackageVersionId == null || _package.PackageId == null)
            {
                IsNewPackageVersion = false;
                return;
            }

            try
            {
                var version = new Version(text);
                var latestVersion = new Version(LatestVersion);

                IsVersionWrongFormat = !Regex.Match(text, @"^\d+\.\d+\.\d+\.\d+$").Success;
                IsNewPackageVersion = version > latestVersion;
            }
            catch (Exception)
            {
                IsVersionWrongFormat = true;
                IsNewPackageVersion = false;
            }
        }
    }
}
