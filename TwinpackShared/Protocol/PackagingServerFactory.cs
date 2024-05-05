namespace Twinpack.Protocol
{
    interface IPackagingServerFactory
    {
        IPackageServer Create(string name, string uri);
        string ServerType { get; }
    }
}
