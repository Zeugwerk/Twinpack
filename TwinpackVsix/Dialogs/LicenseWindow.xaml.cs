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
        private Boolean _isInstalling;

        public Models.PackageVersionGetResponse PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));

                LicenseText = Encoding.ASCII.GetString(Convert.FromBase64String(_packageVersion?.LicenseBinary));
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

        public string LicenseText
        {
            get { return _licenseText; }
            set
            {
                _licenseText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LicenseText)));
            }
        }

        public LicenseWindow(ITcPlcLibraryManager libraryManager, Models.PackageVersionGetResponse packageVersion)
        {
            _libraryManager = libraryManager;
            DataContext = this;
            PackageVersion = packageVersion;
            IsInstalling = _libraryManager != null;

            InitializeComponent();
        }

        public bool? ShowLicense()
        {
            if (string.IsNullOrEmpty(_packageVersion?.LicenseBinary))
                return true;

            if (_libraryManager != null && TwinpackUtils.IsPackageInstalled(_libraryManager, _packageVersion))
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
    }
}
