using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Reporting.NETCore;
using QRCoder;
using Dapper;

namespace HospitalMobileAPPApi.Controllers
{
    /// <summary>PDF report generation and billing history.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [Tags("PatientReport")]
    public class PatientReportController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly SecuritySettings _securitySettings;
        private readonly IBillingService _billingService;

        public PatientReportController(
            IConfiguration configuration,
            IWebHostEnvironment env,
            IOptions<SecuritySettings> securitySettings,
            IBillingService billingService)
        {
            _configuration = configuration;
            _env = env;
            _securitySettings = securitySettings.Value;
            _billingService = billingService;
        }

        [HttpGet("GenerateReport")]
        public IActionResult GenerateReport(long rptId, string reportName, string parameters, string user = "MobileApp")
        {
            if (string.IsNullOrWhiteSpace(reportName) || string.IsNullOrWhiteSpace(parameters))
            {
                return BadRequest(new { message = "Report name and parameters are required" });
            }

            try
            {
                var reportConfig = GetReportConfiguration(rptId);

                if (reportConfig.Rows.Count == 0)
                    return NotFound("Report configuration not found.");

                var report = new LocalReport();

                string reportPath = Path.Combine(_env.ContentRootPath, "Reports", $"{reportName}.rdlc");

                if (!System.IO.File.Exists(reportPath))
                    return NotFound($"RDLC file not found: {reportName}.rdlc");

                report.ReportPath = reportPath;
                report.EnableExternalImages = true;

                report.DataSources.Clear();

                int datasetCounter = 0;

                foreach (DataRow row in reportConfig.Rows)
                {
                    datasetCounter++;

                    string query = row["RPT_QUERY"]?.ToString();
                    int paramCount = Convert.ToInt32(row["PARAM"]);
                    int datePickerType = Convert.ToInt32(row["DATE_PICKER"] ?? 0);

                    DataTable dt = ExecuteQuerySafe(query, parameters);

                    if (dt == null)
                        return StatusCode(500, $"Dataset {datasetCounter} returned NULL");

                    report.DataSources.Add(
                        new ReportDataSource($"DataSet{datasetCounter}", dt)
                    );
                }

                var reportParams = GetCommonParameters(user, reportName, parameters, rptId);
                report.SetParameters(reportParams);


                Warning[] warnings;
                string[] streamIds;
                string mimeType;
                string encoding;
                string extension;

                byte[] pdfBytes = report.Render(
                    "PDF",
                    null,
                    out mimeType,
                    out encoding,
                    out extension,
                    out streamIds,
                    out warnings
                );

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

        // ---------------- SAFE QUERY EXECUTION ----------------

        private DataTable ExecuteQuerySafe(string query, string patDiagId)
        {
            DataTable dt = new DataTable();
            string connStr = _configuration.GetConnectionString("HMISConnection");

            using (OracleConnection con = new OracleConnection(connStr))
            {
                using (OracleCommand cmd = new OracleCommand())
                {
                    cmd.Connection = con;

                    // If query contains :id parameter
                    if (query.Contains(":id"))
                    {
                        cmd.CommandText = query;
                        cmd.Parameters.Add(new OracleParameter("id", patDiagId));
                    }
                    else if (query.Contains("{0}"))
                    {
                        // fallback only if needed
                        cmd.CommandText = string.Format(query, patDiagId);
                    }
                    else
                    {
                        cmd.CommandText = query;
                    }

                    con.Open();

                    using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                    {
                        da.Fill(dt);
                    }
                }
            }

            return dt;
        }

        // ---------------- REPORT CONFIG ----------------

        private DataTable GetReportConfiguration(long rptId)
        {
            DataTable dt = new DataTable();
            string connStr = _configuration.GetConnectionString("HMISConnection");

            string query = @"SELECT RM.*, RD.RPT_QUERY
                         FROM REPORT_MST RM 
                           LEFT JOIN REPORT_DATASET RD ON RD.RPT_ID = RM.RPT_ID 
                          WHERE RM.RPT_ID = :rptId AND RD.IS_ACTIVE = 'Y' 
                          ORDER BY RD.RPT_DT_ID ASC";

            using (OracleConnection con = new OracleConnection(connStr))
            using (OracleCommand cmd = new OracleCommand(query, con))
            {
                cmd.Parameters.Add(new OracleParameter("rptId", rptId));
                con.Open();

                using (OracleDataAdapter da = new OracleDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
            }

            return dt;
        }

        // ---------------- PARAMETERS ----------------

        private List<ReportParameter> GetCommonParameters(string user, string reportName, string patDiagId, long rptId)
        {
            var reportParams = new List<ReportParameter>();

            string logoPath = Path.Combine(_env.ContentRootPath, "Images", "Logo.jpg");
            string bgPath = Path.Combine(_env.ContentRootPath, "Images", "BG.jpg");

            reportParams.Add(new ReportParameter("HOSPITAL_ENG", "BAHRIA TOWN INTERNATIONAL HOSPITAL"));
            reportParams.Add(new ReportParameter("BRANCH_ENG", "KARACHI BRANCH"));
            reportParams.Add(new ReportParameter("ADDRESS_ENG", "Precinct 19, Phase 1, Bahria Town Karachi"));
            reportParams.Add(new ReportParameter("PHONE_NO", "111 111 284"));
            reportParams.Add(new ReportParameter("HOSPITAL_URD", "بحریہ ٹاؤن انٹرنیشنل ہسپتال"));
            reportParams.Add(new ReportParameter("BRANCH_URD", "کراچی برانچ"));
            reportParams.Add(new ReportParameter("ADDRESS_URD", "پریسنٹ ۱۹ ، فیز ۱، بحریہ ٹاوُن کراچی"));
            reportParams.Add(new ReportParameter("PRINTEDBY", user));
            reportParams.Add(new ReportParameter("PRINTEDVIA", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"));
            reportParams.Add(new ReportParameter("Logo", Convert.ToBase64String(System.IO.File.ReadAllBytes(logoPath))));
            reportParams.Add(new ReportParameter("RptBG", Convert.ToBase64String(System.IO.File.ReadAllBytes(bgPath))));

            if (reportName == "Labrpt" || reportName == "RadRpt" || reportName == "GastRpt")
            {
                string patientName = GetPatientName(patDiagId);
                string qrData =
                    $"http://btkhospital.com/OnlineReports/Reports/ReportView.aspx?lRptNo={rptId}&lRptNm={reportName}&lParam={patDiagId}&PTNM={patientName}";

                reportParams.Add(new ReportParameter("IS_QR", "Y"));
                reportParams.Add(new ReportParameter("QR", GenerateQRCodeBase64(qrData)));
            }

            return reportParams;
        }

        private string GetPatientName(string patDiagId)
        {
            string connStr = _configuration.GetConnectionString("HMISConnection");
            string query = "SELECT FIRST_NAME FROM V_LABORATORY_GEN WHERE PAT_DIAG_ID = :id";

            using (OracleConnection con = new OracleConnection(connStr))
            using (OracleCommand cmd = new OracleCommand(query, con))
            {
                cmd.Parameters.Add(new OracleParameter("id", patDiagId));
                con.Open();
                return cmd.ExecuteScalar()?.ToString() ?? "";
            }
        }

        private string GenerateQRCodeBase64(string code)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                return Convert.ToBase64String(qrCode.GetGraphic(20));
            }
        }

        [HttpGet("prescription/{patientVisitId:int}")]
        public IActionResult GeneratePrescriptionReport(int patientVisitId)
        {
            return GenerateReport(141, "PRESCRIPTION_A4", patientVisitId.ToString());
        }

        [HttpGet("history/{mrNo}")]
        [Obsolete("Use GET /api/Billing/history/{mrNo}. This endpoint remains for backward compatibility.")]
        public async Task<IActionResult> GetBillingHistory(string mrNo)
        {
            if (string.IsNullOrWhiteSpace(mrNo))
            {
                return BadRequest(new { message = "MR number is required" });
            }

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
            {
                return BadRequest(new { message = "Report parameter is required" });
            }

            if (empId <= 0)
            {
                empId = _securitySettings.MobileAppEmpId;
            }

            try
            {
                string connStr = _configuration.GetConnectionString("HMISConnection");

                var report = new LocalReport();
                //var reportParams = new List<ReportParameter>();

                using var con = new OracleConnection(connStr);
                con.Open();

                //----------------------------------
                // Get Logged User
                //----------------------------------

                string userQuery =
                    "SELECT FIRST_NAME||' '||LAST_NAME FROM EMPLOYEE_MST WHERE EMPLOYEE_ID=:id";

                string user;

                using (OracleCommand cmd = new OracleCommand(userQuery, con))
                {
                    cmd.Parameters.Add("id", empId);
                    user = cmd.ExecuteScalar()?.ToString();
                }


                //----------------------------------
                // Load Report Configuration
                //----------------------------------

                string datasetQuery =
                @"SELECT RM.*,RD.RPT_QUERY
                  FROM REPORT_MST RM
                  LEFT JOIN REPORT_DATASET RD
                  ON RD.RPT_ID=RM.RPT_ID
                  WHERE RM.RPT_ID=:id
                  AND RD.IS_ACTIVE='Y'
                  ORDER BY RD.RPT_DT_ID";

                DataTable config = GetDataTable(con, datasetQuery, rptId.ToString());

                if (config.Rows.Count == 0)
                    return NotFound("Report configuration not found.");

                //----------------------------------
                // Get Report Name
                //----------------------------------

                string rptName = config.Rows[0]["RPT_NAME"]?.ToString();
                var reportParams = GetCommonParameters(user ?? "MobileApp", rptName, param, rptId);

                //----------------------------------
                // QR / Barcode Logic
                //----------------------------------

                if (rptName == "PRINT_CARD")
                {
                    reportParams.Add(new ReportParameter("QR", GenerateQRCode(param)));
                    reportParams.Add(new ReportParameter("BC", GenerateBarcodes(param, true)));
                }

                else if (rptName == "PRINT_TOKEN")
                {
                    string mr = GetString(con,
                        "SELECT MR_NO FROM PATIENT_VISIT WHERE PATIENT_VISIT_ID=:id",
                        param);

                    reportParams.Add(new ReportParameter("QR", GenerateQRCode(param)));
                    reportParams.Add(new ReportParameter("BC", GenerateBarcodes(mr, true)));
                }

                else if (rptName == "PRINT IPD LABELS")
                {
                    string mr = GetString(con,
                        "SELECT MR_NO FROM PATIENT_VISIT WHERE PATIENT_VISIT_ID=:id",
                        param);

                    reportParams.Add(new ReportParameter("QR", GenerateQRCode(param)));
                    reportParams.Add(new ReportParameter("BC", GenerateBarcodes(mr, false)));
                }

                else if (rptName == "PRINT LAB LABELS BARCODE")
                {
                    string lblBC = GetString(con,
                        "select MR_NO||'/'||PAT_DIAG_ID lbl from V_LABORATORY_GEN where PAT_DIAG_ID=:id group by MR_NO,PAT_DIAG_ID",
                        param);

                    reportParams.Add(new ReportParameter("BC", GenerateQRCode(lblBC)));
                }

                //----------------------------------
                // Load Dataset Queries
                //----------------------------------

                int i = 0;

                foreach (DataRow row in config.Rows)
                {
                    i++;

                    int prm = Convert.ToInt32(row["PARAM"]);
                    int dtp = Convert.ToInt32(row["DATE_PICKER"]);

                    string query = row["RPT_QUERY"].ToString();

                    if (dtp == 1 && prm == 0)
                        query = string.Format(query, d1?.ToString("dd-MMM-yyyy"));

                    else if (dtp == 1 && prm != 0)
                        query = string.Format(query,
                                d1?.ToString("dd-MMM-yyyy"), param);

                    else if (dtp == 2 && prm == 0)
                        query = string.Format(query,
                                d1?.ToString("dd-MMM-yyyy"),
                                d2?.ToString("dd-MMM-yyyy"));

                    else if (dtp == 2 && prm != 0)
                        query = string.Format(query,
                                d1?.ToString("dd-MMM-yyyy"),
                                d2?.ToString("dd-MMM-yyyy"),
                                param);

                    else if (dtp == 0 && prm != 0)
                        query = string.Format(query, param);

                    DataTable dt = GetDataTable(con, query);

                    report.DataSources.Add(
                        new ReportDataSource($"DataSet{i}", dt));
                }

                //----------------------------------
                // Load RDLC Path from DB
                //----------------------------------

                string rdlcFile = Path.GetFileName(config.Rows[0]["RPT_LOCATION"].ToString());

                string reportPath = Path.Combine(
                    _env.ContentRootPath,
                    "Reports",
                    rdlcFile
                );

                if (!System.IO.File.Exists(reportPath))
                    return NotFound("RDLC not found: " + reportPath);

                report.ReportPath = reportPath;

                //----------------------------------
                // Set Parameters
                //----------------------------------

                report.SetParameters(reportParams);

                //----------------------------------
                // Render PDF
                //----------------------------------

                byte[] pdf = report.Render("PDF");

                return File(pdf, "application/pdf", $"{rptName}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        //----------------------------------
        // Helper Methods
        //----------------------------------

        [HttpGet("DischargeReport")]
        public IActionResult GenerateDischargeReport(int rptId, string param, int empId = 0, DateTime? d1 = null, DateTime? d2 = null)
        {
            if (string.IsNullOrWhiteSpace(param))
            {
                return BadRequest(new { message = "Patient visit id is required" });
            }

            if (empId <= 0)
            {
                empId = _securitySettings.MobileAppEmpId;
            }

            try
            {
                string connStr = _configuration.GetConnectionString("HMISConnection");

                var report = new LocalReport();

                using var con = new OracleConnection(connStr);
                con.Open();

                //----------------------------------
                // Get Logged User
                //----------------------------------
                string dsId = "SELECT DISCHARGE_ID FROM PATIENT_DISCHARGE where PATIENT_VISIT_ID = :pvid and IS_CANCEL='N' ";
                string data;
                using (OracleCommand cmd = new OracleCommand(dsId, con))
                {
                    cmd.Parameters.Add("pvid", param);
                    data = cmd.ExecuteScalar()?.ToString();
                }


                string userQuery = "SELECT FIRST_NAME||' '||LAST_NAME FROM EMPLOYEE_MST WHERE EMPLOYEE_ID=:id";
                string user;
                using (OracleCommand cmd = new OracleCommand(userQuery, con))
                {
                    cmd.Parameters.Add("id", empId);
                    user = cmd.ExecuteScalar()?.ToString();
                }

                //----------------------------------
                // Load Report Configuration
                //----------------------------------
                string datasetQuery = @"SELECT RM.*, RD.RPT_QUERY 
                              FROM REPORT_MST RM 
                              LEFT JOIN REPORT_DATASET RD ON RD.RPT_ID = RM.RPT_ID 
                              WHERE RM.RPT_ID = :id AND RD.IS_ACTIVE = 'Y' 
                              ORDER BY RD.RPT_DT_ID";

                DataTable config = GetDataTable(con, datasetQuery, rptId.ToString());

                if (config.Rows.Count == 0)
                    return NotFound("Report configuration not found.");

                //----------------------------------
                // Get Report Name
                //----------------------------------
                string rptName = config.Rows[0]["RPT_NAME"]?.ToString();
                var reportParams = GetCommonParameters(user ?? "MobileApp", rptName, data, rptId);


                    //----------------------------------
                    // QR / Barcode Logic for other reports
                    //----------------------------------
                    if (rptName == "PRINT_CARD")
                    {
                        reportParams.Add(new ReportParameter("QR", GenerateQRCode(data)));
                        reportParams.Add(new ReportParameter("BC", GenerateBarcodes(data, true)));
                    }
                    else if (rptName == "PRINT_TOKEN")
                    {
                        string mr = GetString(con, "SELECT MR_NO FROM PATIENT_VISIT WHERE PATIENT_VISIT_ID=:id", data);
                        reportParams.Add(new ReportParameter("QR", GenerateQRCode(data)));
                        reportParams.Add(new ReportParameter("BC", GenerateBarcodes(mr, true)));
                    }
                    else if (rptName == "PRINT IPD LABELS")
                    {
                        string mr = GetString(con, "SELECT MR_NO FROM PATIENT_VISIT WHERE PATIENT_VISIT_ID=:id", data);
                        reportParams.Add(new ReportParameter("QR", GenerateQRCode(data)));
                        reportParams.Add(new ReportParameter("BC", GenerateBarcodes(mr, false)));
                    }
                    else if (rptName == "PRINT LAB LABELS BARCODE")
                    {
                        string lblBC = GetString(con, "select MR_NO||'/'||PAT_DIAG_ID lbl from V_LABORATORY_GEN where PAT_DIAG_ID=:id group by MR_NO,PAT_DIAG_ID", data);
                        reportParams.Add(new ReportParameter("BC", GenerateQRCode(lblBC)));
                    }
                

                //----------------------------------
                // Load Dataset Queries (same as Windows Forms)
                //----------------------------------
                int i = 0;
                foreach (DataRow row in config.Rows)
                {
                    i++;
                    int prm = Convert.ToInt32(row["PARAM"]);
                    int dtp = Convert.ToInt32(row["DATE_PICKER"]);
                    string query = row["RPT_QUERY"].ToString();

                    // Format query based on parameters (matching Windows Forms logic)
                    if (dtp == 1 && prm == 0)
                        query = string.Format(query, d1?.ToString("dd-MMM-yyyy"));
                    else if (dtp == 1 && prm != 0)
                        query = string.Format(query, d1?.ToString("dd-MMM-yyyy"), data);
                    else if (dtp == 2 && prm == 0)
                        query = string.Format(query, d1?.ToString("dd-MMM-yyyy"), d2?.ToString("dd-MMM-yyyy"));
                    else if (dtp == 2 && prm != 0)
                        query = string.Format(query, d1?.ToString("dd-MMM-yyyy"), d2?.ToString("dd-MMM-yyyy"), data);
                    else if (dtp == 0 && prm != 0)
                        query = string.Format(query, data);

                    DataTable dt = GetDataTable(con, query);
                    report.DataSources.Add(new ReportDataSource($"DataSet{i}", dt));
                }

                //----------------------------------
                // Load RDLC Path from DB
                //----------------------------------
                string rdlcFile = Path.GetFileName(config.Rows[0]["RPT_LOCATION"].ToString());
                string reportPath = Path.Combine(_env.ContentRootPath, "Reports", rdlcFile);

                if (!System.IO.File.Exists(reportPath))
                    return NotFound("RDLC not found: " + reportPath);

                report.ReportPath = reportPath;

                //----------------------------------
                // Set Parameters
                //----------------------------------
                report.SetParameters(reportParams);

                //----------------------------------
                // Render PDF
                //----------------------------------
                byte[] pdf = report.Render("PDF");
                return File(pdf, "application/pdf", $"{rptName}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        private string GetString(OracleConnection con, string query, string param)
        {
            using OracleCommand cmd = new OracleCommand(query, con);
            cmd.Parameters.Add("id", param);
            return cmd.ExecuteScalar()?.ToString();
        }

        private DataTable GetDataTable(OracleConnection con, string query, string param = null)
        {
            using OracleCommand cmd = new OracleCommand(query, con);

            if (param != null)
                cmd.Parameters.Add("id", param);

            using OracleDataAdapter da = new OracleDataAdapter(cmd);

            DataTable dt = new DataTable();
            da.Fill(dt);

            return dt;
        }

        //----------------------------------
        // QR Generator
        //----------------------------------

        private string GenerateQRCode(string text)
        {
            QRCodeGenerator qr = new QRCodeGenerator();
            QRCodeData data = qr.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);

            PngByteQRCode code = new PngByteQRCode(data);

            return Convert.ToBase64String(code.GetGraphic(20));
        }

        //----------------------------------
        // Barcode Generator
        //----------------------------------

        private string GenerateBarcodes(string value, bool rotate)
        {
            //Barcode barcode = new Barcode();
            //Image img = barcode.Encode(TYPE.CODE128, value, Color.Black, Color.White, 560, 280);

            //if (rotate)
            //    img.RotateFlip(RotateFlipType.Rotate90FlipNone);

            //using MemoryStream ms = new MemoryStream();
            //img.Save(ms, ImageFormat.Png);

            //return Convert.ToBase64String(ms.ToArray());
            return "";
        }



    }
}

