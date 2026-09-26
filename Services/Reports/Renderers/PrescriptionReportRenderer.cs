using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using System.Data;
using HospitalMobileAPPApi.Services.Reports.Layout;
using HospitalMobileAPPApi.Services.Reports.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Renderers;

public sealed class PrescriptionReportRenderer : IReportPdfRenderer
{
    public string TemplateName => ReportTemplateNames.Prescription;

    public byte[] Render(ReportDocumentContext context)
    {
        var data = ReportDatasetResolver.ResolvePrimary(context.DataSets);
        var branding = context.Branding;
        var first = data.FirstRow();

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
                    col.Item().PaddingTop(8).AlignCenter().Text("PATIENT PRESCRIPTION").Style(ReportTheme.Title);

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("MR NO").Style(ReportTheme.PrescriptionLabel);
                            c.Item().Text("PATIENT NAME").Style(ReportTheme.PrescriptionLabel);
                            c.Item().Text("SEX").Style(ReportTheme.PrescriptionLabel);
                            c.Item().Text("VISIT DATE").Style(ReportTheme.PrescriptionLabel);
                            c.Item().Text("DOCTOR").Style(ReportTheme.PrescriptionLabel);
                        });
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().Text(data.GetFirstString("MR_NO")).Style(ReportTheme.PrescriptionValue);
                            c.Item().Text(data.GetFirstString("PATIENTNAME")).Style(ReportTheme.PrescriptionValue);
                            c.Item().Text(data.GetFirstString("GENDER")).Style(ReportTheme.PrescriptionValue);
                            c.Item().Text(first.GetDateTime("VISIT_DATE").FormatDateTime("dd-MMM-yyyy")).Style(ReportTheme.PrescriptionValue);
                            c.Item().Text(data.GetFirstString("DOCTOR")).Style(ReportTheme.PrescriptionValue);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("PRESCRIPTION ID").Style(ReportTheme.PrescriptionLabel);
                            c.Item().Text("PRINTED ON").Style(ReportTheme.PrescriptionLabel);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(data.GetFirstString("PATIENT_VISIT_ID")).Style(ReportTheme.PrescriptionValue);
                            c.Item().Text(DateTime.Now.ToString("dd-MMM-yyyy h:mm:ss tt")).Style(ReportTheme.PrescriptionValue);
                        });
                    });

                    col.Item().PaddingTop(8).Element(c => ComposeMedicineTable(c, data));

                    var nextVisit = data.GetFirstString("NEXTVISIT");
                    if (!string.IsNullOrWhiteSpace(nextVisit))
                    {
                        col.Item().PaddingTop(10).Text($"Next Visit: {nextVisit}").Style(ReportTheme.PrescriptionValue);
                    }

                    col.Item().PaddingTop(12).Element(c => c.ComposePrintAuditFooter(branding));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeMedicineTable(IContainer container, DataTable data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(35);
                columns.RelativeColumn(3.4f);
                columns.RelativeColumn(1f);
                columns.ConstantColumn(45);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(0.8f);
                columns.ConstantColumn(40);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellHeader).Text("S NO").Style(ReportTheme.PrescriptionLabel);
                header.Cell().Element(CellHeader).Text("MEDICINE").Style(ReportTheme.PrescriptionLabel);
                header.Cell().Element(CellHeader).AlignCenter().Text("ROUTE").Style(ReportTheme.PrescriptionLabel);
                header.Cell().Element(CellHeader).AlignCenter().Text("DOSE").Style(ReportTheme.PrescriptionLabel);
                header.Cell().Element(CellHeader).AlignCenter().Text("FREQUENCY").Style(ReportTheme.PrescriptionLabel);
                header.Cell().Element(CellHeader).AlignCenter().Text("WHEN").Style(ReportTheme.PrescriptionLabel);
                header.Cell().Element(CellHeader).AlignCenter().Text("DAYS").Style(ReportTheme.PrescriptionLabel);
            });

            var index = 1;
            foreach (DataRow row in data.Rows)
            {
                table.Cell().Element(CellBody).Text(index.ToString()).Style(ReportTheme.PrescriptionValue);
                table.Cell().Element(CellBody).Column(col =>
                {
                    col.Item().Text(row.GetString("MEDICINE")).Style(ReportTheme.PrescriptionValue);
                    var remarks = row.GetString("REMARKS");
                    if (!string.IsNullOrWhiteSpace(remarks))
                        col.Item().Text($"Note: {remarks}").Style(ReportTheme.PrescriptionValue.FontSize(8));
                });
                table.Cell().Element(CellBody).AlignCenter().Column(col =>
                {
                    col.Item().Text(row.GetString("ROUTE_NAME")).Style(ReportTheme.PrescriptionValue);
                    var routeUrd = row.GetString("ROUTE_URDU");
                    if (!string.IsNullOrWhiteSpace(routeUrd))
                        col.Item().Text(routeUrd).Style(ReportTheme.PrescriptionValue.FontSize(8));
                });
                table.Cell().Element(CellBody).AlignCenter().Text(row.GetString("DOSE")).Style(ReportTheme.PrescriptionValue);
                table.Cell().Element(CellBody).AlignCenter().Column(col =>
                {
                    col.Item().Text(row.GetString("FREQ_DEFINITION")).Style(ReportTheme.PrescriptionValue);
                    var freqUrd = row.GetString("FREQ_URDU");
                    if (!string.IsNullOrWhiteSpace(freqUrd))
                        col.Item().Text(freqUrd).Style(ReportTheme.PrescriptionValue.FontSize(8));
                });
                table.Cell().Element(CellBody).AlignCenter().Column(col =>
                {
                    col.Item().Text(row.GetString("DOSE_WHEN")).Style(ReportTheme.PrescriptionValue);
                    var whenUrd = row.GetString("DOSE_WHEN_URDU");
                    if (!string.IsNullOrWhiteSpace(whenUrd))
                        col.Item().Text(whenUrd).Style(ReportTheme.PrescriptionValue.FontSize(8));
                });
                table.Cell().Element(CellBody).AlignCenter().Text(row.GetString("DAYS")).Style(ReportTheme.PrescriptionValue);
                index++;
            }
        });
    }

    private static IContainer CellHeader(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(ReportTheme.LightGrey).PaddingVertical(3).PaddingHorizontal(2);

    private static IContainer CellBody(IContainer container) =>
        container.BorderBottom(0.25f).BorderColor(ReportTheme.LightGrey).PaddingVertical(3).PaddingHorizontal(2);
}
