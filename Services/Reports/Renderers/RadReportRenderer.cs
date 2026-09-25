using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Layout;
using HospitalMobileAPPApi.Services.Reports.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Renderers;

public sealed class RadReportRenderer : IReportPdfRenderer
{
    public string TemplateName => ReportTemplateNames.Radiology;

    public byte[] Render(ReportDocumentContext context)
    {
        var resolved = ReportDatasetResolver.ResolveDiagnostic(context.DataSets);
        var patient = resolved.Patient;
        var results = resolved.Results;
        var branding = context.Branding;
        var modality = patient.GetFirstString("MODALITY_NM");
        var title = string.IsNullOrWhiteSpace(modality) ? "RADIOLOGY REPORT" : $"{modality} REPORT";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(0.35f, Unit.Inch);
                page.MarginVertical(0.35f, Unit.Inch);
                page.DefaultTextStyle(x => x.FontFamily(ReportTheme.FontFamily));
                page.ApplyPageBackground(branding);

                page.Content().Column(col =>
                {
                    col.Item().Element(c => c.ComposeHospitalHeader(branding));
                    col.Item().PaddingTop(8).Element(c => c.ComposeCenteredTitle(title));
                    col.Item().PaddingTop(6).Element(c => DiagnosticPatientSection.ComposeRadGastStyle(c, patient));
                    col.Item().PaddingTop(8).Element(c => ComposeResults(c, results));
                    col.Item().PaddingTop(8).Element(c => c.ComposeMedicoLegalDisclaimer());
                    col.Item().PaddingTop(10).Element(c => ComposeSignatures(c, results));
                    col.Item().PaddingTop(8).Element(c => c.ComposePrintAuditFooter(branding));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeResults(IContainer container, DataTable results)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2f);
                columns.RelativeColumn(5f);
            });

            foreach (DataRow row in results.Rows)
            {
                var resultValue = row.GetString("RESULT_VALUE");
                if (string.IsNullOrWhiteSpace(resultValue))
                    continue;

                table.Cell().Element(CellBody).Text(row.GetString("DIA_ELE_MST_NM")).Style(ReportTheme.TableCell.Bold());
                table.Cell().Element(CellBody).Text(resultValue).Style(ReportTheme.TableCell);
            }
        });
    }

    private static void ComposeSignatures(IContainer container, DataTable results)
    {
        var row = results.FirstRow();
        container.ComposeSignatureBlock(
            row.GetString("PREPAREDBY_NM"),
            row.GetString("PB_TYPE"),
            row.GetDateTime("CREATED_ON").FormatDateTime("dd-MMM-yyyy h:mm:ss tt"),
            row.GetString("VERIFIEDBY_NM"),
            row.GetString("VB_TYPE"),
            row.GetString("DR_QUALIFICATIONS"),
            row.GetDateTime("VERIFIED_ON").FormatDateTime("dd-MMM-yyyy h:mm:ss tt"));
    }

    private static IContainer CellBody(IContainer container) =>
        container.BorderBottom(0.25f).BorderColor(ReportTheme.LightGrey).PaddingVertical(3).PaddingHorizontal(2);
}
