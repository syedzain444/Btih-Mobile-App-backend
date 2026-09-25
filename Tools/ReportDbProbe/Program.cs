using Oracle.ManagedDataAccess.Client;
using System.Data;

var connStr = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("HMIS_CONNECTION")
    ?? "User Id=HMIS;Password=hmis;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=172.20.10.52)(PORT=8076))(CONNECT_DATA=(SERVICE_NAME=SIDHOSER)))";

await using var con = new OracleConnection(connStr);
await con.OpenAsync();
Console.WriteLine("Connected to HMIS\n");

await DumpReportConfig(con, [19, 22, 64, 141, 26, 27]);

var patDiagId = await GetScalar(con, "SELECT PAT_DIAG_ID FROM V_LABORATORY_GEN WHERE REPORT_STATUS='REPORT GENERATED' AND ROWNUM=1");
var visitId = await GetScalar(con, "SELECT PATIENT_VISIT_ID FROM PRESCRIPTIONS WHERE ROWNUM=1");
var billId = await GetScalar(con, "SELECT PAY_DTL_ID FROM BILL_PAY_DTL WHERE ROWNUM=1");
var dischargeVisitId = await GetScalar(con, "SELECT PATIENT_VISIT_ID FROM PATIENT_DISCHARGE WHERE IS_CANCEL='N' AND ROWNUM=1");
Console.WriteLine($"\nSample IDs:");
Console.WriteLine($"  Lab/Gastro/Rad parameters (PAT_DIAG_ID) = {patDiagId}");
Console.WriteLine($"  Prescription parameters (PATIENT_VISIT_ID) = {visitId}");
Console.WriteLine($"  Billing parameters (PAY_DTL_ID / billId) = {billId}");
Console.WriteLine($"  Discharge param (PATIENT_VISIT_ID) = {dischargeVisitId}");
if (!string.IsNullOrWhiteSpace(patDiagId))
{
    Console.WriteLine($"\n--- Testing lab datasets with PAT_DIAG_ID={patDiagId} ---");
    await TestLabDatasets(con, 19, patDiagId);
}

static async Task DumpReportConfig(OracleConnection con, int[] rptIds)
{
    var ids = string.Join(",", rptIds);
    const string sql = """
        SELECT RM.RPT_ID, RM.RPT_NAME, RM.RPT_LOCATION, RD.RPT_DT_ID, RM.PARAM, RM.DATE_PICKER, RD.RPT_QUERY
        FROM REPORT_MST RM
        LEFT JOIN REPORT_DATASET RD ON RD.RPT_ID = RM.RPT_ID AND RD.IS_ACTIVE = 'Y'
        WHERE RM.RPT_ID IN ({0})
        ORDER BY RM.RPT_ID, RD.RPT_DT_ID
        """;

    await using var cmd = new OracleCommand(string.Format(sql, ids), con);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"RPT_ID={reader["RPT_ID"]} NAME={reader["RPT_NAME"]} LOC={reader["RPT_LOCATION"]} DT_ID={reader["RPT_DT_ID"]} PARAM={reader["PARAM"]} DATE_PICKER={reader["DATE_PICKER"]}");
        var q = reader["RPT_QUERY"]?.ToString() ?? "";
        Console.WriteLine($"  QUERY: {q[..Math.Min(q.Length, 300)]}{(q.Length > 300 ? "..." : "")}");
    }
}

static async Task TestLabDatasets(OracleConnection con, int rptId, string patDiagId)
{
    const string cfgSql = """
        SELECT RD.RPT_QUERY
        FROM REPORT_DATASET RD
        WHERE RD.RPT_ID = :id AND RD.IS_ACTIVE = 'Y'
        ORDER BY RD.RPT_DT_ID
        """;

    await using var cfgCmd = new OracleCommand(cfgSql, con);
    cfgCmd.Parameters.Add("id", rptId);
    await using var cfgReader = await cfgCmd.ExecuteReaderAsync();
    var i = 0;
    while (await cfgReader.ReadAsync())
    {
        i++;
        var query = cfgReader["RPT_QUERY"]?.ToString() ?? "";
        var dt = await ExecuteQuery(con, query, patDiagId);
        Console.WriteLine($"DataSet{i}: rows={dt.Rows.Count}, columns=[{string.Join(", ", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}]");
        if (dt.Rows.Count > 0)
        {
            var row = dt.Rows[0];
            foreach (DataColumn col in dt.Columns)
                Console.WriteLine($"  {col.ColumnName} = {row[col]}");
        }
    }
}

static async Task<DataTable> ExecuteQuery(OracleConnection con, string query, string param)
{
    await using var cmd = new OracleCommand { Connection = con };
    if (query.Contains(":id", StringComparison.Ordinal))
    {
        cmd.CommandText = query;
        cmd.Parameters.Add("id", param);
    }
    else if (query.Contains("{0}", StringComparison.Ordinal))
    {
        cmd.CommandText = string.Format(query, param);
    }
    else
    {
        cmd.CommandText = query;
    }

    using var da = new OracleDataAdapter(cmd);
    var dt = new DataTable();
    da.Fill(dt);
    return dt;
}

static async Task DumpScalar(OracleConnection con, string sql)
{
    var v = await GetScalar(con, sql);
    Console.WriteLine($"{sql.Split("FROM")[1].Trim()[..Math.Min(40, sql.Length)]}: {v}");
}

static async Task<string?> GetScalar(OracleConnection con, string sql)
{
    await using var cmd = new OracleCommand(sql, con);
    return (await cmd.ExecuteScalarAsync())?.ToString();
}
