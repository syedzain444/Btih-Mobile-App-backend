using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Layout;
using HospitalMobileAPPApi.Services.Reports.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Renderers;

public sealed class DischargeCertificateRenderer : IReportPdfRenderer
{
    public string TemplateName => ReportTemplateNames.DischargeCertificate;

    public byte[] Render(ReportDocumentContext context)
    {
        var patient = context.DataSets.ElementAtOrDefault(0) ?? new System.Data.DataTable();
        var investigations = context.DataSets.ElementAtOrDefault(1);
        var surgeries = context.DataSets.ElementAtOrDefault(2);
        var medicines = context.DataSets.ElementAtOrDefault(3);
        var branding = context.Branding;
        var dcType = patient.GetFirstString("DC_TYPE");
        var title = dcType.Equals("LAMA", StringComparison.OrdinalIgnoreCase)
            ? "SELF-DISCHARGE AGAINST MEDICAL ADVICE FORM"
            : "DISCHARGE CERTIFICATE";

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
                    col.Item().PaddingTop(8).AlignCenter().Text(title).Style(ReportTheme.Title);

                    col.Item().PaddingTop(8).Element(c => ComposePatientInfo(c, patient));
                    col.Item().PaddingTop(8).Element(c => ComposeClinicalSections(c, patient));

                    if (investigations.HasRows())
                        col.Item().PaddingTop(8).Element(c => ComposeInvestigations(c, investigations!));

                    if (surgeries.HasRows())
                        col.Item().PaddingTop(8).Element(c => ComposeSurgeries(c, surgeries!));

                    if (medicines.HasRows())
                        col.Item().PaddingTop(8).Element(c => ComposeMedicines(c, medicines!));

                    col.Item().PaddingTop(12).Element(c => ComposeSignatures(c, patient));
                    col.Item().PaddingTop(8).Element(c => c.ComposePrintAuditFooter(branding));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposePatientInfo(IContainer container, DataTable patient)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("MR NO").Style(ReportTheme.Label);
                row.RelativeItem(2).Text(patient.GetFirstString("MR_NO")).Style(ReportTheme.Value);
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("PATIENT NAME").Style(ReportTheme.Label);
                row.RelativeItem(2).Text(patient.GetFirstString("PATIENTNAME")).Style(ReportTheme.Value);
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("SEX").Style(ReportTheme.Label);
                row.RelativeItem().Text(patient.GetFirstString("GENDER")).Style(ReportTheme.Value);
                row.RelativeItem().Text("AGE").Style(ReportTheme.Label);
                row.RelativeItem().Text(patient.GetFirstString("AGE")).Style(ReportTheme.Value);
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("DOCTOR").Style(ReportTheme.Label);
                row.RelativeItem(2).Text(patient.GetFirstString("DOCTOR")).Style(ReportTheme.Value);
            });
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("DATE OF ADMISSION").Style(ReportTheme.Label);
                row.RelativeItem().Text(patient.GetFirstString("ADMISSION_DATE")).Style(ReportTheme.Value);
                row.RelativeItem().Text("DATE OF DISCHARGE").Style(ReportTheme.Label);
                row.RelativeItem().Text(patient.GetFirstString("DISCHARGE_DATE")).Style(ReportTheme.Value);
            });
        });
    }

    private static void ComposeClinicalSections(IContainer container, DataTable patient)
    {
        container.Column(col =>
        {
            col.Item().Element(c => c.ComposeTextSection("DIAGNOSIS AT ADMISSION", patient.GetFirstString("PROVISIONAL_DIAGNOSIS")));
            col.Item().Element(c => c.ComposeTextSection("DIAGNOSIS AT THE TIME OF DISCHARGE", patient.GetFirstString("DIAGNOSIS_DISCHARGE")));
            col.Item().Element(c => c.ComposeTextSection("PRESNTING COMPLAINTS", patient.GetFirstString("PRESENTING_COMPLAINTS")));
            col.Item().Element(c => c.ComposeTextSection("CLINICAL OBSERVATIONS", patient.GetFirstString("CLINICAL_OBSERVATION")));
            col.Item().Element(c => c.ComposeTextSection("TREATMENT GIVEN IN HOSPITAL", patient.GetFirstString("TREATMENT_HOSPITAL")));
            col.Item().Element(c => c.ComposeTextSection("TREATMENT / INSTRUCTION ON DISCHARGE", patient.GetFirstString("TREATMENT_INSTRUCTION_ON_DC")));
            col.Item().Element(c => c.ComposeTextSection("RELEVANT INVESTIGATION", patient.GetFirstString("RELEVANT_INVESTIGATION")));
            col.Item().Element(c => c.ComposeTextSection("SURGERY DETAILS", patient.GetFirstString("SURGERY_DETAILS")));
            col.Item().Element(c => c.ComposeTextSection("OTHER INFORMATION", patient.GetFirstString("OTHER_INFO")));
        });
    }

    private static void ComposeInvestigations(IContainer container, DataTable investigations)
    {
        container.Column(col =>
        {
            col.Item().Text("RELEVANT INVESTIGATIONS").Style(ReportTheme.SectionTitle);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(40);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(3f);
                });

                table.Header(header =>
                {
                    header.Cell().Text("S. NO.").Style(ReportTheme.Label);
                    header.Cell().Text("DATE").Style(ReportTheme.Label);
                    header.Cell().Text("INVESTIGATION").Style(ReportTheme.Label);
                });

                var index = 1;
                foreach (DataRow row in investigations.Rows)
                {
                    table.Cell().Text(index.ToString()).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetDateTime("CREATED_ON").FormatDate("dd-MMM-yy")).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetString("DIAGNOSTIC_NAME")).Style(ReportTheme.Value);
                    index++;
                }
            });
        });
    }

    private static void ComposeSurgeries(IContainer container, DataTable surgeries)
    {
        container.Column(col =>
        {
            col.Item().Text("SURGERY(S) DETAIL").Style(ReportTheme.SectionTitle);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(40);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(3f);
                });

                table.Header(header =>
                {
                    header.Cell().Text("S. NO.").Style(ReportTheme.Label);
                    header.Cell().Text("DATE").Style(ReportTheme.Label);
                    header.Cell().Text("SURGERY").Style(ReportTheme.Label);
                });

                var index = 1;
                foreach (DataRow row in surgeries.Rows)
                {
                    table.Cell().Text(index.ToString()).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetDateTime("DT_PERFORM").FormatDate("dd-MMM-yy")).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetString("PROCEDURE_NM")).Style(ReportTheme.Value);
                    index++;
                }
            });
        });
    }

    private static void ComposeMedicines(IContainer container, DataTable medicines)
    {
        container.Column(col =>
        {
            col.Item().Text("MEDICINES ON DISCHARGE").Style(ReportTheme.SectionTitle);
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(2f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                    columns.RelativeColumn(1f);
                });

                table.Header(header =>
                {
                    header.Cell().Text("S NO").Style(ReportTheme.Label);
                    header.Cell().Text("MEDICINE").Style(ReportTheme.Label);
                    header.Cell().Text("DOSAGE").Style(ReportTheme.Label);
                    header.Cell().Text("DAYS").Style(ReportTheme.Label);
                    header.Cell().Text("TIME").Style(ReportTheme.Label);
                });

                var index = 1;
                foreach (DataRow row in medicines.Rows)
                {
                    table.Cell().Text(index.ToString()).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetString("MED_NAME")).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetString("DOSAGE")).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetString("DAYS")).Style(ReportTheme.Value);
                    table.Cell().Text(row.GetString("TIME")).Style(ReportTheme.Value);
                    index++;
                }
            });
        });
    }

    private static void ComposeSignatures(IContainer container, DataTable patient)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Discharge Doctor").Style(ReportTheme.Label);
                col.Item().Text(patient.GetFirstString("DIS_DR")).Style(ReportTheme.Value);
            });
            row.RelativeItem().Column(col =>
            {
                col.Item().AlignRight().Text("Nurse").Style(ReportTheme.Label);
                col.Item().AlignRight().Text(patient.GetFirstString("NURSE")).Style(ReportTheme.Value);
            });
        });
    }
}
