namespace Twinpack.Configuration
{
    /// <summary>
    /// Metadata that only matters when a PLC/library is *published* as a package (twinpack push /
    /// the Twinpack VSIX publish dialog). Deliberately not part of ConfigPlcProject / config.json:
    /// it either gets derived fresh from the compiled library's own TwinCAT project properties
    /// (Title/Description/Author/Company), falls back to whatever is already published on the
    /// server for that package, or is supplied explicitly (via `twinpack push` flags or the publish
    /// dialog) for a single push - it is never persisted back to a solution's config.json.
    /// </summary>
    public class PlcPublishMetadata
    {
        public PlcPublishMetadata() { }

        public PlcPublishMetadata(PlcPublishMetadata other)
        {
            if (other == null)
                return;

            Description = other.Description;
            Authors = other.Authors;
            DisplayName = other.DisplayName;
            ProjectUrl = other.ProjectUrl;
            Entitlement = other.Entitlement;
            License = other.License;
            LicenseFile = other.LicenseFile;
            LicenseTmcFile = other.LicenseTmcFile;
            IconFile = other.IconFile;
            BinaryDownloadUrl = other.BinaryDownloadUrl;
        }

        public string Description { get; set; }
        public string Authors { get; set; }
        public string DisplayName { get; set; }
        public string ProjectUrl { get; set; }
        public string Entitlement { get; set; }
        public string License { get; set; }
        public string LicenseFile { get; set; }
        public string LicenseTmcFile { get; set; }
        public string IconFile { get; set; }
        public string BinaryDownloadUrl { get; set; }

        /// <summary>
        /// Returns a copy of <paramref name="baseline"/> with every non-empty field of
        /// <paramref name="overrides"/> applied on top of it.
        /// </summary>
        public static PlcPublishMetadata Merge(PlcPublishMetadata baseline, PlcPublishMetadata overrides)
        {
            var result = new PlcPublishMetadata(baseline);
            if (overrides == null)
                return result;

            if (!string.IsNullOrEmpty(overrides.Description)) result.Description = overrides.Description;
            if (!string.IsNullOrEmpty(overrides.Authors)) result.Authors = overrides.Authors;
            if (!string.IsNullOrEmpty(overrides.DisplayName)) result.DisplayName = overrides.DisplayName;
            if (!string.IsNullOrEmpty(overrides.ProjectUrl)) result.ProjectUrl = overrides.ProjectUrl;
            if (!string.IsNullOrEmpty(overrides.Entitlement)) result.Entitlement = overrides.Entitlement;
            if (!string.IsNullOrEmpty(overrides.License)) result.License = overrides.License;
            if (!string.IsNullOrEmpty(overrides.LicenseFile)) result.LicenseFile = overrides.LicenseFile;
            if (!string.IsNullOrEmpty(overrides.LicenseTmcFile)) result.LicenseTmcFile = overrides.LicenseTmcFile;
            if (!string.IsNullOrEmpty(overrides.IconFile)) result.IconFile = overrides.IconFile;
            if (!string.IsNullOrEmpty(overrides.BinaryDownloadUrl)) result.BinaryDownloadUrl = overrides.BinaryDownloadUrl;

            return result;
        }
    }
}
