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
using NLog;
using NuGet.Protocol.Plugins;

namespace Twinpack.Dialogs
{
    public partial class PackagingServerDialog : Window, INotifyPropertyChanged
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private CancelableTask _cancelableTask = new CancelableTask(_logger);

        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
        public event PropertyChangedEventHandler PropertyChanged;
        string _urlMem;

        public PackagingServerDialog()
        {
            DataContext = this;

            Protocol.PackagingServerRegistry.Servers.ForEach(x =>
                PackagingServers.Add(new Models.PackagingServer() { Connected = x.Connected, LoggedIn = x.LoggedIn, Name = x.Name, ServerType = x.ServerType, Url = x.UrlBase, Enabled = x.Enabled }));

            InitializeComponent();

            PackagingServersView.SelectionChanged += (o, s) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoveButtonEnabled)));
            };

            Loaded += Dialog_Loaded;
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void Dialog_Loaded(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // hide window close button
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            PackagingServers.Add(new Models.PackagingServer() { Name = $"Source Repository #{PackagingServers.Count()}", Url = "https://", ServerType = Protocol.PackagingServerRegistry.ServerTypes.First() });
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoveButtonEnabled)));

        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (PackagingServersView.SelectedItem == null)
                return;

            PackagingServers.Remove(PackagingServersView.SelectedItem as Models.PackagingServer);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRemoveButtonEnabled)));
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void ServerType_SelectionChanged(object sender, RoutedEventArgs e)
#pragma warning restore VSTHRD100 // "async void"-Methoden vermeiden
        {
            var comboBox = sender as ComboBox;
            var item = FindAncestor<ListViewItem>(comboBox);

            var s = item.DataContext as Models.PackagingServer;
            var server = Protocol.PackagingServerRegistry.CreateServer(comboBox.SelectedItem as string, s.Name, s.Url);
            var auth = new Protocol.Authentication(server);
            int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);

            await _cancelableTask.RunAsync(async token =>
            {
                PackagingServers.ElementAt(index).Connecting = true;
                await auth.LoginAsync(true, token);
            },
            () =>
            {
                PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
                PackagingServers.ElementAt(index).Connected = server.Connected;
                PackagingServers.ElementAt(index).Connecting = false;

                return Task.CompletedTask;
            });
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        {
            var button = sender as Button;
            var item = FindAncestor<ListViewItem>(button);

            var s = item.DataContext as Models.PackagingServer;
            int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);

            var server = Protocol.PackagingServerRegistry.CreateServer(s.ServerType, s.Name, s.Url);
            var auth = new Protocol.Authentication(server);

            await _cancelableTask.RunAsync(async token =>
            {
                PackagingServers.ElementAt(index).Connecting = true;
                await auth.LoginAsync(false, token);
            },
            () =>
            {
                PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
                PackagingServers.ElementAt(index).Connected = server.Connected;
                PackagingServers.ElementAt(index).Connecting = false;

                return Task.CompletedTask;
            });
        }

#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        private async void CancelLoginButton_Click(object sender, RoutedEventArgs e)
#pragma warning disable VSTHRD100 // "async void"-Methoden vermeiden
        {
            _cancelableTask.Cancel();
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

                await _cancelableTask.RunAsync(async token =>
                {
                    await auth.LogoutAsync();
                    await auth.LoginAsync(true, token); // check if connection is still possbile, so we know if connection is possible without credentials
                });

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
                    int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);

                    await _cancelableTask.RunAsync(async token =>
                    {
                        PackagingServers.ElementAt(index).Connecting = true;
                        await auth.LoginAsync(true, token);
                    },
                    () =>
                    {
                        PackagingServers.ElementAt(index).Connecting = false;
                        PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
                        PackagingServers.ElementAt(index).Connected = server.Connected;

                        return Task.CompletedTask;
                    });

                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);
            }
        }

        private async void Enabled_CheckChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                var checkBox = sender as CheckBox;
                var item = FindAncestor<ListViewItem>(checkBox);

  
                var s = item.DataContext as Models.PackagingServer;
                var server = Protocol.PackagingServerRegistry.CreateServer(s.ServerType, s.Name, s.Url);
                var auth = new Protocol.Authentication(server);
                int index = PackagingServersView.ItemContainerGenerator.IndexFromContainer(item);

                await _cancelableTask.RunAsync(async token =>
                {
                    PackagingServers.ElementAt(index).Connecting = true;

                    if (checkBox.IsChecked == true)
                    {
                        await auth.LoginAsync(true, token);
                    }
                    else
                    {
                        PackagingServers.ElementAt(index).Connecting = false;
                        PackagingServers.ElementAt(index).LoggedIn = false;
                        PackagingServers.ElementAt(index).Connected = false;
                    }
                },
                () =>
                {
                    PackagingServers.ElementAt(index).Connecting = false;
                    PackagingServers.ElementAt(index).LoggedIn = server.LoggedIn;
                    PackagingServers.ElementAt(index).Connected = server.Connected;

                    return Task.CompletedTask;
                });

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
                IsEnabled = false;

                var allConnected = PackagingServers.Any(x => !x.Connected && x.Enabled) == false;

                if(allConnected || MessageBoxResult.Yes == MessageBox.Show("The connection to one ore more packaging servers could not be " +
                    "established! Either the URL is incorrect or the server requires authentication, " +
                    "continue anyway?", "Connection error", MessageBoxButton.YesNo, MessageBoxImage.Question))
                {

                    await _cancelableTask.RunAsync(async token =>
                    {
                        Protocol.PackagingServerRegistry.Servers.Clear();
                        foreach (var x in PackagingServers)
                        {
                            try
                            {
                                await Protocol.PackagingServerRegistry.AddServerAsync(x.ServerType, x.Name, x.Url, ignoreLoginException: true, enabled: x.Enabled, login: x.Enabled, cancellationToken: token);
                            }
                            catch (Exception) { }
                        }
                    });

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
            finally
            {
                IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancelableTask.Cancel();
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
