namespace Twinpack.Packaging
{
    interface IPackagingServerFactory
    {
        IPackageServer Create(string name, string uri);
        string ServerType { get; }
    }

    class NativePackagingServer : IPackagingServerFactory
    {
        public IPackageServer Create(string name, string uri)
        {
            return new TwinpackServer(name, uri);
        }

        public string ServerType { get; } = "Twinpack Repository";
    }
}
