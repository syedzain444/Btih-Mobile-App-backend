using HospitalMobileAPPApi.Services.Reports.Models;

namespace HospitalMobileAPPApi.Services.Reports;

public interface IReportPdfService
{
    byte[] Render(ReportDocumentContext context);
}

public sealed class ReportPdfService : IReportPdfService
{
    private readonly IReadOnlyDictionary<string, IReportPdfRenderer> _renderers;

    public ReportPdfService(IEnumerable<IReportPdfRenderer> renderers)
    {
        _renderers = renderers.ToDictionary(
            r => r.TemplateName,
            StringComparer.OrdinalIgnoreCase);
    }

    public byte[] Render(ReportDocumentContext context)
    {
        var templateName = ReportTemplateNames.Normalize(context.TemplateName)
            ?? throw new InvalidOperationException($"Report template '{context.TemplateName}' is not supported.");

        if (!_renderers.TryGetValue(templateName, out var renderer))
        {
            throw new InvalidOperationException(
                $"No QuestPDF renderer registered for template '{templateName}'. Supported templates: {string.Join(", ", _renderers.Keys)}.");
        }

        return renderer.Render(context);
    }
}
