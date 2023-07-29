using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TCatSysManagerLib;

namespace Twinpack.Dialogs
{
    /// <summary>
    /// Interaction logic for LicenseDialog.xaml
    /// </summary>
    public partial class LicenseWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Models.PackageVersionGetResponse _packageVersion;
        private ITcPlcLibraryManager _libraryManager;
        private string _licenseText;
        private string _licenseTmcText;
        private bool _isInstalling;
        private bool _showLicenseText;
        private bool _showLicenseTmcText;


        public Models.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
            }
        }

        public bool IsInstalling
        {
            get { return _isInstalling; }
            set
            {
                _isInstalling = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalling)));
            }
        }
        public bool ShowLicenseText
        {
            get { return _showLicenseText; }
            set
            {
                _showLicenseText = value;
                if (_showLicenseText)
                    ShowLicenseTmcText = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLicenseText)));
            }
        }

        public bool ShowLicenseTmcText
        {
            get { return _showLicenseTmcText; }
            set
            {
                _showLicenseTmcText = value;

                if (_showLicenseTmcText)
                    ShowLicenseText = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLicenseTmcText)));
            }
        }
        public bool HasLicenseText
        {
            get { return string.IsNullOrEmpty(_licenseText); }
        }

        public bool HasLicenseTmcText
        {
            get { return string.IsNullOrEmpty(_licenseText); }
        }

        public string LicenseText
        {
            get { return _licenseText; }
            set
            {
                _licenseText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLicenseText)));
            }
        }

        public string LicenseTmcText
        {
            get { return _licenseTmcText; }
            set
            {
                _licenseTmcText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseTmcText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLicenseTmcText)));
            }
        }

        public LicenseWindow(ITcPlcLibraryManager libraryManager, Models.PackageVersionGetResponse packageVersion)
        {
            _libraryManager = libraryManager;
            DataContext = this;
            PackageVersion = packageVersion;

            if (!string.IsNullOrEmpty(PackageVersion?.LicenseBinary))
                LicenseText = Encoding.ASCII.GetString(Convert.FromBase64String(_packageVersion?.LicenseBinary));

            if (!string.IsNullOrEmpty(PackageVersion?.LicenseTmcBinary))
                LicenseTmcText = Encoding.ASCII.GetString(Convert.FromBase64String(_packageVersion?.LicenseTmcBinary));

            if (HasLicenseTmcText)
                ShowLicenseTmcText = true;

            if (HasLicenseText)
                ShowLicenseText = true;

            IsInstalling = _libraryManager != null;

            InitializeComponent();
        }

        public bool? ShowLicense()
        {
            if (string.IsNullOrEmpty(_packageVersion?.LicenseBinary) && string.IsNullOrEmpty(_packageVersion?.LicenseTmcBinary))
                return true;

            return base.ShowDialog();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public void LicenseAgreementButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseText = true;
        }

        public void RuntimeLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseTmcText = true;
        }
    }
}
