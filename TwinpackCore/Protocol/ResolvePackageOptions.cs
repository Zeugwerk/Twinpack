namespace Twinpack.Protocol
{
    /// <summary>Hints when resolving a package version across configured servers.</summary>
    public class ResolvePackageOptions
    {
        public string PreferredVersion;
        public string PreferredBranch;
        public string PreferredTarget;
        public string PreferredConfiguration;
    }
}
