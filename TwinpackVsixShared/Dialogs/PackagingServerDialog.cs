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

namespace Twinpack.Dialogs
{
    public partial class PackagingServerDialog : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public PackagingServerDialog()
        {
            DataContext = this;

            Packaging.PackagingServerRegistry.Servers.ForEach(x =>
                PackagingServers.Add(new Models.PackagingServer() { Connected = x.LoggedIn, Name = x.Name, ServerType = x.ServerType, Url = x.UrlBase }));

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
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            PackagingServers.Add(new Models.PackagingServer() { Name = $"Source Repository #{PackagingServers.Count()}", Url = "https://", ServerType = Packaging.PackagingServerRegistry.ServerTypes.First() });
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

            var x = item.DataContext as Models.PackagingServer;
            var server = Packaging.PackagingServerRegistry.CreateServer(x.ServerType, x.Name, x.Url);
            var auth = new Packaging.Authentication(server);
            await auth.LoginAsync(false);

            x.Connected = server.LoggedIn;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackagingServers)));
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var item = FindAncestor<ListViewItem>(button);

                var x = item.DataContext as Models.PackagingServer;
                var server = Packaging.PackagingServerRegistry.CreateServer(x.ServerType, x.Name, x.Url);
                var auth = new Packaging.Authentication(server);
                auth.Logout();

                x.Connected = server.LoggedIn;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackagingServers)));
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
                Packaging.PackagingServerRegistry.Servers.Clear();
                foreach (var x in PackagingServers)
                {
                    await Packaging.PackagingServerRegistry.AddServerAsync(x.ServerType, x.Name, x.Url);
                }

                Packaging.PackagingServerRegistry.Save();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }

            Close();
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
        public IEnumerable<string> ServerTypes { get => Packaging.PackagingServerRegistry.ServerTypes; }
    }
}
