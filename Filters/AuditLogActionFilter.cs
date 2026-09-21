using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HospitalMobileAPPApi.Filters
{
    public class AuditLogActionFilter : IAsyncActionFilter
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogActionFilter(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            if (executed.Exception != null)
            {
                return;
            }

            var method = context.HttpContext.Request.Method;
            if (method is not ("POST" or "PUT" or "PATCH" or "DELETE"))
            {
                return;
            }

            var path = context.HttpContext.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/api/Health", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var user = context.HttpContext.User;
            var mrNo = context.ActionArguments.Values
                .Select(v => v?.GetType().GetProperty("MrNo")?.GetValue(v)?.ToString())
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            await _auditLogService.WriteAsync(new AuditLogEntry
            {
                ActorId = AuditContext.GetActorId(user),
                ActorRole = AuditContext.GetActorRole(user),
                Action = $"{method} {path}",
                EntityType = context.RouteData.Values["controller"]?.ToString(),
                MrNo = mrNo,
                IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                Details = $"Status={context.HttpContext.Response.StatusCode}",
            });
        }
    }
}
