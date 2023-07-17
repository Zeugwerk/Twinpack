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

namespace Twinpack.Dialogs
{
    public partial class PublishWindow : Window, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private PackageContext _context;
        private EnvDTE.Project _plc;
        private Models.ConfigPlcProject _plcConfig;

        private bool _isVersionDataReadOnly;
        private bool _isGeneralDataReadOnly;
        private bool _isEditPackageVisible;
        private bool _isVersionEditable;
        private bool _isNewPackage;
        private bool _isNewPackageVersion;
        private string _version;
        private bool _isApplyEnabled;
        private Authentication _auth = new Authentication();

        private Models.PackageGetResponse _package = new Models.PackageGetResponse();
        private Models.PackageVersionGetResponse _packageVersion = new Models.PackageVersionGetResponse();

        public event PropertyChangedEventHandler PropertyChanged;

        public PublishWindow(PackageContext context, EnvDTE.Project plc = null, int? packageId = null, int? packageVersionId = null, string username = "", string password = "")
        {
            _context = context;
            _plc = plc;
            DataContext = this;

            IsNewPackage = packageId == null;
            IsVersionDataReadOnly = false;
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
            await _auth.InitializeAsync();
            if (!_auth.LoggedIn)
            {
                await _auth.LoginAsync();
                if (!_auth.LoggedIn)
                    MessageBox.Show("Login was not successful! Go to https://twinpack.dev/wp-login.php to register", "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IsEnabled = false;

            try
            {
                await LoginAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }

            IsVersionDataReadOnly = false;
            IsGeneralDataReadOnly = false;

            _plcConfig = null;
            if(!string.IsNullOrEmpty(_context?.Solution?.FullName))
            {
                var config = Models.ConfigFactory.Load(Path.GetDirectoryName(_context.Solution.FullName));
                config = null;
                if (config == null)
                    config = Models.ConfigFactory.CreateFromSolution(_context.Solution);

                string projectName = null;
                foreach (EnvDTE.Project prj in _context.Solution.Projects)
                {
                    try
                    {
                        ITcSmTreeItem plcs = (prj.Object as ITcSysManager).LookupTreeItem("TIPC");
                        foreach (ITcSmTreeItem9 plc in plcs)
                            if (Object.ReferenceEquals((_plc.Object as dynamic).Parent, plc))
                            {
                                projectName = plc.Name;
                                break;
                            }
                    }
                    catch (Exception) { }
                }

                _plcConfig = config.Projects.Where(x => x.Name == projectName).SelectMany(x => x.Plcs).Where(x => x.Name == _plc.Name).FirstOrDefault();
                if (_package.PackageId == null && _plcConfig != null)
                    _package = await TwinpackService.GetPackageAsync(_auth.Username, _auth.Password, _auth.Username, _plcConfig.Name);
            }

            if (_package.PackageId != null)
            {
                try
                {
                    var package = await TwinpackService.GetPackageAsync(_auth.Username, _auth.Password, (int)_package.PackageId);

                    PackageName = package.Name;
                    DisplayName = package.DisplayName;
                    Description = package.Description;
                    Entitlement = package.Entitlement;
                    ProjectUrl = package.ProjectUrl;
                    IconUrl = package.IconUrl;
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
                IconUrl = _plcConfig.IconUrl;
            }

            if (_packageVersion.PackageVersionId == null && _package.PackageId != null)
            {
                // try to get the specific version
                IsNewPackageVersion = false;
                _packageVersion = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, _auth.Username, _package.Name, _packageVersion.Version, "Release", "main", "TC3.1", false, null);
                // fallback to the latest available version
                if(_packageVersion.PackageVersionId == null)
                {
                    IsNewPackageVersion = true;
                    _packageVersion.PackageVersionId = (await TwinpackService.GetPackageVersionsAsync(_auth.Username, _auth.Password, (int)_package.PackageId, 1, 1)).FirstOrDefault()?.PackageVersionId;
                }
            }
            else if(_plcConfig != null)
            {
                //Configuration = "Release";
                //Target = "TC3.1";
                Version = _plcConfig.Version;
                Authors = _plcConfig.Authors;
                Entitlement = _plcConfig.Entitlement;
                License = _plcConfig.License;
                //LicenseFile = mappedPlc.LicenseFile;
            }

            if (_packageVersion.PackageVersionId != null)
            { 
                try
                {
                    var packageVersion = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, (int)_packageVersion.PackageVersionId, includeBinary: false);

                    Authors = packageVersion.Authors;
                    License = packageVersion.License;
                    Notes = packageVersion.Notes;
                    Version = packageVersion.Version;
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

            IsNewPackage = _package.PackageId == null;
            IsEditPackageVisible = !IsNewPackage;
            IsApplyApplicable = false;
            IsVersionDataReadOnly = false;
            IsGeneralDataReadOnly = IsNewPackage;

            IsEnabled = true;
        }

        public async Task LoadPackageAsync(int? packageId)
        {

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

        public bool IsEditPackageVisible
        {
            get { return _isEditPackageVisible; }
            set
            {
                _isEditPackageVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditPackageVisible)));
            }
        }

        public bool IsVersionDataReadOnly
        {
            get { return _isVersionDataReadOnly; }
            set
            {
                _isVersionDataReadOnly = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVersionDataReadOnly)));
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

        public bool IsApplyApplicable
        {
            get { return _isApplyEnabled; }
            set
            {
                _isApplyEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsApplyApplicable)));
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

        public string IconUrl
        {
            get { return _package.IconUrl; }
            set
            {
                _package.IconUrl = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconUrl)));

                if(!string.IsNullOrEmpty(_package.IconUrl))
                    UpdateIconImage(_package.IconUrl);
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

        public string Authors
        {
            get { return _packageVersion.Authors; }
            set
            {
                _packageVersion.Authors = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Authors)));
            }
        }

        private void EditPackage_Click(object sender, RoutedEventArgs e)
        {
            IsGeneralDataReadOnly = false;
            IsEditPackageVisible = false;
            IsApplyApplicable = true;

        }
        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputDialog();
            inputDialog.InputValue = _package.IconUrl;
            inputDialog.ShowDialog();

            if (inputDialog.DialogResult == true)
            {
                IconUrl = inputDialog.InputValue;
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
                IconUrl = IconUrl
            };

            try
            {
                await LoginAsync();
                var packageResult = await TwinpackService.PutPackageAsync(_auth.Username, _auth.Password, package);

                DisplayName = packageResult.DisplayName;
                Description = packageResult.Description;
                ProjectUrl = packageResult.ProjectUrl;
                IconUrl = packageResult.IconUrl;
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
                _logger.Error(ex.Message);
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
                Authors = Authors,
                License = License,
                Notes = Notes
            };

            try
            {
                await LoginAsync();
                var packageVersionResult = await TwinpackService.PutPackageVersionAsync(_auth.Username, _auth.Password, packageVersion);

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
                _logger.Error(ex.Message);
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
                TwinpackService.UpdatePlcVersion(iec, new Version(_plcConfig.Version));
                _logger.Info($"Checking all objects of PLC {_plcConfig.Name}");
                if (!iec.CheckAllObjects())
                {
                    if (TwinpackService.BuildErrorCount(_context.Dte) > 0)
                        _logger.Error($"{_plcConfig.Name} does not compile!");
                }

                var path = $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries\{(_packageVersion.Target ?? "TC3.1")}\{_plcConfig.Name}_{_plcConfig.Version}.library";
                _logger.Info($"Saving and installing to {path}");

                iec.SaveAsLibrary(path, false);

                try
                {
                    await LoginAsync();
                    _packageVersion = await TwinpackService.PostPackageVersionAsync(_auth.Username, _auth.Password, _plcConfig, "Release", "main", "TC3.1", Notes, false, cachePath: $@"{Path.GetDirectoryName(_context.Solution.FullName)}\.Zeugwerk\libraries");
                    _package = await TwinpackService.GetPackageAsync(_auth.Username, _auth.Password, (int)_packageVersion.PackageId);

                    IsNewPackage = false;
                    IsNewPackageVersion = false;
                    IsEditPackageVisible = true;
                    IsApplyApplicable = false;
                    IsVersionDataReadOnly = false;
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

        private void CancelPackage_Click(object sender, RoutedEventArgs e)
        {
            IsGeneralDataReadOnly = true;
            IsApplyApplicable = false;
            IsEditPackageVisible = true;
        }

        private async void ApplyPackage_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            await PatchPackageAsync();
            IsGeneralDataReadOnly = true;
            IsApplyApplicable = false;
            IsEditPackageVisible = true;
            IsEnabled = true;
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
                    _packageVersion = await TwinpackService.GetPackageVersionAsync(_auth.Username, _auth.Password, _auth.Username, _packageVersion.Name, _version, "Release", "main", "TC3.1", includeBinary: false, cachePath: null);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex.Message);
            }

            IsNewPackageVersion = _packageVersion.PackageVersionId == null;
        }

        private void UpdateIconImage(string iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl))
                return;

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconUrl);
                bitmap.EndInit();

                imgIcon.Source = bitmap;
            }
            catch (WebException ex)
            {
                Debug.WriteLine("Failed to download the icon image: " + ex.Message);
            }
        }
    }
}
