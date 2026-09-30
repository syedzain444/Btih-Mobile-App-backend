using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
using HospitalMobileAPPApi.Services.Reports.Helpers;
using HospitalMobileAPPApi.Services.Reports.Layout;
using HospitalMobileAPPApi.Services.Reports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace HospitalMobileAPPApi.Services
{
    public interface IAppointmentConfirmationService
    {
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
        Task<AppointmentConfirmationQrDto> EnsureQrAsync(string appointmentId);
        Task<AppointmentConfirmationQrDto?> ResolveQrAsync(string qrTokenOrPayload);
        Task<byte[]> GenerateConfirmationPdfAsync(string appointmentId);
    }

    public class AppointmentConfirmationService : IAppointmentConfirmationService
    {
        private readonly IAppointmentConfirmationRepository _repository;
        private readonly IReportBrandingProvider _brandingProvider;
        private readonly PaymentGatewaySettings _paymentSettings;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly ILogger<AppointmentConfirmationService> _logger;

        public AppointmentConfirmationService(
            IAppointmentConfirmationRepository repository,
            IReportBrandingProvider brandingProvider,
            IOptions<PaymentGatewaySettings> paymentSettings,
            ILogger<AppointmentConfirmationService> logger,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _repository = repository;
            _brandingProvider = brandingProvider;
            _paymentSettings = paymentSettings.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
            _repository.EnsureSchemaAsync(cancellationToken);

        public async Task<AppointmentConfirmationQrDto> EnsureQrAsync(string appointmentId)
        {
            if (string.IsNullOrWhiteSpace(appointmentId))
            {
                throw new ArgumentException("Appointment ID is required.");
            }

            await _repository.EnsureSchemaAsync();

            var existing = await _repository.GetByAppointmentIdAsync(appointmentId);
            var snapshot = await _repository.GetLiveAppointmentSnapshotAsync(appointmentId)
                ?? throw new InvalidOperationException("Appointment not found.");

            var token = existing?.QrToken ?? GenerateToken();
            var payload = BuildPayload(token);

            var qr = new AppointmentConfirmationQrDto
            {
                AppointmentId = snapshot.AppointmentId,
                QrToken = token,
                QrPayload = payload,
                MrNo = snapshot.MrNo,
                PatientName = snapshot.PatientName,
                Phone = snapshot.Phone,
                DoctorName = snapshot.DoctorName,
                DepartmentId = snapshot.DepartmentId,
                DepartmentHint = snapshot.DepartmentHint,
                AppointmentTime = snapshot.AppointmentTime,
                Purpose = snapshot.Purpose,
                Status = snapshot.Status,
                CreatedAt = existing?.CreatedAt ?? DateTime.Now,
                Verified = true,
            };

            await _repository.UpsertQrAsync(qr);
            return qr;
        }

        public async Task<AppointmentConfirmationQrDto?> ResolveQrAsync(string qrTokenOrPayload)
        {
            var token = ExtractToken(qrTokenOrPayload);
            if (string.IsNullOrWhiteSpace(token)) return null;

            var stored = await _repository.GetByTokenAsync(token);
            if (stored == null) return null;

            // Refresh live status when possible so check-in sees current appointment state.
            var live = await _repository.GetLiveAppointmentSnapshotAsync(stored.AppointmentId);
            if (live != null)
            {
                stored.Status = live.Status;
                stored.PatientName = live.PatientName ?? stored.PatientName;
                stored.DoctorName = live.DoctorName ?? stored.DoctorName;
                stored.AppointmentTime = live.AppointmentTime ?? stored.AppointmentTime;
                stored.Purpose = live.Purpose ?? stored.Purpose;
                stored.DepartmentHint = live.DepartmentHint ?? stored.DepartmentHint;
                stored.MrNo = live.MrNo ?? stored.MrNo;
                stored.Phone = live.Phone ?? stored.Phone;
            }

            stored.Verified = true;
            return stored;
        }

        public async Task<byte[]> GenerateConfirmationPdfAsync(string appointmentId)
        {
            var qr = await EnsureQrAsync(appointmentId);
            var branding = _brandingProvider.Create(
                printedBy: "MobileApp",
                printedVia: "AppointmentConfirmation",
                reportName: null);

            // Inject verification QR into branding for hospital header layout.
            var qrPng = GenerateQrPng(qr.QrPayload);
            branding = new ReportBranding
            {
                HospitalEng = branding.HospitalEng,
                BranchEng = branding.BranchEng,
                AddressEng = branding.AddressEng,
                PhoneNo = branding.PhoneNo,
                HospitalUrd = branding.HospitalUrd,
                BranchUrd = branding.BranchUrd,
                AddressUrd = branding.AddressUrd,
                PrintedBy = branding.PrintedBy,
                PrintedVia = branding.PrintedVia,
                LogoImage = branding.LogoImage,
                BackgroundImage = branding.BackgroundImage,
                ShowQrCode = true,
                QrCodeImage = qrPng,
            };

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(0.4f, Unit.Inch);
                    page.MarginVertical(0.4f, Unit.Inch);
                    page.DefaultTextStyle(x => x.FontFamily(ReportTheme.FontFamily));
                    page.ApplyPageBackground(branding);

                    page.Content().Column(col =>
                    {
                        col.Item().Element(c => c.ComposeHospitalHeader(branding));

                        col.Item().PaddingTop(12).AlignCenter().Text("APPOINTMENT CONFIRMATION")
                            .Style(ReportTheme.Title);

                        col.Item().PaddingTop(4).AlignCenter().Text("Bahria Town International Hospital")
                            .Style(ReportTheme.PrescriptionLabel);

                        col.Item().PaddingTop(14).Element(c => ComposeDetails(c, qr));

                        col.Item().PaddingTop(16).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(box =>
                        {
                            box.Item().Text("Check-in QR").SemiBold().FontSize(11);
                            box.Item().PaddingTop(4).Text(
                                    "Present this QR at hospital reception. Scanning verifies your appointment details.")
                                .FontSize(9).FontColor(Colors.Grey.Darken2);
                            box.Item().PaddingTop(8).AlignCenter().Width(120).Image(qrPng).FitArea();
                            box.Item().PaddingTop(6).AlignCenter().Text($"Token: {qr.QrToken}")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        });

                        col.Item().PaddingTop(16).Text(
                                "Please arrive 15 minutes early and bring your MR card / CNIC. " +
                                "This document is computer-generated for the Bahria Town International Hospital mobile app.")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);

                        col.Item().PaddingTop(10).Text($"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeDetails(IContainer container, AppointmentConfirmationQrDto qr)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(130);
                    columns.RelativeColumn();
                });

                void Row(string label, string? value)
                {
                    table.Cell().PaddingVertical(3).Text(label).Style(ReportTheme.PrescriptionLabel);
                    table.Cell().PaddingVertical(3).Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                        .Style(ReportTheme.PrescriptionValue);
                }

                Row("Appointment ID", qr.AppointmentId);
                Row("Patient", qr.PatientName);
                Row("MR No", qr.MrNo);
                Row("Phone", qr.Phone);
                Row("Doctor", qr.DoctorName);
                Row("Specialty", qr.DepartmentHint);
                Row("Date / Time", qr.AppointmentTime);
                Row("Purpose", qr.Purpose);
                Row("Status", qr.Status);
            });
        }

        private string BuildPayload(string token)
        {
            var apiBase = ResolvePublicApiBase();
            if (!string.IsNullOrWhiteSpace(apiBase))
            {
                return $"{apiBase.TrimEnd('/')}/api/AppointmentConfirmation/qr/{token}";
            }

            return $"btihapp://appointment/qr/{token}";
        }

        private string ResolvePublicApiBase()
        {
            if (!string.IsNullOrWhiteSpace(_paymentSettings.PublicApiBaseUrl))
            {
                return _paymentSettings.PublicApiBaseUrl.Trim().TrimEnd('/');
            }

            var request = _httpContextAccessor?.HttpContext?.Request;
            if (request != null)
            {
                return $"{request.Scheme}://{request.Host.Value}";
            }

            return string.Empty;
        }

        private static string GenerateToken()
        {
            var bytes = new byte[24];
            Random.Shared.NextBytes(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        internal static string? ExtractToken(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var value = raw.Trim();

            var marker = "/api/AppointmentConfirmation/qr/";
            var idx = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var token = value[(idx + marker.Length)..];
                var cut = token.IndexOfAny(['?', '#', '/', '&']);
                return cut >= 0 ? token[..cut] : token;
            }

            const string deep = "btihapp://appointment/qr/";
            if (value.StartsWith(deep, StringComparison.OrdinalIgnoreCase))
            {
                var token = value[deep.Length..];
                var cut = token.IndexOfAny(['?', '#', '/', '&']);
                return cut >= 0 ? token[..cut] : token;
            }

            if (value.Length is >= 16 and <= 64 && value.All(Uri.IsHexDigit))
            {
                return value;
            }

            return value;
        }

        private static byte[] GenerateQrPng(string payload)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            return png.GetGraphic(10);
        }
    }
}
