using HospitalMobileAPPApi.Services.Reports.Models;
using QRCoder;

namespace HospitalMobileAPPApi.Services.Reports.Helpers;

public interface IReportBrandingProvider
{
    ReportBranding Create(
        string printedBy,
        string printedVia,
        string? reportName = null,
        long? reportId = null,
        string? parameter = null,
        string? patientName = null);
}

public sealed class ReportBrandingProvider : IReportBrandingProvider
{
    private readonly IWebHostEnvironment _env;
    private readonly object _imageLock = new();
    private byte[]? _logo;
    private byte[]? _bg;
    private bool _imagesLoaded;

    public ReportBrandingProvider(IWebHostEnvironment env)
    {
        _env = env;
    }

    public ReportBranding Create(
        string printedBy,
        string printedVia,
        string? reportName = null,
        long? reportId = null,
        string? parameter = null,
        string? patientName = null)
    {
        EnsureImagesLoaded();

        var normalizedName = ReportTemplateNames.Normalize(reportName);
        var showQr = normalizedName is ReportTemplateNames.Lab
            or ReportTemplateNames.Gastro
            or ReportTemplateNames.Radiology;

        byte[]? qr = null;
        if (showQr && reportId.HasValue && !string.IsNullOrWhiteSpace(parameter))
        {
            var qrData =
                $"http://btkhospital.com/OnlineReports/Reports/ReportView.aspx?lRptNo={reportId}&lRptNm={reportName}&lParam={parameter}&PTNM={patientName}";
            qr = GenerateQrCode(qrData);
        }

        return new ReportBranding
        {
            PrintedBy = printedBy,
            PrintedVia = printedVia,
            LogoImage = _logo,
            BackgroundImage = _bg,
            ShowQrCode = showQr && qr != null,
            QrCodeImage = qr,
        };
    }

    private void EnsureImagesLoaded()
    {
        if (_imagesLoaded) return;
        lock (_imageLock)
        {
            if (_imagesLoaded) return;
            var logoPath = Path.Combine(_env.ContentRootPath, "Images", "Logo.jpg");
            var bgPath = Path.Combine(_env.ContentRootPath, "Images", "BG.jpg");
            _logo = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;
            _bg = File.Exists(bgPath) ? File.ReadAllBytes(bgPath) : null;
            _imagesLoaded = true;
        }
    }

    private static byte[] GenerateQrCode(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(20);
    }
}
