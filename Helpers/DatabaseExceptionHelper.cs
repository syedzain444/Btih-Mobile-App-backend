using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Helpers
{
    public static class DatabaseExceptionHelper
    {
        public static bool TryGetFriendlyMessage(Exception ex, out string message, out int statusCode)
        {
            message = string.Empty;
            statusCode = StatusCodes.Status503ServiceUnavailable;

            if (ex is not OracleException oracleEx)
            {
                return false;
            }

            message = oracleEx.Number switch
            {
                1017 => "Database login failed (ORA-01017). Check HMISConnection User Id/Password in appsettings or environment variables.",
                942 => "Required mobile portal table is missing (ORA-00942). DBA must run Docs/MOBILE_PORTAL_TABLES.sql on the HMIS schema (at minimum PATIENT_MSG_THREAD, PATIENT_MSG, PATIENT_MSG_ATTACHMENT).",
                28000 => "HMIS database account is locked (ORA-28000). Ask DBA to run: ALTER USER HMIS ACCOUNT UNLOCK;",
                12170 or 12541 or 12545 => "Cannot reach Oracle database server. Check VPN/network and connection string host/port.",
                _ => $"Database error (ORA-{oracleEx.Number:00000}): {oracleEx.Message}",
            };

            return true;
        }
    }
}
