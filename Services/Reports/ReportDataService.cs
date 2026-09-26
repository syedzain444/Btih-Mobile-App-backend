using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Services.Reports;

public interface IReportDataService
{
    DataTable GetReportConfiguration(long rptId);
    List<DataTable> LoadReportDataSets(long rptId, string parameter, DateTime? d1 = null, DateTime? d2 = null);
    DataTable ExecuteQuerySafe(string query, string parameter);
    DataTable GetDataTable(OracleConnection connection, string query, string? parameter = null);
    string GetString(OracleConnection connection, string query, string parameter);
    string? GetEmployeeName(OracleConnection connection, int empId);
    string? GetPatientName(string parameter, string? templateName = null);
    string? ResolveDischargeId(OracleConnection connection, string patientVisitId);
    string ResolveTemplateName(DataTable config, params string[] fallbacks);
    List<DataTable> LoadConfiguredDataSets(
        OracleConnection connection,
        DataTable config,
        string parameter,
        DateTime? d1 = null,
        DateTime? d2 = null);
}

public sealed class ReportDataService : IReportDataService
{
    private readonly IConfiguration _configuration;

    public ReportDataService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString => _configuration.GetConnectionString("HMISConnection")
        ?? throw new InvalidOperationException("HMISConnection is not configured.");

    public DataTable GetReportConfiguration(long rptId)
    {
        // Explicit columns only — RM.* previously collided with RD.RPT_QUERY when REPORT_MST also has RPT_QUERY.
        const string query = """
            SELECT
                RM.RPT_ID,
                RM.RPT_NAME,
                RM.RPT_LOCATION,
                RM.PARAM,
                RM.DATE_PICKER,
                RD.RPT_DT_ID,
                RD.RPT_QUERY AS DATASET_QUERY
            FROM REPORT_MST RM
            INNER JOIN REPORT_DATASET RD
                ON RD.RPT_ID = RM.RPT_ID
               AND RD.IS_ACTIVE = 'Y'
            WHERE RM.RPT_ID = :rptId
            ORDER BY RD.RPT_DT_ID ASC
            """;

        var dt = new DataTable();
        using var con = new OracleConnection(ConnectionString);
        using var cmd = new OracleCommand(query, con);
        cmd.Parameters.Add(new OracleParameter("rptId", rptId));
        con.Open();
        using var da = new OracleDataAdapter(cmd);
        da.Fill(dt);
        return dt;
    }

    public List<DataTable> LoadReportDataSets(long rptId, string parameter, DateTime? d1 = null, DateTime? d2 = null)
    {
        var config = GetReportConfiguration(rptId);
        if (config.Rows.Count == 0)
            return [];

        using var con = new OracleConnection(ConnectionString);
        con.Open();
        return LoadConfiguredDataSets(con, config, parameter, d1, d2);
    }

    public DataTable ExecuteQuerySafe(string query, string parameter)
    {
        var formatted = FormatQuery(query, prm: 1, dtp: 0, parameter, null, null);
        return ExecuteFormattedQuery(formatted, parameter);
    }

    public DataTable GetDataTable(OracleConnection connection, string query, string? parameter = null)
    {
        using var cmd = new OracleCommand(query, connection);
        if (parameter != null && query.Contains(":id", StringComparison.Ordinal))
            cmd.Parameters.Add("id", parameter);

        using var da = new OracleDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);
        return dt;
    }

    public string GetString(OracleConnection connection, string query, string parameter)
    {
        using var cmd = new OracleCommand(query, connection);
        cmd.Parameters.Add("id", parameter);
        return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
    }

    public string? GetEmployeeName(OracleConnection connection, int empId)
    {
        const string userQuery = "SELECT FIRST_NAME||' '||LAST_NAME FROM EMPLOYEE_MST WHERE EMPLOYEE_ID=:id";
        using var cmd = new OracleCommand(userQuery, connection);
        cmd.Parameters.Add("id", empId);
        return cmd.ExecuteScalar()?.ToString();
    }

    public string? GetPatientName(string parameter, string? templateName = null)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return null;

        var normalizedTemplate = ReportTemplateNames.Normalize(templateName);
        if (normalizedTemplate == ReportTemplateNames.Prescription)
            return QueryPatientName(PrescriptionPatientNameQuery, parameter);

        if (normalizedTemplate is ReportTemplateNames.BillingReceipt or ReportTemplateNames.DischargeCertificate)
            return null;

        return QueryPatientName(DiagnosticPatientNameQuery, parameter);
    }

    private const string DiagnosticPatientNameQuery = """
        SELECT PATIENT_NAME
        FROM V_LABORATORY_GEN
        WHERE PAT_DIAG_ID = :id
        """;

    private const string PrescriptionPatientNameQuery = """
        SELECT PATIENTNAME
        FROM (
            SELECT PATIENTNAME
            FROM PRESCRIPTIONS
            WHERE PATIENT_VISIT_ID = :id
              AND PATIENTNAME IS NOT NULL
            ORDER BY PP_ID DESC
        )
        WHERE ROWNUM = 1
        """;

    private string? QueryPatientName(string query, string parameter)
    {
        using var con = new OracleConnection(ConnectionString);
        using var cmd = new OracleCommand(query, con);
        cmd.Parameters.Add(new OracleParameter("id", parameter));
        con.Open();
        return cmd.ExecuteScalar()?.ToString();
    }

    public string? ResolveDischargeId(OracleConnection connection, string patientVisitId)
    {
        const string query = "SELECT DISCHARGE_ID FROM PATIENT_DISCHARGE where PATIENT_VISIT_ID = :pvid and IS_CANCEL='N' ";
        using var cmd = new OracleCommand(query, connection);
        cmd.Parameters.Add("pvid", patientVisitId);
        return cmd.ExecuteScalar()?.ToString();
    }

    public string ResolveTemplateName(DataTable config, params string[] fallbacks)
    {
        if (config.Rows.Count == 0)
            throw new InvalidOperationException("Report configuration is empty.");

        var location = config.Rows[0]["RPT_LOCATION"]?.ToString();
        if (!string.IsNullOrWhiteSpace(location))
        {
            var fromDb = Path.GetFileNameWithoutExtension(location);
            if (!string.IsNullOrWhiteSpace(fromDb))
                return fromDb;
        }

        foreach (var fallback in fallbacks)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;
        }

        return config.Rows[0]["RPT_NAME"]?.ToString()
            ?? throw new InvalidOperationException("Unable to resolve report template name.");
    }

    public List<DataTable> LoadConfiguredDataSets(
        OracleConnection connection,
        DataTable config,
        string parameter,
        DateTime? d1 = null,
        DateTime? d2 = null)
    {
        var dataSets = new List<DataTable>();

        foreach (DataRow row in config.Rows)
        {
            var prm = Convert.ToInt32(row["PARAM"]);
            var dtp = Convert.ToInt32(row["DATE_PICKER"] ?? 0);
            var query = row.GetDatasetQuery();
            var formatted = FormatQuery(query, prm, dtp, parameter, d1, d2);
            dataSets.Add(ExecuteFormattedQuery(formatted, parameter));
        }

        return dataSets;
    }

    private DataTable ExecuteFormattedQuery(string query, string parameter)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new DataTable();

        if (query.Contains(":id", StringComparison.Ordinal))
        {
            using var con = new OracleConnection(ConnectionString);
            con.Open();
            return GetDataTable(con, query, parameter);
        }

        using var connection = new OracleConnection(ConnectionString);
        connection.Open();
        return GetDataTable(connection, query);
    }

    private static string FormatQuery(
        string query,
        int prm,
        int dtp,
        string parameter,
        DateTime? d1,
        DateTime? d2)
    {
        if (string.IsNullOrWhiteSpace(query))
            return query;

        if (dtp == 1 && prm == 0)
            return string.Format(query, d1?.ToString("dd-MMM-yyyy"));

        if (dtp == 1 && prm != 0)
            return string.Format(query, d1?.ToString("dd-MMM-yyyy"), parameter);

        if (dtp == 2 && prm == 0)
            return string.Format(query, d1?.ToString("dd-MMM-yyyy"), d2?.ToString("dd-MMM-yyyy"));

        if (dtp == 2 && prm != 0)
            return string.Format(query, d1?.ToString("dd-MMM-yyyy"), d2?.ToString("dd-MMM-yyyy"), parameter);

        if (dtp == 0 && prm != 0)
            return string.Format(query, parameter);

        return query;
    }
}
