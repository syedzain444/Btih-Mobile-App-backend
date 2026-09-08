using HospitalMobileAPPApi.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Filters
{
    public class OracleExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<OracleExceptionFilter> _logger;

        public OracleExceptionFilter(ILogger<OracleExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var ex = context.Exception;
            if (ex is not OracleException && ex.InnerException is not OracleException)
            {
                return;
            }

            var oracleEx = ex as OracleException ?? (OracleException)ex.InnerException!;
            if (!DatabaseExceptionHelper.TryGetFriendlyMessage(oracleEx, out var message, out var statusCode))
            {
                return;
            }

            _logger.LogError(oracleEx, "Oracle error in {Action}", context.ActionDescriptor.DisplayName);

            context.Result = new ObjectResult(new
            {
                success = false,
                message,
                oracleError = $"ORA-{oracleEx.Number:00000}",
            })
            {
                StatusCode = statusCode,
            };

            context.ExceptionHandled = true;
        }
    }
}
