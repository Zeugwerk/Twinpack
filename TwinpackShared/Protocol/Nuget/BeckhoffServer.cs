using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Twinpack.Models;
using Twinpack.Exceptions;
using System.Reflection;
using AdysTech.CredentialManager;
using System.Threading;
using System.Security.Cryptography;
using System.Runtime.Remoting.Messaging;
using System.Runtime.CompilerServices;

namespace Twinpack.Protocol
{
    class BeckhoffPackagingServerFactory : IPackagingServerFactory
    {
        public IPackageServer Create(string name, string uri)
        {
            return new BeckhoffServer(name, uri);
        }

        public string ServerType { get; } = "Beckhoff Repository";
    }

    public class BeckhoffServer : NugetServer, IPackageServer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public new string ServerType { get; } = "Beckhoff Repository";

        public BeckhoffServer(string name = "", string url = null) : base(name,url)
        {

        }
    }
}
