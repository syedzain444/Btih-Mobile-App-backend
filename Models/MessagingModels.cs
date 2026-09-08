namespace HospitalMobileAPPApi.Models
{
    public class CreateThreadRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? InitialMessage { get; set; }
    }

    public class SendMessageRequest
    {
        public string MrNo { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    public class MessageThreadSummary
    {
        public int ThreadId { get; set; }
        public string MrNo { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
    }

    public class MessageItem
    {
        public int MessageId { get; set; }
        public int ThreadId { get; set; }
        public string SenderType { get; set; } = string.Empty;
        public string? SenderName { get; set; }
        public string? Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<MessageAttachmentItem> Attachments { get; set; } = new();
    }

    public class MessageAttachmentItem
    {
        public int AttachmentId { get; set; }
        public int MessageId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
    }
}
