using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Layout;
using HospitalMobileAPPApi.Services.Reports.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Renderers;

public sealed class LabReportRenderer : IReportPdfRenderer
{
    private static readonly HashSet<string> BoldElementIds = new(StringComparer.Ordinal)
    {
        "2074", "2144", "2149", "2161", "2154"
    };

    public string TemplateName => ReportTemplateNames.Lab;

    public byte[] Render(ReportDocumentContext context)
    {
        var resolved = ReportDatasetResolver.ResolveDiagnostic(context.DataSets);
        var patient = resolved.Patient;
        var results = resolved.Results;
        var branding = context.Branding;
        var patientRow = patient.FirstRow();

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
                    col.Item().PaddingTop(8).Element(c => c.ComposeCenteredTitle("LABORATORY REPORT"));
                    col.Item().PaddingTop(6).Element(c => DiagnosticPatientSection.ComposeLabStyle(c, patient));

                    col.Item().PaddingTop(8).Element(c => ComposeTestHeader(c, patient, patientRow));
                    col.Item().PaddingTop(4).Element(c => ComposeResultsTable(c, results));
                    col.Item().PaddingTop(10).Element(c => ComposeTravelInfo(c, patientRow));
                    col.Item().PaddingTop(12).Element(c => ComposeSignatures(c, results));
                    col.Item().PaddingTop(8).Element(c => c.ComposePrintAuditFooter(branding));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeTestHeader(IContainer container, DataTable patient, DataRow? patientRow)
    {
        container.Row(row =>
        {
            row.RelativeItem(2).Column(col =>
            {
                col.Item().Text(patient.GetFirstString("TESTNAME")).Style(ReportTheme.SectionTitle);
                var sampleDate = patientRow.GetDateTime("DT_SAMPLECOLLECTION");
                if (sampleDate != null)
                    col.Item().Text(sampleDate.FormatDateTime("dd-MMM-yy hh:m tt")).Style(ReportTheme.Label);
            });

            row.RelativeItem().Column(col =>
            {
                var passport = patientRow.GetString("PASSPORT");
                if (!string.IsNullOrWhiteSpace(passport))
                {
                    col.Item().Text("CNIC").Style(ReportTheme.Label);
                    col.Item().Text("PASSPORT").Style(ReportTheme.Label);
                    col.Item().Text("REF PNR").Style(ReportTheme.Label);
                    col.Item().Text("FLIGHT NO").Style(ReportTheme.Label);
                }
            });

            row.RelativeItem().Column(col =>
            {
                var passport = patientRow.GetString("PASSPORT");
                if (!string.IsNullOrWhiteSpace(passport))
                {
                    col.Item().Text($": {patientRow.GetString("CNIC")}").Style(ReportTheme.Value);
                    col.Item().Text($": {passport}").Style(ReportTheme.Value);
                    col.Item().Text($": {patientRow.GetString("REF_PNR")}").Style(ReportTheme.Value);
                    col.Item().Text($": {patientRow.GetString("FLIGHT_NO")}").Style(ReportTheme.Value);
                }
            });
        });
    }

    private static void ComposeResultsTable(IContainer container, DataTable results)
    {
        var previousDate = results.GetFirstString("PREVIOUSRESULTDATE");
        if (string.IsNullOrWhiteSpace(previousDate) && results.Rows.Count > 0)
        {
            var dt = results.Rows[0].GetDateTime("PREVIOUSRESULTDATE");
            previousDate = dt.FormatDate("dd-MMM-yy");
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(2.6f);
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellHeader).Text("Investigation").Style(ReportTheme.TableHeader);
                header.Cell().Element(CellHeader).AlignCenter().Text("Values").Style(ReportTheme.TableHeader);
                header.Cell().Element(CellHeader).AlignCenter().Text("Reference Ranges").Style(ReportTheme.TableHeader);
                header.Cell().Element(CellHeader).Column(col =>
                {
                    col.Item().Text("* Last Available Results").Style(ReportTheme.TableHeader);
                    if (!string.IsNullOrWhiteSpace(previousDate))
                        col.Item().Text(previousDate).Style(ReportTheme.Label);
                });
            });

            foreach (DataRow row in results.Rows)
            {
                var fullLine = row.GetString("FULL_LINE").Equals("Y", StringComparison.OrdinalIgnoreCase);
                if (fullLine)
                {
                    table.Cell().ColumnSpan(4).Element(CellBody).Column(col =>
                    {
                        var elementId = row.GetString("DIA_ELE_MST_ID");
                        var prefix = elementId == "2074" ? "SARS-COV-2 (COVID-19) " : string.Empty;
                        var nameStyle = BoldElementIds.Contains(elementId)
                            ? ReportTheme.TableCell.Bold()
                            : ReportTheme.TableCell;

                        col.Item().Text(prefix + row.GetString("DIA_ELE_MST_NM")).Style(nameStyle);
                        var remarks = row.GetString("REMARKS");
                        if (!string.IsNullOrWhiteSpace(remarks))
                            col.Item().Text(remarks).Style(ReportTheme.TableCell);
                    });
                    continue;
                }

                var resultValue = row.GetString("RESULT_VALUE");
                var previous = row.GetString("PREVIOUSRESULT");

                table.Cell().Element(CellBody).Text(row.GetString("DIA_ELE_MST_NM")).Style(ReportTheme.TableCell);
                table.Cell().Element(CellBody).Column(col =>
                {
                    var remarks = row.GetString("REMARKS");
                    if (!string.IsNullOrWhiteSpace(remarks))
                        col.Item().Text(remarks).Style(ReportTheme.TableCell);
                    col.Item().Text(resultValue).Style(ReportTheme.TableCell);
                });
                table.Cell().Element(CellBody).AlignCenter().Text(row.GetString("DEFAULT_VALUES")).Style(ReportTheme.TableCell);
                table.Cell().Element(CellBody).Text(previous).Style(ReportTheme.TableCell);
            }
        });
    }

    private static void ComposeTravelInfo(IContainer container, DataRow? patientRow)
    {
        if (patientRow == null || string.IsNullOrWhiteSpace(patientRow.GetString("PASSPORT")))
            return;

        container.Text(string.Empty);
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

    private static IContainer CellHeader(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(ReportTheme.LightGrey).PaddingVertical(3).PaddingHorizontal(2);

    private static IContainer CellBody(IContainer container) =>
        container.BorderBottom(0.25f).BorderColor(ReportTheme.LightGrey).PaddingVertical(2).PaddingHorizontal(2);
}
