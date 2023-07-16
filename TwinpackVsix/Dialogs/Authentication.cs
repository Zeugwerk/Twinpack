using Meziantou.Framework.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Dialogs
{
    public class Authentication
    {
        public Authentication()
        {

        }

        public bool LoggedIn { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public async Task InitializeAsync()
        {
            try
            {
                var credentials = CredentialManager.ReadCredential("Twinpack");
                if(credentials != null)
                {
                    Username = credentials.UserName;
                    Password = credentials.Password;
                    LoggedIn = await TwinpackService.LoginAsync(credentials.UserName, credentials.Password);
                }

            }
            catch (Exceptions.LoginException)
            {
                CredentialManager.DeleteCredential("Twinpack");
                Username = null;
                Password = null;
                LoggedIn = false;
            }
        }

        public async Task LoginAsync()
        {
            if (LoggedIn)
            {
                return;
            }

            var credentials = CredentialManager.PromptForCredentials(
                messageText: $"Login to your Twinpack Server account. Logging in will give you access to additional features. " +
                $"It enables you to intall packages that are maintained by you, but not yet released. It also allows you to upload a new package into your Twinpack repository.",
                captionText: "Twinpack Server login", saveCredential: CredentialSaveOption.Hidden);

            try
            {
                LoggedIn = await TwinpackService.LoginAsync(credentials.UserName, credentials.Password);

                if (LoggedIn)
                {
                    CredentialManager.WriteCredential("Twinpack", credentials.UserName, credentials.Password, CredentialPersistence.LocalMachine);
                    Username = credentials.UserName;
                    Password = credentials.Password;
                }
            }
            catch (LoginException ex)
            {
                CredentialManager.DeleteCredential("Twinpack");
                credentials = null;
                throw ex;
            }
            finally
            {
                Username = credentials?.UserName;
                Password = credentials?.Password;
            }
        }

        public void Logout()
        {
            CredentialManager.DeleteCredential("Twinpack");
            LoggedIn = false;
            Username = "";
            Password = "";
        }
    }
}
