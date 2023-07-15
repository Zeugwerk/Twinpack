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
using NLog;

namespace Twinpack.Dialogs
{
    public partial class PublishWindow : Window, INotifyPropertyChanged
    {
        private static readonly NLog.Logger _logger = LogManager.GetCurrentClassLogger();

        private bool _isVersionDataReadOnly;
        private bool _isGeneralDataReadOnly;
        private bool _isEditing;
        private bool _isApplyEnabled;

        private string _username;
        private string _password;

        private Twinpack.Models.PackageGetResponse _package = new Twinpack.Models.PackageGetResponse();
        private Twinpack.Models.PackageVersionGetResponse _packageVersion = new Twinpack.Models.PackageVersionGetResponse();

        public event PropertyChangedEventHandler PropertyChanged;

        public PublishWindow(int? packageId = null, int? packageVersionId = null, string username = "", string password = "")
        {
            InitializeComponent();
            DataContext = this;
            IsEditing = packageId != null;
            IsVersionDataReadOnly = false;
            IsGeneralDataReadOnly = false;

            _package.PackageId = (int)packageId;
            _packageVersion.PackageVersionId = (int)packageVersionId;
            _username = username;
            _password = password;

            Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            IsVersionDataReadOnly = false;
            IsGeneralDataReadOnly = false;

            if (_package.PackageId != null)
            {
                try
                {
                    var package = await Twinpack.TwinpackService.GetPackageAsync(_username, _password, (int)_package.PackageId);

                    Name = package.Name;
                    DisplayName = package.DisplayName;
                    Description = package.Description;
                    Entitlement = package.Entitlement;
                    ProjectUrl = package.ProjectUrl;
                    IconUrl = package.IconUrl;
                }
                catch (Twinpack.Exceptions.GetException ex)
                {
                    MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    IsEnabled = false;
                    _logger.Error(ex.Message);
                }
            }

            if(_packageVersion.PackageVersionId != null)
            { 
                try
                {
                    var packageVersion = await TwinpackService.GetPackageVersionAsync(_username, _password, (int)_packageVersion.PackageVersionId);

                    Authors = packageVersion.Authors;
                    License = packageVersion.License;
                    Notes = packageVersion.Notes;
                }
                catch (Exceptions.GetException ex)
                {
                    MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    IsEnabled = false;
                    _logger.Error(ex.Message);
                }
            }

            IsVersionDataReadOnly = false;
            IsGeneralDataReadOnly = false;
            IsEnabled = true;
        }

        public async Task LoadPackageAsync(int? packageId)
        {

        }

        public bool IsEditing
        {
            get { return _isEditing; }
            set
            {
                _isEditing = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditing)));
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

        public string Name
        {
            get { return _package.Name; }
            set
            {
                _package.Name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
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
            var package = new Twinpack.Models.PackagePatchRequest()
            {
                PackageId = (int)_package.PackageId,
                DisplayName = DisplayName,
                Description = Description,
                ProjectUrl = ProjectUrl,
                IconUrl = IconUrl
            };

            try
            {
                var packageResult = await Twinpack.TwinpackService.PutPackageAsync(_username, _password, package);

                DisplayName = packageResult.DisplayName;
                Description = packageResult.Description;
                ProjectUrl = packageResult.ProjectUrl;
                IconUrl = packageResult.IconUrl;
            }
            catch (Twinpack.Exceptions.GetException ex)
            {
                MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private async Task<bool> PatchPackageVersionAsync()
        {
            var packageVersion = new Twinpack.Models.PackageVersionPatchRequest()
            {
                PackageVersionId = (int)_packageVersion.PackageVersionId,
                Authors = Authors,
                License = License,
                Notes = Notes
            };

            try
            {
                var packageVersionResult = await Twinpack.TwinpackService.PutPackageVersionAsync(_username, _password, packageVersion);

                Authors = packageVersionResult.Authors;
                License = packageVersionResult.License;
                Notes = packageVersionResult.Notes;
            }
            catch (Twinpack.Exceptions.GetException ex)
            {
                MessageBox.Show(ex.Message, "Retrieving Package failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void Publish_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CancelPackage_Click(object sender, RoutedEventArgs e)
        {
            IsGeneralDataReadOnly = true;
            IsApplyApplicable = false;
        }

        private async void ApplyPackage_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            await PatchPackageAsync();
            IsGeneralDataReadOnly = true;
            IsApplyApplicable = false;
            IsEnabled = true;
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            if(await PatchPackageAsync())
                await PatchPackageVersionAsync();
            IsEnabled = true;
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
