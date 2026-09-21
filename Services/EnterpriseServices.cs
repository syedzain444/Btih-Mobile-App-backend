using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
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
        Task<PaymentIntentDto> CreateIntentAsync(CreatePaymentRequest request);
        Task<PaymentIntentDto?> ConfirmAsync(ConfirmPaymentRequest request, string? rawPayload = null);
        Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null);
        Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo);
        Task<bool> ProcessWebhookAsync(string? signature, string rawBody);
    }

    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _repository;
        private readonly PaymentGatewaySettings _settings;

        public PaymentService(IPaymentRepository repository, IOptions<PaymentGatewaySettings> settings)
        {
            _repository = repository;
            _settings = settings.Value;
        }

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
            return await _repository.GetIntentAsync(request.PaymentId);
        }

        public Task<PaymentIntentDto?> GetIntentAsync(int paymentId, string? mrNo = null) =>
            _repository.GetIntentAsync(paymentId, mrNo);

        public Task<List<PaymentIntentDto>> GetHistoryAsync(string mrNo) =>
            _repository.GetHistoryAsync(mrNo.Trim());

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
        private readonly IJwtService _jwtService;
        private readonly AdminSettings _adminSettings;

        public AdminService(
            IAdminRepository adminRepository,
            IAdminPortalRepository adminPortalRepository,
            IMessagingRepository messagingRepository,
            IJwtService jwtService,
            IOptions<AdminSettings> adminSettings)
        {
            _adminRepository = adminRepository;
            _adminPortalRepository = adminPortalRepository;
            _messagingRepository = messagingRepository;
            _jwtService = jwtService;
            _adminSettings = adminSettings.Value;
        }

        public async Task<(bool Success, string Message, JwtTokenResult? Token, AdminUserDto? User)> LoginAsync(AdminLoginRequest request)
        {
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

            var token = _jwtService.GenerateToken(
                $"admin:{user.AdminId}",
                user.Username,
                user.Role,
                _adminSettings.TokenExpiryMinutes);

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
                    Role = request.Role is "Admin" or "Staff" ? request.Role : "Admin",
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
            return updated ? await _adminPortalRepository.GetAppointmentAsync(appointmentId) : null;
        }

        public async Task<PatientAppointment?> RejectAppointmentAsync(string appointmentId, string? notes)
        {
            var updated = await _adminPortalRepository.UpdateAppointmentStatusAsync(appointmentId, "Rejected", notes);
            return updated ? await _adminPortalRepository.GetAppointmentAsync(appointmentId) : null;
        }

        public async Task<MessageItem?> ReplyToThreadAsync(AdminReplyMessageRequest request)
        {
            var messageId = await _messagingRepository.AddMessageAsync(
                request.ThreadId,
                "STAFF",
                request.StaffName ?? "Hospital Staff",
                request.Body);

            await _messagingRepository.TouchThreadAsync(request.ThreadId);

            return new MessageItem
            {
                MessageId = messageId,
                ThreadId = request.ThreadId,
                SenderType = "STAFF",
                SenderName = request.StaffName ?? "Hospital Staff",
                Body = request.Body,
                CreatedAt = DateTime.Now,
                Attachments = new List<MessageAttachmentItem>(),
            };
        }

        public Task<List<RefillRequestItem>> GetPendingRefillsAsync() =>
            _adminRepository.GetPendingRefillsAsync();

        public Task<bool> UpdateRefillAsync(AdminUpdateRefillRequest request) =>
            _adminRepository.UpdateRefillAsync(request.RefillId, request.Status, request.StatusMessage);

        public Task<List<SupportTicketDto>> GetOpenTicketsAsync() =>
            _adminRepository.GetOpenTicketsAsync();

        public Task<bool> UpdateTicketAsync(AdminUpdateTicketRequest request) =>
            _adminRepository.UpdateTicketAsync(request.TicketId, request.Status, request.AdminNotes);
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
        SupportContactDto GetContactInfo();
        Task<List<FaqItem>> GetFaqAsync(string langCode, string? category);
        Task<int> CreateTicketAsync(CreateSupportTicketRequest request);
        Task<SupportTicketDto?> GetTicketAsync(int ticketId, string? mrNo);
        Task<List<SupportTicketDto>> GetTicketsAsync(string mrNo);
    }

    public class SupportService : ISupportService
    {
        private readonly ISupportRepository _repository;
        private readonly SupportSettings _settings;

        public SupportService(ISupportRepository repository, IOptions<SupportSettings> settings)
        {
            _repository = repository;
            _settings = settings.Value;
        }

        public SupportContactDto GetContactInfo() => new()
        {
            HospitalName = _settings.HospitalName,
            Phone = _settings.Phone,
            Email = _settings.Email,
            Address = _settings.Address,
            WorkingHours = _settings.WorkingHours,
        };

        public Task<List<FaqItem>> GetFaqAsync(string langCode, string? category) =>
            _repository.GetFaqAsync(langCode, category);

        public Task<int> CreateTicketAsync(CreateSupportTicketRequest request) =>
            _repository.CreateTicketAsync(request);

        public Task<SupportTicketDto?> GetTicketAsync(int ticketId, string? mrNo) =>
            _repository.GetTicketAsync(ticketId, mrNo);

        public Task<List<SupportTicketDto>> GetTicketsAsync(string mrNo) =>
            _repository.GetTicketsByMrNoAsync(mrNo.Trim());
    }
}
