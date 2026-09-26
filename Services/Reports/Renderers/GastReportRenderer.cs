using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Layout;
using HospitalMobileAPPApi.Services.Reports.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Renderers;

public sealed class GastReportRenderer : IReportPdfRenderer
{
    public string TemplateName => ReportTemplateNames.Gastro;

    public byte[] Render(ReportDocumentContext context)
    {
        var resolved = ReportDatasetResolver.ResolveDiagnostic(context.DataSets);
        var patient = resolved.Patient;
        var results = resolved.Results;
        var images = resolved.Images;
        var branding = context.Branding;
        var title = patient.GetFirstString("DIAGNOSTIC_NAME");

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
                    col.Item().PaddingTop(6).Element(c => DiagnosticPatientSection.ComposeRadGastStyle(c, patient, useDiagnosticNameTitle: true));
                    col.Item().PaddingTop(8).Element(c => ComposeIndicationSection(c, results));
                    col.Item().PaddingTop(6).Element(c => ComposeResults(c, results));
                    if (images.HasRows())
                        col.Item().PaddingTop(8).Element(c => ComposeImages(c, images!));
                    col.Item().PaddingTop(10).Element(c => ComposeSignatures(c, results));
                    col.Item().PaddingTop(8).Element(c => c.ComposePrintAuditFooter(branding));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeIndicationSection(IContainer container, DataTable results)
    {
        var indicationRows = results.Rows.Cast<DataRow>()
            .Where(r => r.GetString("DIA_ELE_MST_NM").Contains("Indication", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (indicationRows.Count == 0)
            return;

        container.Column(col =>
        {
            foreach (var row in indicationRows)
            {
                col.Item().Text(row.GetString("DIA_ELE_MST_NM")).Style(ReportTheme.TableCell.Bold());
                col.Item().Text(row.GetString("RESULT_VALUE")).Style(ReportTheme.TableCell);
            }
        });
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
                var name = row.GetString("DIA_ELE_MST_NM");
                if (name.Contains("Indication", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Medicine", StringComparison.OrdinalIgnoreCase))
                    continue;

                var resultValue = row.GetString("RESULT_VALUE");
                if (string.IsNullOrWhiteSpace(resultValue))
                    continue;

                table.Cell().Element(CellBody).Text(name).Style(ReportTheme.TableCell.Bold());
                table.Cell().Element(CellBody).Text(resultValue).Style(ReportTheme.TableCell);
            }
        });
    }

    private static void ComposeImages(IContainer container, DataTable images)
    {
        var rows = images.Rows.Cast<DataRow>().ToList();
        container.Column(col =>
        {
            for (var i = 0; i < rows.Count; i += 2)
            {
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Element(c => ComposeImageCell(c, rows[i]));
                    if (i + 1 < rows.Count)
                        row.RelativeItem().Element(c => ComposeImageCell(c, rows[i + 1]));
                    else
                        row.RelativeItem();
                });
            }
        });
    }

    private static void ComposeImageCell(IContainer container, DataRow imageRow)
    {
        var location = imageRow.GetString("LOCATION");
        container.Column(col =>
        {
            if (File.Exists(location))
            {
                col.Item().Height(180).Image(File.ReadAllBytes(location)).FitArea();
            }

            var caption = imageRow.GetString("CAPTION");
            if (!string.IsNullOrWhiteSpace(caption))
                col.Item().AlignCenter().Text(caption).Style(ReportTheme.TableCell);
        });
    }

    private static void ComposeSignatures(IContainer container, DataTable results)
    {
        var row = results.FirstRow();
        container.ComposeSignatureBlock(
            row.GetString("PREPAREDBY_NM"),
            row.GetString("PB_TYPE"),
            row.GetDateTime("ENTEREDON").FormatDateTime("dd-MMM-yyyy h:mm:ss tt"),
            row.GetString("VERIFIEDBY_NM"),
            row.GetString("VB_TYPE"),
            row.GetString("DR_QUALIFICATIONS"),
            row.GetDateTime("VERIFIED_ON").FormatDateTime("dd-MMM-yyyy h:mm:ss tt"));
    }

    private static IContainer CellBody(IContainer container) =>
        container.BorderBottom(0.25f).BorderColor(ReportTheme.LightGrey).PaddingVertical(3).PaddingHorizontal(2);
}
