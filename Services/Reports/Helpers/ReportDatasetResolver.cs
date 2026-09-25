using System.Data;

namespace HospitalMobileAPPApi.Services.Reports.Helpers;

public sealed class ResolvedDiagnosticDatasets
{
    public required DataTable Patient { get; init; }
    public required DataTable Results { get; init; }
    public DataTable? Images { get; init; }
}

public static class ReportDatasetResolver
{
    public static ResolvedDiagnosticDatasets ResolveDiagnostic(IReadOnlyList<DataTable> dataSets)
    {
        DataTable? patient = null;
        DataTable? results = null;
        DataTable? images = null;

        foreach (var table in dataSets)
        {
            if (table.HasColumn("LOCATION") && table.HasColumn("CAPTION"))
            {
                images = table;
                continue;
            }

            if (table.HasColumn("DIA_ELE_MST_NM") || table.HasColumn("RESULT_VALUE"))
            {
                results = table;
                continue;
            }

            if (table.HasColumn("PATIENT_NAME") || table.HasColumn("MR_NO") || table.HasColumn("PAT_DIAG_ID")
                || table.HasColumn("FIRST_NAME") || table.HasColumn("TESTNAME"))
            {
                patient = table;
            }
        }

        patient ??= dataSets.ElementAtOrDefault(0) ?? EmptyTable();
        results ??= dataSets.ElementAtOrDefault(1) ?? EmptyTable();

        if (images == null && dataSets.Count > 2)
            images = dataSets[2];

        return new ResolvedDiagnosticDatasets
        {
            Patient = patient,
            Results = results,
            Images = images,
        };
    }

    public static string? ExtractPatientName(IReadOnlyList<DataTable> dataSets)
    {
        foreach (var table in dataSets)
        {
            if (!table.HasRows())
                continue;

            var row = table.Rows[0];
            var name = row.GetString("PATIENTNAME");
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            name = row.GetString("PATIENT_NAME");
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return null;
    }

    public static DataTable ResolvePrimary(IReadOnlyList<DataTable> dataSets)
        => dataSets.FirstOrDefault(t => t.Rows.Count > 0) ?? dataSets.ElementAtOrDefault(0) ?? EmptyTable();

    public static DataTable ResolveSecondary(IReadOnlyList<DataTable> dataSets)
        => dataSets.Skip(1).FirstOrDefault(t => t.Rows.Count > 0) ?? dataSets.ElementAtOrDefault(1) ?? EmptyTable();

    private static DataTable EmptyTable() => new();

    private static bool HasColumn(this DataTable table, string column)
    {
        foreach (DataColumn c in table.Columns)
        {
            if (c.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
