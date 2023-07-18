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
        public Models.LoginPostResponse UserInfo { get; set; }

        public async Task InitializeAsync()
        {
            try
            {
                LoggedIn = false;
                var credentials = CredentialManager.ReadCredential("Twinpack");
                if(credentials != null)
                {
                    Username = credentials.UserName;
                    Password = credentials.Password;
                    UserInfo = await TwinpackService.LoginAsync(credentials.UserName, credentials.Password);
                    LoggedIn = UserInfo.User != null;
                }

            }
            catch (Exception) { }
            finally
            {
                if(!LoggedIn)
                {
                    CredentialManager.DeleteCredential("Twinpack");
                    Username = null;
                    Password = null;
                }

            }
        }

        public async Task LoginAsync()
        {
            if (LoggedIn)
                return;

            var credentials = CredentialManager.PromptForCredentials(
                messageText: $"Login to your Twinpack Server account. Logging in will give you access to additional features. " +
                $"It enables you to intall packages that are maintained by you, but not yet released. It also allows you to upload a new package into your Twinpack repository.",
                captionText: "Twinpack Server login", saveCredential: CredentialSaveOption.Hidden);

            try
            {
                UserInfo = new Models.LoginPostResponse();
                UserInfo = await TwinpackService.LoginAsync(credentials.UserName, credentials.Password);
                LoggedIn = UserInfo.User != null;

            }
            catch (Exception) { }
            finally
            {
                if (LoggedIn)
                {
                    CredentialManager.WriteCredential("Twinpack", credentials.UserName, credentials.Password, CredentialPersistence.LocalMachine);
                    Username = credentials?.UserName;
                    Password = credentials?.Password;
                }
                else
                {
                    CredentialManager.DeleteCredential("Twinpack");
                    Username = null;
                    Password = null;
                }
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
