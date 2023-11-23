using AdysTech.CredentialManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Twinpack.Dialogs
{
    public class Authentication
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private TwinpackServer _twinpackServer;
        public Authentication(TwinpackServer twinpackServer)
        {
            _twinpackServer = twinpackServer;
        }


        public async Task LoginAsync(bool onlyTry = false, CancellationToken cancellationToken = default)
        {
            if (_twinpackServer.LoggedIn)
                return;

            // first do a silent login
            try
            {
                if (!_twinpackServer.LoggedIn)
                    await _twinpackServer.LoginAsync(null, null, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                throw ex;
            }
            catch (OperationCanceledException ex)
            {
                throw ex;
            }
            catch (Exception)
            { }

            if (onlyTry)
                return;

            // then login with prompting if it didn't work
            while (!_twinpackServer.LoggedIn)
            {
                var message = "";
                try
                {
                    bool save=true;
                    var credentials = CredentialManager.PromptForCredentials(_twinpackServer.TwinpackUrlBase, ref save,
                        message: $"Login to your Twinpack Server account. Logging in will give you access to additional features. " +
                        $"It enables you to intall packages that are maintained by you, but not yet released. It also allows you to upload a new package into your Twinpack repository.",
                        caption: "Twinpack Server login");

                    if (credentials != null)
                        await _twinpackServer.LoginAsync(credentials.UserName, credentials.Password, cancellationToken);

                    if (!_twinpackServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful!");

                    if (!save)
                        CredentialManager.RemoveCredentials(_twinpackServer.TwinpackUrlBase);
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

                if (!_twinpackServer.LoggedIn)
                {
                    if (MessageBox.Show($@"{message} Do you want to register or reset your password?", "Login failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        Process.Start(_twinpackServer.RegisterUrl);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        public void Logout()
        {
            _twinpackServer.Logout();
        }
    }
}
