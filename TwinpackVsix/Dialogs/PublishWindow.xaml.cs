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

namespace Twinpack.Dialogs
{
    public partial class PublishWindow : Window, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private PackageContext _context;
        private EnvDTE.Project _plc;
        private Models.ConfigPlcProject _plcConfig;

        private bool _isConfigured;
        private bool _isGeneralDataReadOnly;
        private bool _isVersionEditable;
        private bool _isNewUser;
        private bool _isNewPackage;
        private bool _isNewPackageVersion;
        private string _version;
        private string _iconFile;
        private BitmapImage _iconImage;

        private TwinpackServer _twinpackServer = new TwinpackServer();
        private Authentication _auth;

        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Models.PackageVersionGetResponse();

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
            try
            { 
                if (!_twinpackServer.LoggedIn)
                {
                    await _auth.LoginAsync();
                    if(!_twinpackServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful! Go to https://twinpack.dev/wp-login.php to register");
                }
            }
            catch(Exceptions.LoginException ex)
            {
                MessageBox.Show(ex.Message, "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }          
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            finally
            {
                IsNewUser = _twinpackServer.UserInfo.DistributorName == null;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IsEnabled = false;

            try
            {
                await _twinpackServer.LoginAsync();
                if(!_twinpackServer.LoggedIn)
                {
                    _twinpackServer.Logout();
                    await LoginAsync();
                }

                IsNewUser = _twinpackServer.UserInfo.DistributorName == null;
                IsGeneralDataReadOnly = false;
    
                if(_plc != null)
                {
                    _plcConfig = await Models.ConfigPlcProjectFactory.CreateAsync(_context.Solution, _plc, _twinpackServer);
                    if (_package.PackageId == null && _plcConfig != null)
                    {
                        _package = await _twinpackServer.GetPackageAsync(_twinpackServer.Username, _plcConfig.Name);
                        _plcConfig.DistributorName = _twinpackServer.UserInfo.DistributorName ?? _plcConfig.DistributorName;
                    }
                }
                else
                {
                    _plcConfig = null;
                }


                if (_package.PackageId != null)
                {
                    try
                    {
                        _package = await _twinpackServer.GetPackageAsync((int)_package.PackageId);
    
                        PackageName = _package.Name;
                        DisplayName = _package.DisplayName;
                        Description = _package.Description;
                        Entitlement = _package.Entitlement;
                        ProjectUrl = _package.ProjectUrl;
                        DistributorName = _package.DistributorName;
                        Authors = _package.Authors;
                        License = _package.License;
                        LicenseFile = _plcConfig?.LicenseFile;
                        IconFile = _plcConfig?.IconFile;
                        IconImage = TwinpackUtils.IconImage(_package.IconUrl);
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
                else if(_plcConfig != null)
                {
                    IsVersionEditable = true;
    
                    PackageName = _plcConfig.Name;
                    DisplayName = _plcConfig.DisplayName;
                    Description = _plcConfig.Description;
                    Entitlement = _plcConfig.Entitlement;
                    ProjectUrl = _plcConfig.ProjectUrl;
                    IconFile = _plcConfig.IconFile;
                    DistributorName = _plcConfig.DistributorName;

                    if (!string.IsNullOrEmpty(_plcConfig.IconFile))
                        IconImage = TwinpackUtils.IconImage(Path.Combine(_plcConfig.RootPath, _plcConfig.IconFile));
                }
    
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
                    //Configuration = "Release";
                    //Target = "TC3.1";
                    //Branch = "main";
                    Version = _plcConfig.Version;
                    Authors = _plcConfig.Authors;
                    Entitlement = _plcConfig.Entitlement;
                    License = _plcConfig.License;
                    LicenseFile = _plcConfig.LicenseFile;
                }
    
                if (_packageVersion.PackageVersionId != null)
                { 
                    try
                    {
                        _packageVersion = await _twinpackServer.GetPackageVersionAsync((int)_packageVersion.PackageVersionId, includeBinary: false);
                        Notes = _packageVersion.Notes;
                        Version = _packageVersion.Version;
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

                IsConfigured = _plcConfig != null && _plcConfig.Name == _package.Name && _plcConfig.DistributorName == _package.DistributorName && _package.Repository == _twinpackServer.Username;
                IsNewPackage = _package.PackageId == null;
                IsGeneralDataReadOnly = _package.Repository != _twinpackServer.Username;
                IsEnabled = true;
             }
            catch(Exception ex)
            {
                _logger.Trace(ex);
            }
        }

        public async Task LoadPackageAsync(int? packageId)
        {

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

        public bool IsVersionEditable
        {
            get { return _isVersionEditable; }
            set
            {
                _isVersionEditable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVersionEditable)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageName)));
            }
        }

        public string DisplayName
        {
            get { return _package.DisplayName; }
            set
            {
                _package.DisplayName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }

        public string Entitlement
        {
            get { return _package.Entitlement; }
            set
            {
                _package.Entitlement = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Entitlement)));
            }
        }

        public string Description
        {
            get { return _package.Description; }
            set
            {
                _package.Description = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            }
        }

        public string ProjectUrl
        {
            get { return _package.ProjectUrl; }
            set
            {
                _package.ProjectUrl = value;
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
                _packageVersion.Version = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
            }
        }

        public string DistributorName
        {
            get { return _packageVersion.DistributorName; }
            set
            {
                _packageVersion.DistributorName = value;
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(License)));
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

        public string Authors
        {
            get { return _packageVersion.Authors; }
            set
            {
                _packageVersion.Authors = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Authors)));
            }
        }

        private async void ChangeLicense_Click(object sender, RoutedEventArgs e)
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


        private async void ChangeIcon_Click(object sender, RoutedEventArgs e)
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
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                IsEnabled = false;

                var systemManager = (_plc.Object as dynamic).SystemManager as ITcSysManager2;
                var iec = (_plc.Object as dynamic) as ITcPlcIECProject2;
                TwinpackUtils.SyncPlcProj(iec, _plcConfig);
                _logger.Info($"Checking all objects of PLC {_plcConfig.Name}");
                if (!iec.CheckAllObjects())
                {
                    if (TwinpackUtils.BuildErrorCount(_context.Dte) > 0)
                        _logger.Error($"{_plcConfig.Name} does not compile!");
                }

                var path = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries\{(_packageVersion.Target ?? "TC3.1")}\{_plcConfig.Name}_{_plcConfig.Version}.library";
                _logger.Info($"Saving and installing to {path}");

                iec.SaveAsLibrary(path, false);

                try
                {
                    await LoginAsync();
                    _packageVersion = await _twinpackServer.PostPackageVersionAsync(_plcConfig, "Release", "main", "TC3.1", Notes,
                        false, cachePath: $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries");
                    _package = await _twinpackServer.GetPackageAsync((int)_packageVersion.PackageId);

                    IsNewPackage = false;
                    IsNewPackageVersion = false;
                    IsGeneralDataReadOnly = true;
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
            }
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
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

                Close();
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
            if (_packageVersion.PackageVersionId == null || _package.PackageId == null)
                return;

            var text = ((TextBox)sender).Text;
            _version = text;
            await Task.Delay(100);

            try
            {
                if (_version == text)
                    _packageVersion = await _twinpackServer.GetPackageVersionAsync(_packageVersion.Repository, _packageVersion.Name, _version, "Release", "main", "TC3.1", includeBinary: false, cachePath: null);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex.Message);
            }

            IsNewPackageVersion = _packageVersion.PackageVersionId == null;
        }


    }
}
