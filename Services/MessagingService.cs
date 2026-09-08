using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class MessagingService : IMessagingService
    {
        private readonly IMessagingRepository _repository;

        public MessagingService(IMessagingRepository repository)
        {
            _repository = repository;
        }

        public Task<int> CreateThreadAsync(CreateThreadRequest request)
        {
            return _repository.CreateThreadAsync(request);
        }

        public Task<List<MessageThreadSummary>> GetInboxAsync(string mrNo)
        {
            return _repository.GetInboxAsync(mrNo);
        }

        public async Task<PagedResult<MessageItem>> GetMessagesAsync(
            int threadId,
            string mrNo,
            int pageNumber,
            int pageSize)
        {
            if (!await _repository.ThreadBelongsToPatientAsync(threadId, mrNo))
            {
                return new PagedResult<MessageItem>
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Data = new List<MessageItem>(),
                };
            }

            var total = await _repository.GetMessageCountAsync(threadId);
            var skip = (pageNumber - 1) * pageSize;
            var data = await _repository.GetMessagesAsync(threadId, skip, pageSize);

            return new PagedResult<MessageItem>
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0,
                Data = data,
            };
        }

        public async Task<MessageItem> SendMessageAsync(int threadId, SendMessageRequest request)
        {
            if (!await _repository.ThreadBelongsToPatientAsync(threadId, request.MrNo))
            {
                throw new InvalidOperationException("Thread not found for patient");
            }

            var messageId = await _repository.AddMessageAsync(threadId, "PATIENT", "Patient", request.Body);

            return new MessageItem
            {
                MessageId = messageId,
                ThreadId = threadId,
                SenderType = "PATIENT",
                SenderName = "Patient",
                Body = request.Body,
                CreatedAt = DateTime.Now,
                Attachments = new List<MessageAttachmentItem>(),
            };
        }

        public async Task<MessageItem> SendAttachmentAsync(
            int threadId,
            string mrNo,
            string? body,
            IFormFile file,
            string webRootPath,
            string uploadSubPath,
            long maxBytes,
            IEnumerable<string> allowedExtensions)
        {
            if (!await _repository.ThreadBelongsToPatientAsync(threadId, mrNo))
            {
                throw new InvalidOperationException("Thread not found for patient");
            }

            if (file.Length <= 0)
            {
                throw new InvalidOperationException("Attachment file is empty");
            }

            if (file.Length > maxBytes)
            {
                throw new InvalidOperationException($"Attachment exceeds maximum size of {maxBytes} bytes");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"File type {extension} is not allowed");
            }

            var safeMr = mrNo.Replace('/', '_').Replace('\\', '_');
            var folder = Path.Combine(webRootPath, uploadSubPath, safeMr);
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(folder, storedName);
            await using (var stream = File.Create(physicalPath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"{uploadSubPath.Trim('/')}/{safeMr}/{storedName}".Replace('\\', '/');
            var messageId = await _repository.AddMessageAsync(threadId, "PATIENT", "Patient", body);
            var attachmentId = await _repository.AddAttachmentAsync(
                messageId,
                file.FileName,
                relativePath,
                file.ContentType,
                file.Length);

            return new MessageItem
            {
                MessageId = messageId,
                ThreadId = threadId,
                SenderType = "PATIENT",
                SenderName = "Patient",
                Body = body,
                CreatedAt = DateTime.Now,
                Attachments = new List<MessageAttachmentItem>
                {
                    new()
                    {
                        AttachmentId = attachmentId,
                        MessageId = messageId,
                        FileName = file.FileName,
                        FileUrl = $"/{relativePath}",
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                    },
                },
            };
        }
    }
}
