using HospitalMobileAPPApi.Filters;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>Admin users, roles, and module permissions (RBAC).</summary>
    [ApiController]
    [Route("api/admin/rbac")]
    [Authorize(Policy = AuthorizationPolicies.PortalAccess)]
    [RequireAdminModule(AdminModules.AccessControl)]
    [Tags("Admin RBAC")]
    public class AdminRbacController : ControllerBase
    {
        private readonly IRbacService _rbac;

        public AdminRbacController(IRbacService rbac) => _rbac = rbac;

        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissions()
        {
            var items = await _rbac.GetPermissionsAsync();
            return Ok(new { success = true, data = items });
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles([FromQuery] bool includeInactive = true)
        {
            var items = await _rbac.GetRolesAsync(includeInactive);
            return Ok(new { success = true, data = items });
        }

        [HttpGet("roles/{id:int}")]
        public async Task<IActionResult> GetRole(int id)
        {
            var item = await _rbac.GetRoleAsync(id);
            return item == null
                ? NotFound(new { success = false, message = "Role not found" })
                : Ok(new { success = true, data = item });
        }

        [HttpPost("roles")]
        public async Task<IActionResult> CreateRole([FromBody] CreateAdminRoleRequest request)
        {
            try
            {
                var created = await _rbac.CreateRoleAsync(request ?? new CreateAdminRoleRequest());
                return Ok(new { success = true, message = "Role created", data = created });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("roles/{id:int}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateAdminRoleRequest request)
        {
            try
            {
                var updated = await _rbac.UpdateRoleAsync(id, request ?? new UpdateAdminRoleRequest());
                return updated == null
                    ? NotFound(new { success = false, message = "Role not found" })
                    : Ok(new { success = true, message = "Role updated", data = updated });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("roles/{id:int}/permissions")]
        public async Task<IActionResult> SetRolePermissions(int id, [FromBody] SetRolePermissionsRequest request)
        {
            var updated = await _rbac.SetRolePermissionsAsync(
                id,
                request?.Permissions ?? new List<string>());
            return updated == null
                ? NotFound(new { success = false, message = "Role not found" })
                : Ok(new { success = true, message = "Permissions updated", data = updated });
        }

        [HttpDelete("roles/{id:int}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var (success, message) = await _rbac.DeleteRoleAsync(id);
            return success
                ? Ok(new { success = true, message })
                : BadRequest(new { success = false, message });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var items = await _rbac.GetStaffUsersAsync();
            return Ok(new { success = true, data = items });
        }

        [HttpGet("users/{id:int}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var item = await _rbac.GetStaffUserAsync(id);
            return item == null
                ? NotFound(new { success = false, message = "User not found" })
                : Ok(new { success = true, data = item });
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateAdminStaffRequest request)
        {
            try
            {
                var created = await _rbac.CreateStaffUserAsync(request ?? new CreateAdminStaffRequest());
                return Ok(new { success = true, message = "User created", data = created });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("users/{id:int}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateAdminStaffRequest request)
        {
            try
            {
                var updated = await _rbac.UpdateStaffUserAsync(id, request ?? new UpdateAdminStaffRequest());
                return updated == null
                    ? NotFound(new { success = false, message = "User not found" })
                    : Ok(new { success = true, message = "User updated", data = updated });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("users/{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetAdminPasswordRequest request)
        {
            var (success, message) = await _rbac.ResetStaffPasswordAsync(
                id,
                request?.NewPassword ?? string.Empty);
            return success
                ? Ok(new { success = true, message })
                : BadRequest(new { success = false, message });
        }
    }
}
