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
        private TwinpackServer _twinpackServer;
        public Authentication(TwinpackServer twinpackServer)
        {
            _twinpackServer = twinpackServer;
        }


        public async Task LoginAsync()
        {
            if (_twinpackServer.LoggedIn)
                return;

            var credentials = CredentialManager.PromptForCredentials(
                messageText: $"Login to your Twinpack Server account. Logging in will give you access to additional features. " +
                $"It enables you to intall packages that are maintained by you, but not yet released. It also allows you to upload a new package into your Twinpack repository.",
                captionText: "Twinpack Server login", saveCredential: CredentialSaveOption.Hidden);

            await _twinpackServer.LoginAsync(credentials.UserName, credentials.Password);
        }

        public void Logout()
        {
            _twinpackServer.Logout();
        }
    }
}
