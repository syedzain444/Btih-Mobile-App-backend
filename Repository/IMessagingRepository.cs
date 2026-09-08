using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IMessagingRepository
    {
        Task<int> CreateThreadAsync(CreateThreadRequest request);
        Task<List<MessageThreadSummary>> GetInboxAsync(string mrNo);
        Task<bool> ThreadBelongsToPatientAsync(int threadId, string mrNo);
        Task<List<MessageItem>> GetMessagesAsync(int threadId, int skip, int take);
        Task<int> GetMessageCountAsync(int threadId);
        Task<int> AddMessageAsync(int threadId, string senderType, string? senderName, string? body);
        Task<int> AddAttachmentAsync(int messageId, string fileName, string filePath, string? contentType, long fileSize);
        Task TouchThreadAsync(int threadId);
    }
}
