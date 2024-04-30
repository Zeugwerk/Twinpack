using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using Newtonsoft.Json.Linq;
using static System.Net.Mime.MediaTypeNames;
using System.Threading.Tasks;

namespace Twinpack.Dialogs
{
    public partial class PackagingServerDialog : Window, INotifyPropertyChanged
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
        public event PropertyChangedEventHandler PropertyChanged;
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        string _urlMem;

        public PackagingServerDialog()
        {
            DataContext = this;

            Protocol.PackagingServerRegistry.Servers.ForEach(x =>
                PackagingServers.Add(new Models.PackagingServer() { Connected = x.Connected, LoggedIn = x.LoggedIn, Name = x.Name, ServerType = x.ServerType, Url = x.UrlBase }));

            InitializeComponent();

            PackagingServersView.SelectionChanged += (o, s) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoveButtonEnabled)));
            };

            Loaded += Dialog_Loaded;
        }

        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // hide window close button
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            PackagingServers.Add(new Models.PackagingServer() { Name = $"Source Repository #{PackagingServers.Count()}", Url = "https://", ServerType = Protocol.PackagingServerRegistry.ServerTypes.First() });
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoveButtonEnabled)));

        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PackagingServersView.SelectedItem == null)
                return;

            PackagingServers.Remove(PackagingServersView.SelectedItem as Models.PackagingServer);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoveButtonEnabled)));

        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = FindAncestor<ListViewItem>(button);

            var s = item.DataContext as Models.PackagingServer;
            var server = Protocol.PackagingServerRegistry.CreateServer(s.ServerType, s.Name, s.Url);
            var auth = new Protocol.Authentication(server);
            await auth.LoginAsync(false);

            int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);
            PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
            PackagingServers.ElementAt(index).Connected = server.Connected;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var item = FindAncestor<ListViewItem>(button);

                var s = item.DataContext as Models.PackagingServer;
                var server = Protocol.PackagingServerRegistry.CreateServer(s.ServerType, s.Name, s.Url);
                var auth = new Protocol.Authentication(server);
                auth.Logout();
                await auth.LoginAsync(true); // check if connection is still possbile, so we know if connection is possible without credentials

                int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);
                PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
                PackagingServers.ElementAt(index).Connected = server.Connected;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }


        private async void Url_TextChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = sender as TextBox;
                var item = FindAncestor<ListViewItem>(textBox);

                _urlMem = textBox.Text;
                await Task.Delay(300);

                if (_urlMem == textBox.Text)
                {
                    var s = item.DataContext as Models.PackagingServer;
                    var server = Protocol.PackagingServerRegistry.CreateServer(s.ServerType, s.Name, textBox.Text);
                    var auth = new Protocol.Authentication(server);
                    await auth.LoginAsync(true);

                    int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);
                    PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
                    PackagingServers.ElementAt(index).Connected = server.Connected;
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }
        

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allConnected = PackagingServers.Any(x => !x.Connected) == false;

                if(allConnected || MessageBoxResult.Yes == MessageBox.Show("The connection to one ore more packaging servers could not be " +
                    "established! Either the URL is incorrect or the server requires authentication, " +
                    "continue anyway?", "Connection error", MessageBoxButton.YesNo, MessageBoxImage.Question))
                {
                    Protocol.PackagingServerRegistry.Servers.Clear();
                    foreach (var x in PackagingServers)
                    {
                        await Protocol.PackagingServerRegistry.AddServerAsync(x.ServerType, x.Name, x.Url);
                    }

                    Protocol.PackagingServerRegistry.Save();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://zeugwerk.dev/wp-login.php");
        }

        private void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://zeugwerk.dev/wp-login.php?action=lostpassword");
        }

        public bool IsRemoveButtonEnabled
        {
            get
            {
                return PackagingServers.Count > 0 && PackagingServersView.SelectedItem != null;
            }
        }
        public ObservableCollection<Models.PackagingServer> PackagingServers { get; } = new ObservableCollection<Models.PackagingServer>();
        public IEnumerable<string> ServerTypes { get => Protocol.PackagingServerRegistry.ServerTypes; }
    }
}
