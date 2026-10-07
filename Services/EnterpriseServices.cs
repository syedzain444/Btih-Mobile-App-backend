using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace HospitalMobileAPPApi.Services
{
    public interface IAuditLogService
    {
        Task WriteAsync(AuditLogEntry entry);
        Task<List<AuditLogItem>> GetRecentAsync(int take, string? mrNo = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly IAuditLogRepository _repository;

        public AuditLogService(IAuditLogRepository repository) => _repository = repository;

        public Task WriteAsync(AuditLogEntry entry) => _repository.WriteAsync(entry);

        public Task<List<AuditLogItem>> GetRecentAsync(int take, string? mrNo = null) =>
            _repository.GetRecentAsync(Math.Clamp(take, 1, 500), mrNo);
    }

    public interface IPaymentService
    {
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
        Task<PaymentIntentDto> CreateIntentAsync(CreatePaymentRequest request);
        Task<PaymentIntentDto?> ConfirmAsync(ConfirmPaymentRequest request, string? rawPayload = null);
        Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null);
        Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo);
        Task<PaymentQrResolveDto?> ResolveQrAsync(string qrTokenOrPayload);
        Task<bool> ProcessWebhookAsync(string? signature, string rawBody);
    }

    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _repository;
        private readonly PaymentGatewaySettings _settings;
        private readonly ILogger<PaymentService> _logger;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public PaymentService(
            IPaymentRepository repository,
            IOptions<PaymentGatewaySettings> settings,
            ILogger<PaymentService> logger,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _repository = repository;
            _settings = settings.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
            _repository.EnsureQrSchemaAsync(cancellationToken);

        public async Task<PaymentIntentDto> CreateIntentAsync(CreatePaymentRequest request)
        {
            if (request.Amount <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero.");
            }

            var gatewayRef = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
            var checkoutUrl = $"{_settings.BaseCheckoutUrl.TrimEnd('/')}/{gatewayRef}?return={Uri.EscapeDataString(request.ReturnUrl ?? _settings.DefaultReturnUrl)}";

            var intent = new PaymentIntentDto
            {
                MrNo = request.MrNo.Trim(),
                BillId = request.BillId,
                InvoiceNo = request.InvoiceNo,
                Amount = request.Amount,
                Currency = "PKR",
                Status = "PENDING",
                Gateway = _settings.Provider,
                CheckoutUrl = checkoutUrl,
                ReturnUrl = request.ReturnUrl ?? _settings.DefaultReturnUrl,
                GatewayRef = gatewayRef,
                CreatedAt = DateTime.Now,
            };

            var paymentId = await _repository.CreateIntentAsync(intent);
            intent.PaymentId = paymentId;
            await _repository.AddTransactionAsync(paymentId, "INTENT_CREATED", gatewayRef, null);

            if (_settings.EnablePaymentQr)
            {
                try
                {
                    await AttachQrAndAppointmentAsync(intent, request.AppointmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Payment #{PaymentId} created but QR/appointment attach failed", paymentId);
                }
            }

            return intent;
        }

        public async Task<PaymentIntentDto?> ConfirmAsync(ConfirmPaymentRequest request, string? rawPayload = null)
        {
            var intent = await _repository.GetIntentAsync(request.PaymentId);
            if (intent == null)
            {
                return null;
            }

            if (intent.Status is "PAID" or "FAILED" or "CANCELLED")
            {
                await _repository.AttachQrToIntentAsync(intent);
                return intent;
            }

            if (!_settings.AllowMockConfirm && _settings.Provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Mock payment confirmation is disabled.");
            }

            if (!string.IsNullOrWhiteSpace(_settings.WebhookSecret) &&
                !string.IsNullOrWhiteSpace(request.WebhookSignature) &&
                !VerifySignature(rawPayload ?? request.GatewayRef ?? string.Empty, request.WebhookSignature))
            {
                throw new UnauthorizedAccessException("Invalid payment webhook signature.");
            }

            var gatewayRef = request.GatewayRef ?? intent.GatewayRef;
            await _repository.UpdateStatusAsync(request.PaymentId, "PAID", gatewayRef, null);
            await _repository.AddTransactionAsync(request.PaymentId, "PAYMENT_CONFIRMED", gatewayRef, rawPayload);
            var updated = await _repository.GetIntentAsync(request.PaymentId);
            if (updated != null)
            {
                await _repository.AttachQrToIntentAsync(updated);
            }

            return updated;
        }

        public async Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null)
        {
            var intent = await _repository.GetIntentAsync(paymentId, mrNo);
            if (intent != null)
            {
                await _repository.AttachQrToIntentAsync(intent);
                if (!string.IsNullOrWhiteSpace(intent.QrPayload))
                {
                    intent.QrImageBase64 = GenerateQrBase64(intent.QrPayload);
                }
            }

            return intent;
        }

        public async Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo)
        {
            var history = await _repository.GetHistoryAsync(mrNo.Trim());
            foreach (var intent in history)
            {
                await _repository.AttachQrToIntentAsync(intent);
            }

            return history;
        }

        public async Task<PaymentQrResolveDto?> ResolveQrAsync(string qrTokenOrPayload)
        {
            var token = ExtractQrToken(qrTokenOrPayload);
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return await _repository.GetPaymentQrByTokenAsync(token);
        }

        private async Task AttachQrAndAppointmentAsync(PaymentIntentDto intent, string? appointmentId)
        {
            await _repository.EnsureQrSchemaAsync();

            PaymentAppointmentDetailsDto? appointment = null;
            if (!string.IsNullOrWhiteSpace(appointmentId))
            {
                appointment = await _repository.GetAppointmentDetailsAsync(appointmentId);
            }

            appointment ??= await _repository.GetLatestActiveAppointmentAsync(intent.MrNo);

            var token = GenerateQrToken();
            var payload = BuildQrPayload(token);
            await _repository.SavePaymentQrAsync(intent.PaymentId, token, payload, appointment);

            intent.QrToken = token;
            intent.QrPayload = payload;
            intent.QrImageBase64 = GenerateQrBase64(payload);
            intent.Appointment = appointment;
        }

        private string BuildQrPayload(string token)
        {
            var apiBase = ResolvePublicApiBase();
            if (!string.IsNullOrWhiteSpace(apiBase))
            {
                return $"{apiBase.TrimEnd('/')}/api/Payment/qr/{token}";
            }

            return $"btihapp://payment/qr/{token}";
        }

        private string ResolvePublicApiBase()
        {
            if (!string.IsNullOrWhiteSpace(_settings.PublicApiBaseUrl))
            {
                return _settings.PublicApiBaseUrl.Trim().TrimEnd('/');
            }

            var request = _httpContextAccessor?.HttpContext?.Request;
            if (request != null)
            {
                return $"{request.Scheme}://{request.Host.Value}";
            }

            return string.Empty;
        }

        private static string GenerateQrToken()
        {
            var bytes = new byte[24];
            Random.Shared.NextBytes(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        internal static string? ExtractQrToken(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var value = raw.Trim();

            // Absolute or relative API path
            var marker = "/api/Payment/qr/";
            var idx = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var token = value[(idx + marker.Length)..];
                var cut = token.IndexOfAny(['?', '#', '/', '&']);
                return cut >= 0 ? token[..cut] : token;
            }

            // Deep link btihapp://payment/qr/{token}
            const string deep = "btihapp://payment/qr/";
            if (value.StartsWith(deep, StringComparison.OrdinalIgnoreCase))
            {
                var token = value[deep.Length..];
                var cut = token.IndexOfAny(['?', '#', '/', '&']);
                return cut >= 0 ? token[..cut] : token;
            }

            // Bare token (hex)
            if (value.Length is >= 16 and <= 64 && value.All(Uri.IsHexDigit))
            {
                return value;
            }

            return value;
        }

        private static string GenerateQrBase64(string payload)
        {
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
            var png = new QRCoder.PngByteQRCode(data);
            var bytes = png.GetGraphic(8);
            return Convert.ToBase64String(bytes);
        }

        public async Task<bool> ProcessWebhookAsync(string? signature, string rawBody)
        {
            if (!string.IsNullOrWhiteSpace(_settings.WebhookSecret) &&
                !VerifySignature(rawBody, signature ?? string.Empty))
            {
                return false;
            }

            var paymentId = ExtractPaymentId(rawBody);
            if (paymentId == null)
            {
                return false;
            }

            var intent = await _repository.GetIntentAsync(paymentId.Value);
            if (intent == null)
            {
                return false;
            }

            var status = rawBody.Contains("failed", StringComparison.OrdinalIgnoreCase) ? "FAILED" : "PAID";
            await _repository.UpdateStatusAsync(paymentId.Value, status, intent.GatewayRef, rawBody);
            await _repository.AddTransactionAsync(paymentId.Value, "WEBHOOK", intent.GatewayRef, rawBody);
            return true;
        }

        private bool VerifySignature(string payload, string signature)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.WebhookSecret));
            var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            return string.Equals(hash, signature, StringComparison.OrdinalIgnoreCase);
        }

        private static int? ExtractPaymentId(string rawBody)
        {
            const string key = "\"paymentId\":";
            var idx = rawBody.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var start = idx + key.Length;
            var end = start;
            while (end < rawBody.Length && char.IsDigit(rawBody[end])) end++;
            return int.TryParse(rawBody[start..end], out var id) ? id : null;
        }
    }

    public interface IAdminService
    {
        Task<(bool Success, string Message, JwtTokenResult? Token, AdminUserDto? User)> LoginAsync(AdminLoginRequest request);
        Task<(bool Success, string Message)> BootstrapAsync(AdminBootstrapRequest request);
        Task<List<MessageThreadSummary>> GetMessageThreadsAsync();
        Task<PagedResult<MessageItem>?> GetThreadMessagesAsync(int threadId, int pageNumber, int pageSize);
        Task<MessageItem?> ReplyToThreadAsync(AdminReplyMessageRequest request);
        Task<PagedResult<AdminPortalUserDto>> GetPortalUsersAsync(string? search, int pageNumber, int pageSize);
        Task<PagedResult<PatientAppointment>> GetAppointmentsAsync(
            string? status,
            DateTime? from,
            DateTime? to,
            string? search,
            int pageNumber,
            int pageSize);
        Task<PatientAppointment?> ApproveAppointmentAsync(string appointmentId, string? notes);
        Task<PatientAppointment?> RejectAppointmentAsync(string appointmentId, string? notes);
        Task<List<RefillRequestItem>> GetPendingRefillsAsync();
        Task<bool> UpdateRefillAsync(AdminUpdateRefillRequest request);
        Task<List<SupportTicketDto>> GetOpenTicketsAsync();
        Task<bool> UpdateTicketAsync(AdminUpdateTicketRequest request);
    }

    public interface IAdminReportService
    {
        Task<RegistrationReportDto> GetRegistrationsReportAsync(DateTime? from, DateTime? to);
        Task<AppointmentReportDto> GetAppointmentsReportAsync(string? status, DateTime? from, DateTime? to);
        Task<byte[]> ExportEngagementAsync(DateTime? from, DateTime? to);
    }

    public class AdminService : IAdminService
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IAdminPortalRepository _adminPortalRepository;
        private readonly IMessagingRepository _messagingRepository;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ISmsService _smsService;
        private readonly IJwtService _jwtService;
        private readonly IAppointmentPrepService _appointmentPrepService;
        private readonly IRbacService _rbacService;
        private readonly AdminSettings _adminSettings;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            IAdminRepository adminRepository,
            IAdminPortalRepository adminPortalRepository,
            IMessagingRepository messagingRepository,
            IPushNotificationService pushNotificationService,
            ISmsService smsService,
            IJwtService jwtService,
            IAppointmentPrepService appointmentPrepService,
            IRbacService rbacService,
            IOptions<AdminSettings> adminSettings,
            ILogger<AdminService> logger)
        {
            _adminRepository = adminRepository;
            _adminPortalRepository = adminPortalRepository;
            _messagingRepository = messagingRepository;
            _pushNotificationService = pushNotificationService;
            _smsService = smsService;
            _jwtService = jwtService;
            _appointmentPrepService = appointmentPrepService;
            _rbacService = rbacService;
            _adminSettings = adminSettings.Value;
            _logger = logger;
        }

        public async Task<(bool Success, string Message, JwtTokenResult? Token, AdminUserDto? User)> LoginAsync(AdminLoginRequest request)
        {
            try
            {
                await _rbacService.EnsureSchemaAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RBAC schema ensure failed during admin login");
            }

            var user = await _adminRepository.GetByUsernameAsync(request.Username.Trim());
            if (user == null)
            {
                return (false, "Invalid username or password", null, null);
            }

            var hash = await _adminRepository.GetPasswordHashAsync(request.Username.Trim());
            if (string.IsNullOrWhiteSpace(hash) ||
                hash.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) ||
                !PasswordHasher.Verify(request.Username.Trim(), request.Password, _adminSettings.PasswordSalt, hash))
            {
                return (false, "Invalid username or password", null, null);
            }

            // Prefer DB role code; fall back to known portal roles
            var roleCode = user.Role?.Trim() ?? AppRoles.Staff;
            var roleRecord = (await _rbacService.GetRolesAsync(false))
                .FirstOrDefault(r => string.Equals(r.Code, roleCode, StringComparison.OrdinalIgnoreCase));
            if (roleRecord != null)
            {
                roleCode = roleRecord.Code;
            }
            else if (AppRoles.IsPortalRole(roleCode))
            {
                roleCode = AppRoles.NormalizePortalRole(roleCode);
            }

            user.Role = roleCode;
            user.Permissions = await _rbacService.GetModulesForRoleAsync(roleCode);

            var token = _jwtService.GenerateToken(
                $"admin:{user.AdminId}",
                user.Username,
                roleCode,
                _adminSettings.TokenExpiryMinutes,
                isAdminPortal: true);

            return (true, "Login successful", token, user);
        }

        public async Task<(bool Success, string Message)> BootstrapAsync(AdminBootstrapRequest request)
        {
            if (string.IsNullOrWhiteSpace(_adminSettings.BootstrapSecret) ||
                !string.Equals(request.BootstrapSecret, _adminSettings.BootstrapSecret, StringComparison.Ordinal))
            {
                return (false, "Invalid bootstrap secret.");
            }

            var existingHash = await _adminRepository.GetPasswordHashAsync(request.Username.Trim());
            var passwordHash = PasswordHasher.Hash(request.Username.Trim(), request.Password, _adminSettings.PasswordSalt);

            if (existingHash == null)
            {
                var created = await _adminRepository.CreateAdminAsync(new AdminUserDto
                {
                    Username = request.Username.Trim(),
                    DisplayName = request.DisplayName,
                    Role = AppRoles.NormalizePortalRole(request.Role),
                    IsActive = true,
                }, passwordHash);

                return created
                    ? (true, "Admin user created.")
                    : (false, "Could not create admin user.");
            }

            if (existingHash.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                var updated = await _adminRepository.UpdatePasswordHashAsync(request.Username.Trim(), passwordHash);
                return updated
                    ? (true, "Admin password initialized.")
                    : (false, "Could not update admin password.");
            }

            return (false, "Admin user already initialized. Use login or contact IT.");
        }

        public Task<List<MessageThreadSummary>> GetMessageThreadsAsync() =>
            _messagingRepository.GetAdminInboxAsync();

        public async Task<PagedResult<MessageItem>?> GetThreadMessagesAsync(
            int threadId,
            int pageNumber,
            int pageSize)
        {
            if (!await _messagingRepository.ThreadExistsAsync(threadId))
            {
                return null;
            }

            var (page, size, skip) = AdminPagination.Normalize(pageNumber, pageSize);
            var total = await _messagingRepository.GetMessageCountAsync(threadId);
            var data = await _messagingRepository.GetMessagesAsync(threadId, skip, size);

            return new PagedResult<MessageItem>
            {
                PageNumber = page,
                PageSize = size,
                TotalRecords = total,
                TotalPages = AdminPagination.TotalPages(total, size),
                Data = data,
            };
        }

        public async Task<PagedResult<AdminPortalUserDto>> GetPortalUsersAsync(
            string? search,
            int pageNumber,
            int pageSize)
        {
            var (page, size, skip) = AdminPagination.Normalize(pageNumber, pageSize);
            var (items, total) = await _adminPortalRepository.GetPortalUsersAsync(search, skip, size);

            return new PagedResult<AdminPortalUserDto>
            {
                PageNumber = page,
                PageSize = size,
                TotalRecords = total,
                TotalPages = AdminPagination.TotalPages(total, size),
                Data = items,
            };
        }

        public async Task<PagedResult<PatientAppointment>> GetAppointmentsAsync(
            string? status,
            DateTime? from,
            DateTime? to,
            string? search,
            int pageNumber,
            int pageSize)
        {
            var (page, size, skip) = AdminPagination.Normalize(pageNumber, pageSize);
            var rangeFrom = from?.Date;
            var rangeTo = to?.Date.AddDays(1).AddTicks(-1);
            var (items, total) = await _adminPortalRepository.GetAppointmentsAsync(
                status,
                rangeFrom,
                rangeTo,
                search,
                skip,
                size);

            return new PagedResult<PatientAppointment>
            {
                PageNumber = page,
                PageSize = size,
                TotalRecords = total,
                TotalPages = AdminPagination.TotalPages(total, size),
                Data = items,
            };
        }

        public async Task<PatientAppointment?> ApproveAppointmentAsync(string appointmentId, string? notes)
        {
            var updated = await _adminPortalRepository.UpdateAppointmentStatusAsync(appointmentId, "Confirmed", notes);
            if (!updated)
            {
                return null;
            }

            var appointment = await _adminPortalRepository.GetAppointmentAsync(appointmentId);
            if (appointment == null)
            {
                return null;
            }

            await SendAppointmentApprovedSmsAsync(appointment);

            if (!string.IsNullOrWhiteSpace(appointment.MRNo))
            {
                try
                {
                    await _pushNotificationService.SendToPatientAsync(
                        appointment.MRNo,
                        "Appointment Confirmed",
                        $"Your appointment{(string.IsNullOrWhiteSpace(appointment.AppointmentTime) ? string.Empty : $" ({appointment.AppointmentTime})")} has been confirmed.",
                        PushNotificationTypes.AppointmentConfirmed,
                        new Dictionary<string, string>
                        {
                            ["screen"] = "appointments",
                            ["appointmentId"] = appointmentId,
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send appointment confirmed push for {AppointmentId}", appointmentId);
                }

                try
                {
                    await _appointmentPrepService.ScheduleForAppointmentAsync(appointmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to schedule fasting prep for confirmed appointment {AppointmentId}", appointmentId);
                }
            }

            return appointment;
        }

        private async Task SendAppointmentApprovedSmsAsync(PatientAppointment appointment)
        {
            if (string.IsNullOrWhiteSpace(appointment.PhoneNo))
            {
                _logger.LogWarning(
                    "Appointment {AppointmentId} approved but no phone number on record for SMS.",
                    appointment.AppointmentId);
                return;
            }

            var message = BuildAppointmentApprovedSms(appointment);
            var sent = await _smsService.SendAsync(appointment.PhoneNo, message);
            if (!sent)
            {
                _logger.LogWarning(
                    "Failed to send appointment approval SMS for {AppointmentId} to {PhoneNo}",
                    appointment.AppointmentId,
                    appointment.PhoneNo);
            }
        }

        private static string BuildAppointmentApprovedSms(PatientAppointment appointment)
        {
            var parts = new List<string> { "BTIH: Your appointment has been confirmed." };

            if (!string.IsNullOrWhiteSpace(appointment.DoctorName))
            {
                parts.Add($"Doctor: {appointment.DoctorName.Trim()}.");
            }

            if (!string.IsNullOrWhiteSpace(appointment.AppointmentTime))
            {
                parts.Add($"Time: {appointment.AppointmentTime.Trim()}.");
            }

            if (!string.IsNullOrWhiteSpace(appointment.MRNo))
            {
                parts.Add($"MR No: {appointment.MRNo.Trim()}.");
            }

            return string.Join(" ", parts);
        }

        public async Task<PatientAppointment?> RejectAppointmentAsync(string appointmentId, string? notes)
        {
            var updated = await _adminPortalRepository.UpdateAppointmentStatusAsync(appointmentId, "Rejected", notes);
            if (!updated)
            {
                return null;
            }

            try
            {
                await _appointmentPrepService.CancelForAppointmentAsync(appointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel prep alerts for rejected appointment {AppointmentId}", appointmentId);
            }

            return await _adminPortalRepository.GetAppointmentAsync(appointmentId);
        }

        public async Task<MessageItem?> ReplyToThreadAsync(AdminReplyMessageRequest request)
        {
            var thread = await _messagingRepository.GetThreadAsync(request.ThreadId);
            if (thread == null)
            {
                return null;
            }

            var messageId = await _messagingRepository.AddMessageAsync(
                request.ThreadId,
                "STAFF",
                request.StaffName ?? "Hospital Staff",
                request.Body);

            await _messagingRepository.TouchThreadAsync(request.ThreadId);

            var message = new MessageItem
            {
                MessageId = messageId,
                ThreadId = request.ThreadId,
                SenderType = "STAFF",
                SenderName = request.StaffName ?? "Hospital Staff",
                Body = request.Body,
                CreatedAt = DateTime.Now,
                Attachments = new List<MessageAttachmentItem>(),
            };

            try
            {
                var preview = request.Body.Trim();
                if (preview.Length > 120)
                {
                    preview = preview[..120] + "...";
                }

                await _pushNotificationService.SendToPatientAsync(
                    thread.MrNo,
                    "New message from hospital",
                    preview,
                    PushNotificationTypes.MessageReceived,
                    new Dictionary<string, string>
                    {
                        ["threadId"] = request.ThreadId.ToString(),
                        ["subject"] = thread.Subject ?? string.Empty,
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message push for thread {ThreadId}", request.ThreadId);
            }

            return message;
        }

        public Task<List<RefillRequestItem>> GetPendingRefillsAsync() =>
            _adminRepository.GetPendingRefillsAsync();

        public Task<bool> UpdateRefillAsync(AdminUpdateRefillRequest request) =>
            _adminRepository.UpdateRefillAsync(request.RefillId, request.Status, request.StatusMessage);

        public Task<List<SupportTicketDto>> GetOpenTicketsAsync() =>
            _adminRepository.GetOpenTicketsAsync();

        public async Task<bool> UpdateTicketAsync(AdminUpdateTicketRequest request)
        {
            var existing = await _adminRepository.GetTicketByIdAsync(request.TicketId);
            if (existing == null)
            {
                return false;
            }

            var updated = await _adminRepository.UpdateTicketAsync(
                request.TicketId,
                request.Status,
                request.AdminNotes);
            if (!updated)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(existing.MrNo))
            {
                return true;
            }

            try
            {
                var hasReply = !string.IsNullOrWhiteSpace(request.AdminNotes);
                var statusLabel = request.Status.Replace('_', ' ').ToLowerInvariant();
                var title = hasReply
                    ? "Reply on your support ticket"
                    : "Support ticket updated";
                var body = hasReply
                    ? TruncateForPush(request.AdminNotes!)
                    : $"Ticket #{request.TicketId} is now {statusLabel}.";

                await _pushNotificationService.SendToPatientAsync(
                    existing.MrNo,
                    title,
                    body,
                    hasReply
                        ? PushNotificationTypes.SupportTicketReply
                        : PushNotificationTypes.SupportTicketUpdated,
                    new Dictionary<string, string>
                    {
                        ["ticketId"] = request.TicketId.ToString(),
                        ["status"] = request.Status,
                        ["category"] = existing.Category ?? string.Empty,
                        ["screen"] = "support_tickets",
                        ["priority"] = "high",
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send support ticket push for ticket {TicketId}",
                    request.TicketId);
            }

            return true;
        }

        private static string TruncateForPush(string text)
        {
            var trimmed = text.Trim();
            return trimmed.Length > 120 ? trimmed[..120] + "..." : trimmed;
        }
    }

    public class AdminReportService : IAdminReportService
    {
        private readonly IAdminPortalRepository _adminPortalRepository;

        public AdminReportService(IAdminPortalRepository adminPortalRepository)
        {
            _adminPortalRepository = adminPortalRepository;
        }

        public Task<RegistrationReportDto> GetRegistrationsReportAsync(DateTime? from, DateTime? to)
        {
            var range = AnalyticsDateRange.Normalize(from, to);
            return _adminPortalRepository.GetRegistrationReportAsync(range.From, range.To);
        }

        public Task<AppointmentReportDto> GetAppointmentsReportAsync(string? status, DateTime? from, DateTime? to)
        {
            var range = AnalyticsDateRange.Normalize(from, to);
            return _adminPortalRepository.GetAppointmentReportAsync(status, range.From, range.To);
        }

        public Task<byte[]> ExportEngagementAsync(DateTime? from, DateTime? to)
        {
            var range = AnalyticsDateRange.Normalize(from, to);
            return _adminPortalRepository.ExportEngagementCsvAsync(range.From, range.To);
        }
    }

    public interface ITelemedicineService
    {
        Task<TelemedSessionDto> CreateSessionAsync(CreateTelemedSessionRequest request);
        Task<TelemedSessionDto?> GetSessionAsync(int sessionId, string? mrNo = null);
        Task<List<TelemedSessionDto>> GetSessionsAsync(string mrNo);
        Task<TelemedSessionDto?> UpdateStatusAsync(int sessionId, string status, string? mrNo = null);
    }

    public class TelemedicineService : ITelemedicineService
    {
        private readonly ITelemedicineRepository _repository;
        private readonly TelemedicineSettings _settings;

        public TelemedicineService(ITelemedicineRepository repository, IOptions<TelemedicineSettings> settings)
        {
            _repository = repository;
            _settings = settings.Value;
        }

        public async Task<TelemedSessionDto> CreateSessionAsync(CreateTelemedSessionRequest request)
        {
            var roomId = $"btih-{Guid.NewGuid():N}"[..16];
            var patientToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            var session = new TelemedSessionDto
            {
                MrNo = request.MrNo.Trim(),
                AppointmentId = request.AppointmentId,
                DoctorId = request.DoctorId,
                DoctorName = request.DoctorName,
                RoomId = roomId,
                PatientToken = patientToken,
                Status = "SCHEDULED",
                ScheduledAt = request.ScheduledAt ?? DateTime.Now.AddMinutes(5),
            };

            var sessionId = await _repository.CreateSessionAsync(session);
            session.SessionId = sessionId;
            session.JoinUrl = $"{_settings.PatientJoinBaseUrl.TrimEnd('/')}?sessionId={sessionId}&room={roomId}&token={Uri.EscapeDataString(patientToken)}";
            return session;
        }

        public async Task<TelemedSessionDto?> GetSessionAsync(int sessionId, string? mrNo = null)
        {
            var session = await _repository.GetSessionAsync(sessionId, mrNo);
            if (session != null)
            {
                session.JoinUrl = BuildJoinUrl(session);
            }
            return session;
        }

        public async Task<List<TelemedSessionDto>> GetSessionsAsync(string mrNo)
        {
            var sessions = await _repository.GetSessionsByMrNoAsync(mrNo.Trim());
            foreach (var session in sessions)
            {
                session.JoinUrl = BuildJoinUrl(session);
            }
            return sessions;
        }

        public async Task<TelemedSessionDto?> UpdateStatusAsync(int sessionId, string status, string? mrNo = null)
        {
            var allowed = new[] { "SCHEDULED", "ACTIVE", "COMPLETED", "CANCELLED" };
            if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid session status.");
            }

            var updated = await _repository.UpdateStatusAsync(sessionId, status.ToUpperInvariant());
            if (!updated)
            {
                return null;
            }

            return await GetSessionAsync(sessionId, mrNo);
        }

        private string BuildJoinUrl(TelemedSessionDto session) =>
            $"{_settings.PatientJoinBaseUrl.TrimEnd('/')}?sessionId={session.SessionId}&room={session.RoomId}&token={Uri.EscapeDataString(session.PatientToken ?? string.Empty)}";
    }

    public interface IContentService
    {
        Task<List<LocalizedContentItem>> GetContentAsync(string langCode);
        Task<LocalizedContentItem?> GetItemAsync(string contentKey, string langCode);
    }

    public class ContentService : IContentService
    {
        private readonly IContentRepository _repository;

        public ContentService(IContentRepository repository) => _repository = repository;

        public Task<List<LocalizedContentItem>> GetContentAsync(string langCode) =>
            _repository.GetByLanguageAsync(NormalizeLang(langCode));

        public Task<LocalizedContentItem?> GetItemAsync(string contentKey, string langCode) =>
            _repository.GetItemAsync(contentKey, NormalizeLang(langCode));

        private static string NormalizeLang(string langCode)
        {
            var code = langCode.Trim().ToLowerInvariant();
            return code is "en" or "ur" ? code : "en";
        }
    }

    public interface ISupportService
    {
        Task<SupportContactDto> GetContactInfoAsync();
        Task UpdateContactInfoAsync(SupportContactDto contact);
        Task<List<FaqItem>> GetFaqAsync(string langCode, string? category);
        Task<List<FaqAdminItem>> GetAllFaqsAdminAsync();
        Task<FaqAdminItem?> GetFaqByIdAsync(int faqId);
        Task<int> CreateFaqAsync(FaqAdminItem item);
        Task<bool> UpdateFaqAsync(FaqAdminItem item);
        Task<bool> DeleteFaqAsync(int faqId);
        Task<int> CreateTicketAsync(CreateSupportTicketRequest request);
        Task<SupportTicketDto?> GetTicketAsync(int ticketId, string? mrNo);
        Task<List<SupportTicketDto>> GetTicketsAsync(string mrNo);
    }

    public class SupportService : ISupportService
    {
        private readonly ISupportRepository _repository;
        private readonly IAuditLogService _auditLogService;
        private readonly IMobilePortalSchemaService _schemaService;
        private readonly SupportSettings _settings;
        private readonly ILogger<SupportService> _logger;

        public SupportService(
            ISupportRepository repository,
            IAuditLogService auditLogService,
            IMobilePortalSchemaService schemaService,
            IOptions<SupportSettings> settings,
            ILogger<SupportService> logger)
        {
            _repository = repository;
            _auditLogService = auditLogService;
            _schemaService = schemaService;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<SupportContactDto> GetContactInfoAsync()
        {
            try
            {
                await _schemaService.EnsureSupportContentSchemaAsync();
                var fromDb = await _repository.GetContactAsync();
                if (fromDb != null &&
                    !string.IsNullOrWhiteSpace(fromDb.Phone) &&
                    !string.IsNullOrWhiteSpace(fromDb.Email))
                {
                    return fromDb;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load support contact from database; using appsettings fallback.");
            }

            return new SupportContactDto
            {
                HospitalName = _settings.HospitalName,
                Phone = _settings.Phone,
                Email = _settings.Email,
                Address = _settings.Address,
                WorkingHours = _settings.WorkingHours,
            };
        }

        public async Task UpdateContactInfoAsync(SupportContactDto contact)
        {
            await _schemaService.EnsureSupportContentSchemaAsync();
            contact.HospitalName = contact.HospitalName?.Trim() ?? string.Empty;
            contact.Phone = contact.Phone?.Trim() ?? string.Empty;
            contact.Email = contact.Email?.Trim() ?? string.Empty;
            contact.Address = contact.Address?.Trim() ?? string.Empty;
            contact.WorkingHours = contact.WorkingHours?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(contact.HospitalName) ||
                string.IsNullOrWhiteSpace(contact.Phone) ||
                string.IsNullOrWhiteSpace(contact.Email) ||
                string.IsNullOrWhiteSpace(contact.Address) ||
                string.IsNullOrWhiteSpace(contact.WorkingHours))
            {
                throw new InvalidOperationException(
                    "Hospital name, phone, email, address, and working hours are required.");
            }

            await _repository.UpsertContactAsync(contact);
        }

        public async Task<List<FaqItem>> GetFaqAsync(string langCode, string? category)
        {
            try
            {
                await _schemaService.EnsureSupportContentSchemaAsync();
                return await _repository.GetFaqAsync(langCode, category);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load FAQ list");
                return new List<FaqItem>();
            }
        }

        public async Task<List<FaqAdminItem>> GetAllFaqsAdminAsync()
        {
            await _schemaService.EnsureSupportContentSchemaAsync();
            return await _repository.GetAllFaqsAdminAsync();
        }

        public async Task<FaqAdminItem?> GetFaqByIdAsync(int faqId)
        {
            await _schemaService.EnsureSupportContentSchemaAsync();
            return await _repository.GetFaqByIdAsync(faqId);
        }

        public async Task<int> CreateFaqAsync(FaqAdminItem item)
        {
            await _schemaService.EnsureSupportContentSchemaAsync();
            ValidateFaq(item);
            return await _repository.CreateFaqAsync(item);
        }

        public async Task<bool> UpdateFaqAsync(FaqAdminItem item)
        {
            await _schemaService.EnsureSupportContentSchemaAsync();
            ValidateFaq(item);
            return await _repository.UpdateFaqAsync(item);
        }

        public async Task<bool> DeleteFaqAsync(int faqId)
        {
            await _schemaService.EnsureSupportContentSchemaAsync();
            return await _repository.DeleteFaqAsync(faqId);
        }

        public async Task<int> CreateTicketAsync(CreateSupportTicketRequest request)
        {
            request.ContactName = request.ContactName.Trim();
            request.Category = string.IsNullOrWhiteSpace(request.Category)
                ? "General"
                : request.Category.Trim();
            request.Subject = string.IsNullOrWhiteSpace(request.Subject)
                ? request.Category
                : request.Subject.Trim();
            request.Description = request.Description.Trim();
            if (!string.IsNullOrWhiteSpace(request.MrNo))
            {
                request.MrNo = request.MrNo.Trim();
            }
            if (!string.IsNullOrWhiteSpace(request.ContactPhone))
            {
                request.ContactPhone = request.ContactPhone.Trim();
            }
            if (!string.IsNullOrWhiteSpace(request.ContactEmail))
            {
                request.ContactEmail = request.ContactEmail.Trim();
            }

            var ticketId = await _repository.CreateTicketAsync(request);

            _logger.LogWarning(
                "STAFF_ALERT new support ticket #{TicketId} category={Category} subject={Subject} mrNo={MrNo} contact={Contact} notify={SupportEmail}",
                ticketId,
                request.Category,
                request.Subject,
                request.MrNo ?? "(guest)",
                request.ContactName,
                _settings.Email);

            try
            {
                await _auditLogService.WriteAsync(new AuditLogEntry
                {
                    ActorId = request.MrNo ?? request.ContactName,
                    ActorRole = string.IsNullOrWhiteSpace(request.MrNo) ? "Guest" : "Patient",
                    Action = "SUPPORT_TICKET_CREATED",
                    EntityType = "SupportTicket",
                    EntityId = ticketId.ToString(),
                    MrNo = request.MrNo,
                    Details =
                        $"Category={request.Category}; Subject={request.Subject}; " +
                        $"NotifyStaff={_settings.Email}",
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write audit for support ticket {TicketId}", ticketId);
            }

            return ticketId;
        }

        public Task<SupportTicketDto?> GetTicketAsync(int ticketId, string? mrNo) =>
            _repository.GetTicketAsync(ticketId, mrNo);

        public Task<List<SupportTicketDto>> GetTicketsAsync(string mrNo) =>
            _repository.GetTicketsByMrNoAsync(mrNo.Trim());

        private static void ValidateFaq(FaqAdminItem item)
        {
            item.Category = string.IsNullOrWhiteSpace(item.Category) ? "General" : item.Category.Trim();
            item.QuestionEn = item.QuestionEn?.Trim() ?? string.Empty;
            item.AnswerEn = item.AnswerEn?.Trim() ?? string.Empty;
            item.QuestionUr = string.IsNullOrWhiteSpace(item.QuestionUr) ? null : item.QuestionUr.Trim();
            item.AnswerUr = string.IsNullOrWhiteSpace(item.AnswerUr) ? null : item.AnswerUr.Trim();

            if (string.IsNullOrWhiteSpace(item.QuestionEn) || string.IsNullOrWhiteSpace(item.AnswerEn))
            {
                throw new InvalidOperationException("English question and answer are required.");
            }
        }
    }
}
