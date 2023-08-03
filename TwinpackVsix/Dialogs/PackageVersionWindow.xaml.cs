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
    public partial class PackageVersionWindow : Window, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private PackageContext _context;
        private EnvDTE.Project _plc;
        private Models.ConfigPlcProject _plcConfig;

        private bool _isConfigured;
        private bool _isLoading;
        private string _loadingText;

        private Models.LoginPostResponse _userInfo;
        private bool _isPublishMode;
        private bool _isPublic;
        private bool _isGeneralDataReadOnly;
        private bool _isVersionWrongFormat;
        private bool _isNewPackage;
        private bool _isNewPackageVersion;
        private string _iconFile;
        private string _licenseFile;
        private string _licenseTmcFile;
        private BitmapImage _iconImage;

        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

        private List<string> _branches;
        private IEnumerable<Models.PackageVersionGetResponse> _dependencies;
        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Models.PackageVersionGetResponse();
        private Models.PackageVersionGetResponse _packageVersionLatest = new Models.PackageVersionGetResponse();

        public event PropertyChangedEventHandler PropertyChanged;

        public PackageVersionWindow(bool publishMode, PackageContext context, EnvDTE.Project plc = null, int? packageId = null, int? packageVersionId = null, string username = "", string password = "")
        {
            _auth = new Authentication(_twinpackServer);
            _context = context;
            _plc = plc;
            DataContext = this;

            IsPublishMode = publishMode;
            IsNewPackage = packageId == null;
            IsGeneralDataReadOnly = false;

            _package.PackageId = packageId;
            _packageVersion.PackageVersionId = packageVersionId;

            Loaded += Window_Loaded;
            InitializeComponent();
        }

        private async Task LoginAsync()
        {
            await _auth.LoginAsync();
            if (!_twinpackServer.LoggedIn)
            {
                Close();
                return;
            }

            UserInfo = _twinpackServer.UserInfo;
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
                    if(!string.IsNullOrEmpty(UserInfo?.DistributorName) && !string.IsNullOrEmpty(_plcConfig?.Name))
                        _package = await _twinpackServer.GetPackageAsync(UserInfo?.DistributorName, _plcConfig.Name);

                    if(_package.PackageId == null)
                    {      
                        if (!IsPublishMode)
                        {
                            MessageBox.Show($"The package '{_plcConfig.Name}' is not published yet. Please publish your package before modifying it!", "Package not published", MessageBoxButton.OK, MessageBoxImage.Error);
                            Close();
                            return;
                        }  
                    
                        var resolvedPackage = await _twinpackServer.ResolvePackageVersionAsync(new Models.PlcLibrary { Name = _plcConfig.Name, Version = "*" });
                        if(resolvedPackage.PackageId != null)
                        {
                            MessageBox.Show($"The package name '{resolvedPackage.Name}' is already taken by '{resolvedPackage.DistributorName}'. Each package on the Twinpack Server must have a unique name. To publish your package, please choose an alternative name that hasn't been used by any other distributor. This can be done by renaming your PLC.", "Package name not available", MessageBoxButton.OK, MessageBoxImage.Error);
                            Close();
                            return;
                        }
                    }

                    _plcConfig.DistributorName = UserInfo?.DistributorName ?? _plcConfig.DistributorName;
                }

                if (_plcConfig == null && _package.PackageId != null)
                    _package = await _twinpackServer.GetPackageAsync((int)_package.PackageId);

                Branches = _package.PackageId != null ? _package.Branches : new List<string> { "main" };

                LoadingText = "Retrieving package version ...";
                if (_packageVersion.PackageVersionId == null && _package.PackageId != null)
                {
                    // try to get the specific version
                    IsNewPackageVersion = false;
                    _packageVersion = await _twinpackServer.GetPackageVersionAsync(_package.DistributorName, _package.Name, _packageVersion.Version, null, null, null, false, null);

                    // fallback to the latest available version
                    if (_packageVersion.PackageVersionId == null)
                    {
                        IsNewPackageVersion = true;
                        _packageVersion.PackageVersionId = (await _twinpackServer.GetPackageVersionsAsync((int)_package.PackageId, 1, 1)).Item1?.FirstOrDefault()?.PackageVersionId;
                    }
                }
                else if (_plcConfig != null)
                {
                    IsNewPackageVersion = true;
                }

                if (_packageVersion.PackageVersionId != null)
                {
                    try
                    {
                        _packageVersion = await _twinpackServer.GetPackageVersionAsync((int)_packageVersion.PackageVersionId, includeBinary: false);
                        _packageVersionLatest = await _twinpackServer.GetPackageVersionAsync(_packageVersion.DistributorName, _packageVersion.Name, null, _packageVersion.Configuration, _packageVersion.Branch, _packageVersion.Target);
                        Dependencies = _packageVersion.Dependencies;
                    }
                    catch (Exceptions.GetException ex)
                    {
                        MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception ex)
                    {
                        _logger.Trace(ex);
                        _logger.Error(ex.Message);
                    }
                }

                PackageName = _package?.Name ?? _plcConfig?.Name;
                DisplayName = _package?.DisplayName ?? _plcConfig?.DisplayName;
                Description = _package?.Description ?? _plcConfig?.Description;
                ProjectUrl = _package?.ProjectUrl ?? _plcConfig?.ProjectUrl;
                DistributorName = _package?.DistributorName ?? _plcConfig?.DistributorName;
                License = _package?.License ?? _plcConfig?.License;
                Version = _packageVersion?.Version ?? _plcConfig?.Version;
                Authors = _packageVersion?.Authors ?? _plcConfig?.Authors;
                License = _packageVersion?.License ?? _plcConfig?.License;
                IconImage = TwinpackUtils.IconImage(_package?.IconUrl);
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

                try
                {
                    LicenseFile = _plcConfig != null && !string.IsNullOrEmpty(_plcConfig.LicenseFile) ? Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, _plcConfig.LicenseFile) : null;
                    LicenseTmcFile = _plcConfig != null && !string.IsNullOrEmpty(_plcConfig.LicenseTmcFile) ? Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, _plcConfig.LicenseTmcFile) : null;
                    IconFile = _plcConfig != null && !string.IsNullOrEmpty(_plcConfig.IconFile) ? Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, _plcConfig.IconFile) : null;
                }
                catch(Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Warn(ex.Message);
                }

                // increment the version number right away
                if (!IsNewPackage && IsPublishMode)
                {
                    var v = new Version(Version);
                    Version = new Version(v.Major, v.Minor, v.Build, v.Revision + 1).ToString();
                }

                // Fill and select advanced options
                BranchesView.ItemsSource = Branches;
                EntitlementView.ItemsSource = UserInfo.Entitlements;
                ConfigurationsView.ItemsSource = UserInfo.Configurations;
                TargetsView.ItemsSource = UserInfo.Targets;

                var branch = Branches.FirstOrDefault(x => x == _packageVersion?.Branch);
                var entitlement = UserInfo?.Entitlements.FirstOrDefault(x => x.Name == _package?.Entitlement) ?? UserInfo?.Entitlements.FirstOrDefault(x => x.IsPublic);
                var configuration = UserInfo?.Configurations.FirstOrDefault(x => x.Name == _packageVersion?.Configuration) ?? UserInfo?.Configurations.FirstOrDefault(x => x.IsPublic);
                var target = UserInfo?.Targets.FirstOrDefault(x => x.Name == _packageVersion?.Target) ?? UserInfo?.Targets.FirstOrDefault(x => x.IsPublic);

                BranchesView.SelectedIndex = branch != null ? Branches.IndexOf(branch) : 0;
                EntitlementView.SelectedIndex = entitlement != null ? UserInfo.Entitlements.IndexOf(entitlement) : 0;
                ConfigurationsView.SelectedIndex = configuration != null ? UserInfo.Configurations.IndexOf(configuration) : 0;
                TargetsView.SelectedIndex = target != null ? UserInfo.Targets.IndexOf(target) : 0;
                FileTypeView.SelectedIndex = _packageVersion?.Compiled == 1 ? 1 : 0;

                IsConfigured = _plcConfig != null && (_package?.PackageId == null || _plcConfig.Name == _package.Name && _plcConfig.DistributorName == _package.DistributorName && _package.Repository == _twinpackServer.Username);
                IsNewPackage = _package.PackageId == null;
                if (IsNewPackage)
                    License = (cmbLicense.Items[2] as ComboBoxItem).Content.ToString();
                IsGeneralDataReadOnly = _package?.PackageId != null && _package.Repository != _twinpackServer.Username;
                
                ValidateNewPackageVersion();
                ValidateVisibility();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
            finally
            {
                IsEnabled = true;
                IsLoading = false;
            }
        }

        public Models.LoginPostResponse UserInfo
        {
            get { return _userInfo; }
            set
            {
                _userInfo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UserInfo)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNewUser)));

            }
        }
        public bool IsNewUser
        {
            get { return string.IsNullOrEmpty(UserInfo?.DistributorName); }
        }

        public bool IsPublishMode
        {
            get { return _isPublishMode; }
            set
            {
                _isPublishMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPublishMode)));
            }
        }

        public bool IsPublic
        {
            get { return _isPublic; }
            set
            {
                _isPublic = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPublic)));
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

        public bool ShowLicenseInfo { get; set; }

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
        
        public List<string> Branches
        {
            get
            { 
                return _branches;
            }
            set
            {
                _branches = value;
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLicenseBinary)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLicenseTmcBinary)));

            }
        }

        public bool HasLicenseBinary
        {
            get { return _packageVersion.HasLicenseBinary; }
        }

        public bool HasLicenseTmcBinary
        {
            get { return _packageVersion.HasLicenseTmcBinary; }
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
            get { return _licenseFile; }
            set
            {
                try
                {
                    _licenseFile = value;
                    if (_plcConfig != null)
                        _plcConfig.LicenseFile = value == null ? null : Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, value);

                    if(_packageVersion != null)
                        _packageVersion.LicenseBinary = _licenseFile == null ? _packageVersion.LicenseBinary : Convert.ToBase64String(File.ReadAllBytes(value));

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseFile)));

                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
            }
        }

        public string LicenseTmcFile
        {
            get { return _licenseTmcFile; }
            set
            {
                try
                {
                    _licenseTmcFile = value;
                    if (_plcConfig != null)
                        _plcConfig.LicenseTmcFile = value == null ? null : Extensions.DirectoryExtension.RelativePath(_plcConfig.RootPath, value);

                    if(_packageVersion != null)
                        _packageVersion.LicenseTmcBinary = _licenseTmcFile == null ? _packageVersion.LicenseTmcBinary : Convert.ToBase64String(File.ReadAllBytes(value));

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseTmcFile)));

                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
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
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                MessageBox.Show(ex.Message, "License", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeLicenseTmc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Multiselect = false;
                openFileDialog.Filter = "tmc files (*.tmc)|*.tmc";
                openFileDialog.InitialDirectory = _plcConfig?.RootPath ?? Environment.CurrentDirectory;
                if (openFileDialog.ShowDialog() == true)
                {
                    var content = File.ReadAllText(openFileDialog.FileName);
                    if (TwinpackUtils.ParseLicenseId(content) == null)
                        throw new InvalidDataException("The tmc file is not a valid license file!");

                    LicenseTmcFile = openFileDialog.FileName;
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
                MessageBox.Show(ex.Message, "License", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _logger.Trace(ex);
                _logger.Error(ex.Message);
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
                Entitlement = (EntitlementView.SelectedItem as Models.LoginPostResponse.Entitlement).Name,
                LicenseBinary = !string.IsNullOrEmpty(LicenseFile) && File.Exists(LicenseFile) ? Convert.ToBase64String(File.ReadAllBytes(LicenseFile)) : _packageVersion?.LicenseBinary,
                LicenseTmcBinary = !string.IsNullOrEmpty(LicenseTmcFile) && File.Exists(LicenseTmcFile) ? Convert.ToBase64String(File.ReadAllBytes(LicenseTmcFile)) : _packageVersion?.LicenseTmcBinary,
                IconFilename = !string.IsNullOrEmpty(IconFile) && File.Exists(IconFile) ? Path.GetFileName(IconFile) : null,
                IconBinary = !string.IsNullOrEmpty(IconFile) && File.Exists(IconFile) ? Convert.ToBase64String(File.ReadAllBytes(IconFile)) : null
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

                var branch = BranchesView.SelectedItem as string;
                var configuration = (ConfigurationsView.SelectedItem as Models.LoginPostResponse.Configuration).Name;
                var entitlement = (EntitlementView.SelectedItem as Models.LoginPostResponse.Entitlement).Name;
                var target = (TargetsView.SelectedItem as Models.LoginPostResponse.Target).Name;
                var compiled = FileTypeView.SelectedIndex != 0;

                IsEnabled = false;
                IsLoading = true;
                LoadingText = "Checking all objects ...";

                var path = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries\{target}\{_plcConfig.Name}_{_plcConfig.Version}.library";
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

                    _logger.Info($"Saving and installing library to {path}");
                    LoadingText = "Saving as library ...";
                    Directory.CreateDirectory(new FileInfo(path).Directory.FullName);
                    iec.SaveAsLibrary(path, false);
                });

                try
                {
                    await LoginAsync();

                    LoadingText = "Uploading to Twinpack ...";

                    var cachePath = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries";
                    var suffix = compiled ? "compiled-library" : "library";                   
                    var packageVersion = new Models.PackageVersionPostRequest()
                    {
                        Name = PackageName,
                        Version = Version,
                        Target = target,
                        License = License,
                        Description = Description,
                        DistributorName = DistributorName,
                        Authors = Authors,
                        Entitlement = entitlement,
                        ProjectUrl = ProjectUrl,
                        DisplayName = DisplayName,
                        Branch = branch,
                        Configuration = configuration,
                        Compiled = compiled ? 1 : 0,
                        Notes = Notes,
                        LicenseBinary = !string.IsNullOrEmpty(LicenseFile) && File.Exists(LicenseFile) ? Convert.ToBase64String(File.ReadAllBytes(LicenseFile)) : _packageVersion?.LicenseBinary,
                        LicenseTmcBinary = !string.IsNullOrEmpty(LicenseTmcFile) && File.Exists(LicenseTmcFile) ? Convert.ToBase64String(File.ReadAllBytes(LicenseTmcFile)) : _packageVersion?.LicenseTmcBinary,
                        IconFilename = !string.IsNullOrEmpty(IconFile) && File.Exists(IconFile) ? Path.GetFileName(IconFile) : null,
                        IconBinary = !string.IsNullOrEmpty(IconFile) && File.Exists(IconFile) ? Convert.ToBase64String(File.ReadAllBytes(IconFile)) : null,
                        Binary = Convert.ToBase64String(File.ReadAllBytes($@"{cachePath}\{target}\{_plcConfig.Name}_{_plcConfig.Version}.{suffix}")),
                        Dependencies = _plcConfig.Packages?.Select(x => new Models.PackageVersionDependencyPostRequest
                        {
                            Repository = x.Repository,
                            DistributorName = x.DistributorName,
                            Name = x.Name,
                            Version = x.Version,
                            Branch = x.Branch,
                            Target = x.Target,
                            Configuration = x.Configuration
                        })
                    };

                    _packageVersion = await _twinpackServer.PostPackageVersionAsync(packageVersion);
                    _package = _packageVersion;
                    _packageVersionLatest.Version = _packageVersion.Version;

                    IsNewPackage = false;
                    IsNewPackageVersion = false;
                    IsPublishMode = false;
                    LatestVersion = _packageVersion.Version;

                    await WritePlcConfigToConfigAsync(_plcConfig);

                    _twinpackServer.InvalidateCache();

                    MessageBox.Show($"You successfully uploaded {_packageVersion.Name} to Twinpack! You may still modify general information of the package.", "Publish successful", MessageBoxButton.OK, MessageBoxImage.Information);

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
                    _logger.Trace(ex);
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

                await WritePlcConfigToConfigAsync(_plcConfig);
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

        public void ValidateNewPackageVersion()
        {
            if (string.IsNullOrEmpty(DistributorName) ||
                string.IsNullOrEmpty(PackageName))
            {
                IsNewPackageVersion = false;
                return;
            }

            try
            {
                var version = new Version(Version);
                var latestVersion = string.IsNullOrEmpty(LatestVersion) ? new Version("0.0.0.0") : new Version(Version);

                IsVersionWrongFormat = !Regex.Match(Version, @"^\d+\.\d+\.\d+\.\d+$").Success;
                IsNewPackageVersion = version > latestVersion;
            }
            catch (Exception)
            {
                IsVersionWrongFormat = true;
                IsNewPackageVersion = false;
            }
        }

        public async void PackageNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            PackageName = text;

            ValidateNewPackageVersion();
        }

        public async void DistributorNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            DistributorName = text;

            ValidateNewPackageVersion();
        }

        public async void VersionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            Version = text;

            ValidateNewPackageVersion();
        }

        public void ShowLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseDialog = new LicenseWindow(null, _packageVersion);
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
                var licenseDialog = new LicenseWindow(null, _packageVersion);
                licenseDialog.ShowLicense();
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        public async Task<Models.Config> WritePlcConfigToConfigAsync(Models.ConfigPlcProject plcConfig)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var config = Models.ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));

            if (config != null)
            {
                Models.ConfigFactory.UpdatePlcProject(config, plcConfig);
                Models.ConfigFactory.Save(config);
            }
            else
            {
                _logger.Warn($"The solution doesn't have a package configuration");
            }

            return config;
        }

        private void ValidateVisibility()
        {
            var configuration = (ConfigurationsView.SelectedItem as Models.LoginPostResponse.Configuration);
            var target = (TargetsView.SelectedItem as Models.LoginPostResponse.Target);
            var entitlement = (EntitlementView.SelectedItem as Models.LoginPostResponse.Entitlement);

            IsPublic = configuration?.IsPublic == true && target?.IsPublic == true && entitlement?.IsPublic == true;
        }

        private void ValidateVisibility_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateVisibility();
        }
    }
}
