using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HospitalMobileAPPApi.Filters
{
    /// <summary>
    /// Requires the caller's role to have the given admin-panel module permission (DB-driven RBAC).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RequireAdminModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _module;

        public RequireAdminModuleAttribute(string module) => _module = module;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    success = false,
                    message = "Authentication required",
                });
                return;
            }

            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                       ?? user.FindFirst("role")?.Value;
            if (string.IsNullOrWhiteSpace(role))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Built-in Admin always allowed for AccessControl bootstrap
            if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_module, AdminModules.AccessControl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var rbac = context.HttpContext.RequestServices.GetRequiredService<IRbacService>();
            var allowed = await rbac.RoleHasModuleAsync(role, _module);
            if (!allowed)
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    message = $"You do not have permission for module '{_module}'.",
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
            }
        }
    }
}
