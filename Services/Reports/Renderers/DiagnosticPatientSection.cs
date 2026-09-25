using System.Data;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Layout;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HospitalMobileAPPApi.Services.Reports.Renderers;

internal static class DiagnosticPatientSection
{
    public static void ComposeLabStyle(IContainer container, DataTable patientData)
    {
        var row = patientData.FirstRow();
        container.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("NAME").Style(ReportTheme.Label);
                    c.Item().Text("ADDRESS").Style(ReportTheme.Label);
                });
                r.RelativeItem(2).Column(c =>
                {
                    c.Item().Text($":   {row.GetString("PATIENT_NAME")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("ADDRESS")}").Style(ReportTheme.Value);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("MR. NO").Style(ReportTheme.Label);
                    c.Item().Text("LAB ID").Style(ReportTheme.Label);
                    c.Item().Text("REF BY").Style(ReportTheme.Label);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text($":   {row.GetString("MR_NO")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("PAT_DIAG_ID")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("ADVISEDBY")}").Style(ReportTheme.Value);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("SEX").Style(ReportTheme.Label);
                    c.Item().Text("DOB").Style(ReportTheme.Label);
                    c.Item().Text("AGE").Style(ReportTheme.Label);
                    c.Item().Text("CONTACT").Style(ReportTheme.Label);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text($":   {row.GetString("GENDER")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetDateTime("DT_DOB").FormatDate()}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("ACT_AGE", row.ComputeAgeYears())}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("CONTACT_NO")}").Style(ReportTheme.Value);
                });
            });
        });
    }

    public static void ComposeRadGastStyle(IContainer container, DataTable patientData, bool useDiagnosticNameTitle = false)
    {
        var row = patientData.FirstRow();
        container.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("NAME").Style(ReportTheme.Label);
                    c.Item().Text("ADDRESS").Style(ReportTheme.Label);
                });
                r.RelativeItem(2).Column(c =>
                {
                    c.Item().Text($":   {row.GetString("PATIENT_NAME")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("ADDRESS")}").Style(ReportTheme.Value);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("MR. NO").Style(ReportTheme.Label);
                    c.Item().Text(useDiagnosticNameTitle ? "STUDY ID" : "LAB ID").Style(ReportTheme.Label);
                    c.Item().Text("REF BY").Style(ReportTheme.Label);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text($":   {row.GetString("MR_NO")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("PAT_DIAG_ID")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("ADVISEDBY")}").Style(ReportTheme.Value);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("SEX").Style(ReportTheme.Label);
                    c.Item().Text("AGE").Style(ReportTheme.Label);
                    c.Item().Text("CONTACT").Style(ReportTheme.Label);
                });
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text($":   {row.GetString("GENDER")}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.ComputeAgeYears()}").Style(ReportTheme.Value);
                    c.Item().Text($":   {row.GetString("CONTACT_NO")}").Style(ReportTheme.Value);
                });
            });
        });
    }
}
