using AdysTech.CredentialManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
                throw ex;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            { }

            if (onlyTry)
                return;

            // then login with prompting if it didn't work
            while (!_packageServer.LoggedIn)
            {
                var message = "";
                try
                {
                    bool save=true;
                    var credentials = CredentialManager.PromptForCredentials(_packageServer.UrlBase, ref save,
                        message: $"Login to Package Server {_packageServer.UrlBase}",
                        caption: "Twinpack Server login");

                    if (credentials != null)
                        await _packageServer.LoginAsync(credentials.UserName, credentials.Password, cancellationToken);

                    if (!_packageServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful!");

                    if (!save)
                        CredentialManager.RemoveCredentials(_packageServer.UrlBase);
                }
                catch (Exceptions.LoginException ex)
                {
                    message = ex.Message;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
                catch (Exception ex)
                {
                    message = "Login to Twinpack Server failed!";
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }

                if (!_packageServer.LoggedIn && _packageServer.UrlRegister != null)
                {
                    if (MessageBox.Show($@"{message} Do you want to register?", "Login failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        Process.Start(_packageServer.UrlRegister);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        public async Task LogoutAsync()
        {
            await _packageServer.LogoutAsync();
        }
    }
}
