using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IMessagingService
    {
        Task<int> CreateThreadAsync(CreateThreadRequest request);
        Task<List<MessageThreadSummary>> GetInboxAsync(string mrNo);
        Task<PagedResult<MessageItem>> GetMessagesAsync(int threadId, string mrNo, int pageNumber, int pageSize);
        Task<MessageItem> SendMessageAsync(int threadId, SendMessageRequest request);
        Task<MessageItem> SendAttachmentAsync(int threadId, string mrNo, string? body, IFormFile file, string webRootPath, string uploadSubPath, long maxBytes, IEnumerable<string> allowedExtensions);
    }
}
