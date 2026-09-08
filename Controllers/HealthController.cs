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

        public HealthController(IMobilePortalSchemaService schemaService)
        {
            _schemaService = schemaService;
        }

        [HttpGet("schema")]
        public async Task<IActionResult> GetSchemaStatus()
        {
            var missing = await _schemaService.GetMissingTablesAsync();

            return Ok(new
            {
                success = missing.Count == 0,
                message = missing.Count == 0
                    ? "All mobile portal tables are present."
                    : "One or more mobile portal tables are missing. Run Docs/MOBILE_PORTAL_TABLES.sql on HMIS schema.",
                missingTables = missing,
                scripts = new
                {
                    full = "Docs/MOBILE_PORTAL_TABLES.sql",
                    messagingOnly = "Docs/MOBILE_MESSAGING_TABLES.sql",
                    pushTokens = "Docs/PATIENT_DEVICE_TOKEN.sql",
                },
            });
        }
    }
}
