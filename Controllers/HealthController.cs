using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [Tags("Health")]
    public class HealthController : ControllerBase
    {
        private readonly IMobilePortalSchemaService _schemaService;
        private readonly IWebHostEnvironment _environment;

        public HealthController(
            IMobilePortalSchemaService schemaService,
            IWebHostEnvironment environment)
        {
            _schemaService = schemaService;
            _environment = environment;
        }

        /// <summary>
        /// Schema + connected Oracle database details (passwords never returned).
        /// </summary>
        [HttpGet("schema")]
        public async Task<IActionResult> GetSchemaStatus()
        {
            IReadOnlyList<string> missing;
            try
            {
                missing = await _schemaService.GetMissingTablesAsync();
            }
            catch (Exception ex)
            {
                missing = Array.Empty<string>();
                var databasesOnError = await _schemaService.GetConnectedDatabasesAsync();
                return Ok(new
                {
                    success = false,
                    checkedAt = DateTimeOffset.Now,
                    environment = _environment.EnvironmentName,
                    message = "Could not verify mobile portal tables.",
                    schemaError = ex.Message,
                    missingTables = missing,
                    databases = databasesOnError,
                    scripts = SchemaScripts,
                });
            }

            var databases = await _schemaService.GetConnectedDatabasesAsync();
            var allDbOk = databases.All(db => db.Configured && db.Connected);
            var schemaOk = missing.Count == 0;

            return Ok(new
            {
                success = schemaOk && allDbOk,
                checkedAt = DateTimeOffset.Now,
                environment = _environment.EnvironmentName,
                message = BuildMessage(schemaOk, allDbOk, missing.Count, databases),
                schema = new
                {
                    ok = schemaOk,
                    missingTables = missing,
                    requiredTables = _schemaService.RequiredTables,
                    requiredTableCount = _schemaService.RequiredTables.Count,
                },
                missingTables = missing,
                databases,
                scripts = SchemaScripts,
            });
        }

        private static object SchemaScripts => new
        {
            full = "Docs/MOBILE_PORTAL_TABLES.sql",
            messagingOnly = "Docs/MOBILE_MESSAGING_TABLES.sql",
            pushTokens = "Docs/PATIENT_DEVICE_TOKEN.sql",
            recentActivity = "Docs/PATIENT_RECENT_ACTIVITY.sql",
            profilePhoto = "Docs/PATIENT_PROFILE_PHOTO.sql",
        };

        private static string BuildMessage(
            bool schemaOk,
            bool allDbOk,
            int missingCount,
            IReadOnlyList<ConnectedDatabaseInfo> databases)
        {
            var connectedCount = databases.Count(db => db.Connected);
            var configuredCount = databases.Count(db => db.Configured);

            if (schemaOk && allDbOk)
            {
                return $"All mobile portal tables are present. {connectedCount}/{configuredCount} databases connected.";
            }

            var parts = new List<string>();
            if (!allDbOk)
            {
                parts.Add($"{connectedCount}/{configuredCount} databases connected");
            }

            if (!schemaOk)
            {
                parts.Add(
                    missingCount == 1
                        ? "1 mobile portal table is missing"
                        : $"{missingCount} mobile portal tables are missing. Run Docs/MOBILE_PORTAL_TABLES.sql on HMIS schema");
            }

            return string.Join(". ", parts) + ".";
        }
    }
}
