using System.Data;

namespace HospitalMobileAPPApi.Services.Reports.Models;

public sealed class ReportDocumentContext
{
    public required string TemplateName { get; init; }
    public required string ReportTitle { get; init; }
    public required ReportBranding Branding { get; init; }
    public required IReadOnlyList<DataTable> DataSets { get; init; }

    public DataTable? GetDataSet(int index)
    {
        if (index < 0 || index >= DataSets.Count)
            return null;

        return DataSets[index];
    }

    public DataTable RequireDataSet(int index)
    {
        var table = GetDataSet(index)
            ?? throw new InvalidOperationException($"Dataset {index + 1} is required for report '{TemplateName}'.");

        return table;
    }
}
