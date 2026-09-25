using HospitalMobileAPPApi.Services.Reports.Models;

namespace HospitalMobileAPPApi.Services.Reports;

public interface IReportPdfRenderer
{
    string TemplateName { get; }

    byte[] Render(ReportDocumentContext context);
}
