using Meziantou.Framework.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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


        public async Task LoginAsync(bool onlyTry = false)
        {
            if (_twinpackServer.LoggedIn)
                return;

            // first do a silent login
            try
            {
                if (!_twinpackServer.LoggedIn)
                    await _twinpackServer.LoginAsync();
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
                    var credentials = CredentialManager.PromptForCredentials(
                        messageText: $"Login to your Twinpack Server account. Logging in will give you access to additional features. " +
                        $"It enables you to intall packages that are maintained by you, but not yet released. It also allows you to upload a new package into your Twinpack repository.",
                        captionText: "Twinpack Server login", saveCredential: CredentialSaveOption.Hidden);

                    if (credentials != null)
                        await _twinpackServer.LoginAsync(credentials.UserName, credentials.Password);

                    if (!_twinpackServer.LoggedIn)
                        throw new Exceptions.LoginException("Login was not successful!");
                }
                catch (Exceptions.LoginException ex)
                {
                    message = ex.Message;
                    _logger.Trace(ex);
                    _logger.Error(ex.Message);
                }
                catch (Exception ex)
                {
                    message = "You have to login to the Twinpack server to publish packages.";
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
