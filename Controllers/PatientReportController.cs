using System.Data;
using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using HospitalMobileAPPApi.Services.Reports;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Controllers;

/// <summary>PDF report generation and billing history.</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
[Tags("PatientReport")]
public class PatientReportController : ControllerBase
{
    private readonly SecuritySettings _securitySettings;
    private readonly IBillingService _billingService;
    private readonly IReportDataService _reportDataService;
    private readonly IReportPdfService _reportPdfService;
    private readonly IReportBrandingProvider _brandingProvider;
    private readonly IConfiguration _configuration;

    public PatientReportController(
        IConfiguration configuration,
        IOptions<SecuritySettings> securitySettings,
        IBillingService billingService,
        IReportDataService reportDataService,
        IReportPdfService reportPdfService,
        IReportBrandingProvider brandingProvider)
    {
        _configuration = configuration;
        _securitySettings = securitySettings.Value;
        _billingService = billingService;
        _reportDataService = reportDataService;
        _reportPdfService = reportPdfService;
        _brandingProvider = brandingProvider;
    }

    [HttpGet("GenerateReport")]
    public IActionResult GenerateReport(long rptId, string reportName, string parameters, string user = "MobileApp")
    {
        if (string.IsNullOrWhiteSpace(reportName) || string.IsNullOrWhiteSpace(parameters))
            return BadRequest(new { message = "Report name and parameters are required" });

        try
        {
            var reportConfig = _reportDataService.GetReportConfiguration(rptId);
            if (reportConfig.Rows.Count == 0)
                return NotFound("Report configuration not found.");

            var templateName = ResolveSupportedTemplateName(reportConfig, reportName);
            if (templateName is null)
                return BadRequest(new { message = $"Report template '{reportName}' is not supported by the native PDF engine." });

            var dataSets = _reportDataService.LoadReportDataSets(rptId, parameters);
            if (dataSets.Count == 0)
                return NotFound("Report configuration not found.");

            if (dataSets.All(ds => ds.Rows.Count == 0))
            {
                return NotFound(new
                {
                    message = "No report data found for the supplied parameter.",
                    rptId,
                    reportName,
                    parameters,
                });
            }

            var patientName = ReportDatasetResolver.ExtractPatientName(dataSets)
                ?? _reportDataService.GetPatientName(parameters, templateName);
            var branding = _brandingProvider.Create(
                user,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                reportName,
                rptId,
                parameters,
                patientName);

            var pdfBytes = _reportPdfService.Render(new ReportDocumentContext
            {
                TemplateName = templateName,
                ReportTitle = reportName,
                Branding = branding,
                DataSets = dataSets,
            });

            return File(pdfBytes, "application/pdf", $"{reportName}_{parameters}.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500,
                "ERROR DETAILS:\n" +
                ex.Message + "\n\n" +
                ex.InnerException?.Message + "\n\n" +
                ex.StackTrace);
        }
    }

    [HttpGet("prescription/{patientVisitId:int}")]
    public IActionResult GeneratePrescriptionReport(int patientVisitId)
    {
        return GenerateReport(141, ReportTemplateNames.Prescription, patientVisitId.ToString());
    }

    [HttpGet("history/{mrNo}")]
    [Obsolete("Use GET /api/Billing/history/{mrNo}. This endpoint remains for backward compatibility.")]
    public async Task<IActionResult> GetBillingHistory(string mrNo)
    {
        if (string.IsNullOrWhiteSpace(mrNo))
            return BadRequest(new { message = "MR number is required" });

        try
        {
            var history = await _billingService.GetHistoryAsync(mrNo);
            var result = history.Items.Select(item => new BillingHistoryModel
            {
                BillId = item.BillId,
                MrNo = item.MrNo,
                InvoiceNo = item.InvoiceNo,
                Department = item.DepartmentCode ?? item.Department,
                VisitDate = item.VisitDate,
                PaymentDate = item.PaymentDate,
                PaymentMethod = item.PaymentMethod,
                Amount = item.Amount,
                IsCancel = item.IsCancel,
                CancelReason = item.CancelReason,
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error retrieving billing history",
                error = ex.Message
            });
        }
    }

    [HttpGet("GenerateReports")]
    public IActionResult GenerateBillingReport(int rptId, string param, int empId = 0, DateTime? d1 = null, DateTime? d2 = null)
    {
        if (string.IsNullOrWhiteSpace(param))
            return BadRequest(new { message = "Report parameter is required" });

        if (empId <= 0)
            empId = _securitySettings.MobileAppEmpId;

        try
        {
            return RenderConfiguredReport(
                rptId,
                param,
                empId,
                d1,
                d2,
                ReportTemplateNames.BillingReceipt);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("DischargeReport")]
    public IActionResult GenerateDischargeReport(int rptId, string param, int empId = 0, DateTime? d1 = null, DateTime? d2 = null)
    {
        if (string.IsNullOrWhiteSpace(param))
            return BadRequest(new { message = "Patient visit id is required" });

        if (empId <= 0)
            empId = _securitySettings.MobileAppEmpId;

        try
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            using var con = new OracleConnection(connStr);
            con.Open();

            var dischargeId = _reportDataService.ResolveDischargeId(con, param);
            if (string.IsNullOrWhiteSpace(dischargeId))
            {
                return NotFound(new
                {
                    message = "No active discharge record found for this visit.",
                    patientVisitId = param,
                });
            }

            return RenderConfiguredReport(
                rptId,
                dischargeId,
                empId,
                d1,
                d2,
                ReportTemplateNames.DischargeCertificate);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    private IActionResult RenderConfiguredReport(
        int rptId,
        string param,
        int empId,
        DateTime? d1,
        DateTime? d2,
        string fallbackTemplate)
    {
        var connStr = _configuration.GetConnectionString("HMISConnection");
        using var con = new OracleConnection(connStr);
        con.Open();

        var user = _reportDataService.GetEmployeeName(con, empId) ?? "MobileApp";
        var config = _reportDataService.GetReportConfiguration(rptId);
        if (config.Rows.Count == 0)
            return NotFound("Report configuration not found.");

        var rptName = config.Rows[0]["RPT_NAME"]?.ToString() ?? fallbackTemplate;
        var templateName = ReportTemplateNames.Normalize(
            _reportDataService.ResolveTemplateName(config, fallbackTemplate))
            ?? fallbackTemplate;

        if (!ReportTemplateNames.IsSupported(templateName))
            return BadRequest(new { message = $"Report template '{templateName}' is not supported by the native PDF engine." });

        var dataSets = _reportDataService.LoadConfiguredDataSets(con, config, param, d1, d2);
        var branding = _brandingProvider.Create(
            user,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            rptName,
            rptId,
            param);

        var pdf = _reportPdfService.Render(new ReportDocumentContext
        {
            TemplateName = templateName,
            ReportTitle = rptName,
            Branding = branding,
            DataSets = dataSets,
        });

        return File(pdf, "application/pdf", $"{rptName}.pdf");
    }

    private string? ResolveSupportedTemplateName(DataTable reportConfig, string reportName)
    {
        var candidates = new List<string?>
        {
            ReportTemplateNames.Normalize(reportName),
            ReportTemplateNames.Normalize(_reportDataService.ResolveTemplateName(reportConfig)),
            ReportTemplateNames.Normalize(reportConfig.Rows[0]["RPT_NAME"]?.ToString()),
        };

        foreach (var candidate in candidates)
        {
            if (ReportTemplateNames.IsSupported(candidate))
                return candidate;
        }

        return null;
    }
}
