using Microsoft.AspNetCore.DataProtection;

namespace HospitalMobileAPPApi.Configuration
{
    public static class DataProtectionConfigurator
    {
        private const string ApplicationName = "HospitalMobileAPPApi";

        public static void ConfigureDataProtection(WebApplicationBuilder builder)
        {
            var configuredPath = builder.Configuration["DataProtection:KeysPath"];
            var candidatePaths = new[]
            {
                configuredPath,
                Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "BTIH",
                    "HospitalMobileAPPApi",
                    "DataProtection-Keys"),
            };

            foreach (var path in candidatePaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!TryEnsureWritableDirectory(path!, out var error))
                {
                    Console.WriteLine($"[DataProtection] Skipping keys path '{path}': {error}");
                    continue;
                }

                Console.WriteLine($"[DataProtection] Using keys path: {path}");
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(path!))
                    .SetApplicationName(ApplicationName);
                return;
            }

            Console.WriteLine(
                "[DataProtection] WARNING: No writable keys directory found. " +
                "Keys will not persist across restarts. Grant IIS App Pool write access to " +
                "DataProtection-Keys or set DataProtection:KeysPath in configuration.");

            builder.Services.AddDataProtection()
                .SetApplicationName(ApplicationName);
        }

        private static bool TryEnsureWritableDirectory(string path, out string? error)
        {
            error = null;

            try
            {
                Directory.CreateDirectory(path);

                var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "ok");
                File.Delete(testFile);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
