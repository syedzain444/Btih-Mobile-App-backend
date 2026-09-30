using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HospitalMobileAPPApi.Helpers
{
    /// <summary>
    /// Profile photos are stored under ProgramData (writable by IIS AppPool),
    /// not under inetpub\wwwroot which is often read-only.
    /// </summary>
    public static class ProfilePhotoStorage
    {
        public const string UrlPrefix = "/uploads/profiles";
        public const string RelativePrefix = "uploads/profiles";

        public static string GetRoot(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configured = configuration["ProfilePhotos:RootPath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured.Trim());
            }

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(programData))
            {
                return Path.Combine(programData, "BTIH", "HospitalMobileAPPApi", "uploads", "profiles");
            }

            var webRoot = environment.WebRootPath
                ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            return Path.Combine(webRoot, "uploads", "profiles");
        }

        public static string EnsureRoot(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var root = GetRoot(configuration, environment);
            Directory.CreateDirectory(root);
            TryGrantModifyAccess(root);
            return root;
        }

        public static string LegacyWwwRootFolder(IWebHostEnvironment environment)
        {
            var webRoot = environment.WebRootPath
                ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            return Path.Combine(webRoot, "uploads", "profiles");
        }

        /// <summary>Resolve a DB-relative path like uploads/profiles/{mr}/{file} to disk.</summary>
        public static string? ResolvePhysicalPath(
            string? relativePath,
            string storageRoot,
            IWebHostEnvironment environment)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var normalized = relativePath.Trim().TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fileNamePart = normalized;
            var markerOs = $"uploads{Path.DirectorySeparatorChar}profiles";
            if (normalized.StartsWith(markerOs, StringComparison.OrdinalIgnoreCase))
            {
                fileNamePart = normalized[markerOs.Length..].TrimStart(Path.DirectorySeparatorChar);
            }

            var primary = Path.Combine(storageRoot, fileNamePart);
            if (File.Exists(primary))
            {
                return primary;
            }

            var legacy = Path.Combine(
                environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
                normalized);
            if (File.Exists(legacy))
            {
                return legacy;
            }

            return primary;
        }

        private static void TryGrantModifyAccess(string directoryPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                var info = new DirectoryInfo(directoryPath);
                var security = info.GetAccessControl();
                var rules = new[]
                {
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                };

                foreach (var sid in rules)
                {
                    security.AddAccessRule(new FileSystemAccessRule(
                        sid,
                        FileSystemRights.Modify,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                }

                // IIS application pool identity group when present.
                try
                {
                    var iis = new NTAccount("IIS_IUSRS");
                    security.AddAccessRule(new FileSystemAccessRule(
                        iis,
                        FileSystemRights.Modify,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                }
                catch
                {
                    // IIS may not exist on non-server boxes.
                }

                info.SetAccessControl(security);
            }
            catch
            {
                // Best-effort — ProgramData is usually already writable.
            }
        }
    }
}
