using AdysTech.CredentialManager;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Twinpack.Exceptions;

namespace Twinpack.Protocol
{
    public class Authentication
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private Protocol.IPackageServer _packageServer;
        public Authentication(Protocol.IPackageServer packageServer)
        {
            _packageServer = packageServer;
        }

        public async Task LoginAsync(bool onlyTry = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_packageServer.LoggedIn)
                return;

            // first do a silent login
            try
            {
                if (!_packageServer.LoggedIn)
                    await _packageServer.LoginAsync(null, null, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _logger.Warn($"Log in to '{_packageServer.Url}' timed-out");
                throw ex;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn($"Log in to '{_packageServer.Url}' cancelled");
                throw;
            }
            catch (LoginException)
            {
                _logger.Warn($"Log in to '{_packageServer.Url}' failed");
            }
            catch (Exception)
            { }

            if (onlyTry)
                return;

            cancellationToken.ThrowIfCancellationRequested();

#if !NETSTANDARD2_1_OR_GREATER
            // then login with prompting if it didn't work
            while (!_packageServer.LoggedIn)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var message = "";
                try
                {
                    bool save=true;
                    var credentials = CredentialManager.PromptForCredentials(_packageServer.UrlBase, ref save,
                        message: $"Login to Package Server {_packageServer.UrlBase}. Please login with your username and password. Leave username blank if the server uses an API Key",
                        caption: "Package Server Login");

                    cancellationToken.ThrowIfCancellationRequested();

                    if (credentials != null)
                        await _packageServer.LoginAsync(credentials.UserName, credentials.Password, cancellationToken);

                    if (!_packageServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful!");

                    if (!save)
                        CredentialManager.RemoveCredentials(_packageServer.UrlBase);

                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (LoginException ex)
                {
                    message = ex.Message;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
                catch (Exception ex)
                {
                    message = @$"Login failed, see '%LOCALAPPDATA%\Zeugwerk\logs\Twinpack' for details!";
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }


                if (!_packageServer.LoggedIn)
                {
                    if (_packageServer.UrlRegister != null)
                    {
                        if (MessageBox.Show($"{message}\n\nDo you need to register?", "Login failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            Process.Start(_packageServer.UrlRegister);
                        else
                            return;
                    }
                    else
                    {
                        if (MessageBox.Show($"{message}\n\nRetry?", "Login failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                            return;
                    }
                }
        }
#endif
        }

        public async Task LogoutAsync()
        {
            await _packageServer.LogoutAsync();
        }
    }
}
