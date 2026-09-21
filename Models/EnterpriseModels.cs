namespace HospitalMobileAPPApi.Models
{
    public class CreatePaymentRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string? BillId { get; set; }
        public string? InvoiceNo { get; set; }
        public decimal Amount { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class PaymentIntentDto
    {
        public int PaymentId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public string? BillId { get; set; }
        public string? InvoiceNo { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "PKR";
        public string Status { get; set; } = "PENDING";
        public string Gateway { get; set; } = string.Empty;
        public string? CheckoutUrl { get; set; }
        public string? ReturnUrl { get; set; }
        public string? GatewayRef { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class ConfirmPaymentRequest
    {
        public int PaymentId { get; set; }
        public string? GatewayRef { get; set; }
        public string? WebhookSignature { get; set; }
    }

    public class AdminLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AdminBootstrapRequest
    {
        public string BootstrapSecret { get; set; } = string.Empty;
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = "BTIH Admin";
        public string Role { get; set; } = "Admin";
    }

    public class AdminUserDto
    {
        public int AdminId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class AdminReplyMessageRequest
    {
        public int ThreadId { get; set; }
        public string Body { get; set; } = string.Empty;
        public string? StaffName { get; set; }
    }

    public class AdminUpdateRefillRequest
    {
        public int RefillId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? StatusMessage { get; set; }
    }

    public class AdminUpdateTicketRequest
    {
        public int TicketId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }
    }

    public class AuditLogEntry
    {
        public string? ActorId { get; set; }
        public string? ActorRole { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? MrNo { get; set; }
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
    }

    public class AuditLogItem
    {
        public int AuditId { get; set; }
        public string? ActorId { get; set; }
        public string? ActorRole { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? MrNo { get; set; }
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateTelemedSessionRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string? AppointmentId { get; set; }
        public int? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }

    public class TelemedSessionDto
    {
        public int SessionId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public string? AppointmentId { get; set; }
        public int? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string? JoinUrl { get; set; }
        public string? PatientToken { get; set; }
    }

    public class LocalizedContentItem
    {
        public string ContentKey { get; set; } = string.Empty;
        public string LangCode { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Body { get; set; }
    }

    public class FaqItem
    {
        public int FaqId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }

    public class SupportContactDto
    {
        public string HospitalName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string WorkingHours { get; set; } = string.Empty;
    }

    public class CreateSupportTicketRequest
    {
        public string? MrNo { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string Category { get; set; } = "General";
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class SupportTicketDto
    {
        public int TicketId { get; set; }
        public string? MrNo { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
