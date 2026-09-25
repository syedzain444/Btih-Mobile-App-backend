using System.Data;
using System.Globalization;

namespace HospitalMobileAPPApi.Services.Reports.Helpers;

public static class DataTableExtensions
{
    public static string GetString(this DataRow? row, string column, string defaultValue = "")
    {
        if (row == null)
            return defaultValue;

        var value = row.GetColumnValue(column);
        if (value != null)
            return value;

        if (column.Equals("PATIENT_NAME", StringComparison.OrdinalIgnoreCase))
        {
            var first = row.GetColumnValue("FIRST_NAME");
            var last = row.GetColumnValue("LAST_NAME");
            var combined = $"{first} {last}".Trim();
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;
        }

        if (column.Equals("PATIENTNAME", StringComparison.OrdinalIgnoreCase))
        {
            var patientName = row.GetColumnValue("PATIENT_NAME");
            if (!string.IsNullOrWhiteSpace(patientName))
                return patientName;

            var first = row.GetColumnValue("FIRST_NAME");
            var last = row.GetColumnValue("LAST_NAME");
            var combined = $"{first} {last}".Trim();
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;
        }

        return defaultValue;
    }

    public static string GetFirstString(this DataTable? table, string column, string defaultValue = "")
    {
        if (table == null || table.Rows.Count == 0)
            return defaultValue;

        return table.Rows[0].GetString(column, defaultValue);
    }

    public static DateTime? GetDateTime(this DataRow? row, string column)
    {
        if (row == null)
            return null;

        var col = FindColumn(row.Table, column);
        if (col == null || row[col] is DBNull)
            return null;

        return row[col] switch
        {
            DateTime dt => dt,
            _ => DateTime.TryParse(Convert.ToString(row[col], CultureInfo.InvariantCulture), out var parsed)
                ? parsed
                : null
        };
    }

    public static string FormatDate(this DateTime? value, string format = "dd-MMM-yy")
    {
        return value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string FormatDateTime(this DateTime? value, string format = "dd-MMM-yyyy h:mm:ss tt")
    {
        return value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static decimal GetDecimal(this DataRow? row, string column)
    {
        if (row == null)
            return 0m;

        var col = FindColumn(row.Table, column);
        if (col == null || row[col] is DBNull)
            return 0m;

        return Convert.ToDecimal(row[col], CultureInfo.InvariantCulture);
    }

    public static decimal GetFirstDecimal(this DataTable? table, string column)
    {
        if (table == null || table.Rows.Count == 0)
            return 0m;

        return table.Rows[0].GetDecimal(column);
    }

    public static int? GetInt(this DataRow? row, string column)
    {
        if (row == null)
            return null;

        var col = FindColumn(row.Table, column);
        if (col == null || row[col] is DBNull)
            return null;

        return Convert.ToInt32(row[col], CultureInfo.InvariantCulture);
    }

    public static bool HasRows(this DataTable? table) => table != null && table.Rows.Count > 0;

    public static DataRow? FirstRow(this DataTable? table)
    {
        if (table == null || table.Rows.Count == 0)
            return null;

        return table.Rows[0];
    }

    public static string ComputeAgeYears(this DataRow? row, string dobColumn = "DT_DOB")
    {
        var dob = row.GetDateTime(dobColumn);
        if (dob == null)
            return row.GetString("ACT_AGE");

        var years = DateTime.Today.Year - dob.Value.Year;
        if (dob.Value.Date > DateTime.Today.AddYears(-years))
            years--;

        return $"{years} yrs ";
    }

    public static string? GetDatasetQuery(this DataRow row)
    {
        foreach (var name in new[] { "DATASET_QUERY", "RPT_QUERY" })
        {
            var col = FindColumn(row.Table, name);
            if (col != null)
            {
                var value = row[col]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return string.Empty;
    }

    private static string? GetColumnValue(this DataRow row, string column)
    {
        var col = FindColumn(row.Table, column);
        if (col == null || row[col] is DBNull)
            return null;

        return Convert.ToString(row[col], CultureInfo.InvariantCulture)?.Trim();
    }

    private static DataColumn? FindColumn(DataTable table, string name)
    {
        foreach (DataColumn column in table.Columns)
        {
            if (column.ColumnName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return column;
        }

        return null;
    }
}
