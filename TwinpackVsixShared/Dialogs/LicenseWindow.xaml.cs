using System;
using System.ComponentModel;
using System.Text;
using System.Windows;

namespace Twinpack.Dialogs
{
    /// <summary>
    /// Interaction logic for LicenseDialog.xaml
    /// </summary>
    public partial class LicenseWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private Protocol.Api.PackageVersionGetResponse _packageVersion;
        private string _licenseText;
        private string _licenseTmcText;
        private bool _isInstalling;
        private bool _showLicenseText;
        private bool _showLicenseTmcText;


        public Protocol.Api.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            private set
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
            private set
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
            private set
            {
                _showLicenseTmcText = value;

                if (_showLicenseTmcText)
                    ShowLicenseText = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowLicenseTmcText)));
            }
        }
        public bool HasLicenseText
        {
            get { return !string.IsNullOrEmpty(_licenseText); }
        }

        public bool HasLicenseTmcText
        {
            get { return !string.IsNullOrEmpty(_licenseTmcText); }
        }

        public string LicenseText
        {
            get { return _licenseText; }
            private set
            {
                _licenseText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLicenseText)));
            }
        }

        public string LicenseTmcText
        {
            get { return _licenseTmcText; }
            private set
            {
                _licenseTmcText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseTmcText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasLicenseTmcText)));
            }
        }

        public LicenseWindow(Protocol.Api.PackageVersionGetResponse packageVersion)
        {
            PackageVersion = packageVersion;

            if (!string.IsNullOrEmpty(PackageVersion?.LicenseBinary))
                LicenseText = Encoding.ASCII.GetString(Convert.FromBase64String(_packageVersion?.LicenseBinary));

            if (!string.IsNullOrEmpty(PackageVersion?.LicenseTmcBinary))
                LicenseTmcText = Encoding.ASCII.GetString(Convert.FromBase64String(_packageVersion?.LicenseTmcBinary));

            if (HasLicenseTmcText)
                ShowLicenseTmcText = true;

            if (HasLicenseText)
                ShowLicenseText = true;

            DataContext = this;
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
            this.DialogResult = true;
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
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
