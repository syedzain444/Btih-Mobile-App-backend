using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class MessagingRepository : IMessagingRepository
    {
        private readonly IConfiguration _configuration;

        public MessagingRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<int> CreateThreadAsync(CreateThreadRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var transaction = (OracleTransaction)await conn.BeginTransactionAsync();

            try
            {
                int threadId;
                await using (var threadCmd = new OracleCommand(@"
                    INSERT INTO PATIENT_MSG_THREAD (MR_NO, SUBJECT, CATEGORY)
                    VALUES (:mr_no, :subject, :category)
                    RETURNING THREAD_ID INTO :thread_id", conn))
                {
                    threadCmd.Transaction = transaction;
                    threadCmd.BindByName = true;
                    threadCmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));
                    threadCmd.Parameters.Add(new OracleParameter("subject", request.Subject));
                    threadCmd.Parameters.Add(new OracleParameter("category", request.Category ?? (object)DBNull.Value));

                    var threadOut = new OracleParameter("thread_id", OracleDbType.Int32)
                    {
                        Direction = System.Data.ParameterDirection.Output,
                    };
                    threadCmd.Parameters.Add(threadOut);

                    await threadCmd.ExecuteNonQueryAsync();
                    threadId = Convert.ToInt32(threadOut.Value.ToString());
                }

                if (!string.IsNullOrWhiteSpace(request.InitialMessage))
                {
                    await InsertMessageInternalAsync(
                        conn,
                        transaction,
                        threadId,
                        "PATIENT",
                        "Patient",
                        request.InitialMessage);
                }

                await transaction.CommitAsync();
                return threadId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MessageThreadSummary>> GetInboxAsync(string mrNo)
        {
            var threads = new List<MessageThreadSummary>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT
                    t.THREAD_ID,
                    t.MR_NO,
                    t.SUBJECT,
                    t.CATEGORY,
                    t.STATUS,
                    t.CREATED_AT,
                    t.UPDATED_AT,
                    lm.LAST_MESSAGE
                FROM PATIENT_MSG_THREAD t
                LEFT JOIN (
                    SELECT
                        THREAD_ID,
                        SUBSTR(BODY, 1, 200) AS LAST_MESSAGE,
                        ROW_NUMBER() OVER (PARTITION BY THREAD_ID ORDER BY CREATED_AT DESC) AS RN
                    FROM PATIENT_MSG
                ) lm
                    ON lm.THREAD_ID = t.THREAD_ID
                    AND lm.RN = 1
                WHERE t.MR_NO = :mr_no
                ORDER BY t.UPDATED_AT DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                threads.Add(new MessageThreadSummary
                {
                    ThreadId = Convert.ToInt32(reader["THREAD_ID"]),
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    Subject = reader["SUBJECT"]?.ToString() ?? string.Empty,
                    Category = reader["CATEGORY"]?.ToString(),
                    Status = reader["STATUS"]?.ToString() ?? "OPEN",
                    CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                    UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
                    LastMessagePreview = reader["LAST_MESSAGE"]?.ToString(),
                    UnreadCount = 0,
                });
            }

            return threads;
        }

        public async Task<bool> ThreadBelongsToPatientAsync(int threadId, string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_MSG_THREAD
                WHERE THREAD_ID = :thread_id
                  AND MR_NO = :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("thread_id", threadId));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<List<MessageItem>> GetMessagesAsync(int threadId, int skip, int take)
        {
            var messages = new List<MessageItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT *
                FROM (
                    SELECT inner_q.*, ROWNUM AS rn
                    FROM (
                        SELECT MESSAGE_ID, THREAD_ID, SENDER_TYPE, SENDER_NAME, BODY, CREATED_AT
                        FROM PATIENT_MSG
                        WHERE THREAD_ID = :thread_id
                        ORDER BY CREATED_AT ASC
                    ) inner_q
                    WHERE ROWNUM <= :max_row
                )
                WHERE rn > :skip", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("thread_id", threadId));
            cmd.Parameters.Add(new OracleParameter("max_row", skip + take));
            cmd.Parameters.Add(new OracleParameter("skip", skip));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var message = new MessageItem
                {
                    MessageId = Convert.ToInt32(reader["MESSAGE_ID"]),
                    ThreadId = Convert.ToInt32(reader["THREAD_ID"]),
                    SenderType = reader["SENDER_TYPE"]?.ToString() ?? string.Empty,
                    SenderName = reader["SENDER_NAME"]?.ToString(),
                    Body = reader["BODY"]?.ToString(),
                    CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                };

                message.Attachments = await GetAttachmentsAsync(conn, message.MessageId);
                messages.Add(message);
            }

            return messages;
        }

        public async Task<int> GetMessageCountAsync(int threadId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_MSG
                WHERE THREAD_ID = :thread_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("thread_id", threadId));

            await conn.OpenAsync();
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<int> AddMessageAsync(int threadId, string senderType, string? senderName, string? body)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            var messageId = await InsertMessageInternalAsync(conn, null, threadId, senderType, senderName, body);
            await TouchThreadInternalAsync(conn, threadId);
            return messageId;
        }

        public async Task<int> AddAttachmentAsync(
            int messageId,
            string fileName,
            string filePath,
            string? contentType,
            long fileSize)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO PATIENT_MSG_ATTACHMENT (
                    MESSAGE_ID, FILE_NAME, FILE_PATH, CONTENT_TYPE, FILE_SIZE
                ) VALUES (
                    :message_id, :file_name, :file_path, :content_type, :file_size
                )
                RETURNING ATTACHMENT_ID INTO :attachment_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("message_id", messageId));
            cmd.Parameters.Add(new OracleParameter("file_name", fileName));
            cmd.Parameters.Add(new OracleParameter("file_path", filePath));
            cmd.Parameters.Add(new OracleParameter("content_type", contentType ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("file_size", fileSize));

            var attachmentOut = new OracleParameter("attachment_id", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(attachmentOut);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(attachmentOut.Value.ToString());
        }

        public async Task TouchThreadAsync(int threadId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            await TouchThreadInternalAsync(conn, threadId);
        }

        private static async Task<int> InsertMessageInternalAsync(
            OracleConnection conn,
            OracleTransaction? transaction,
            int threadId,
            string senderType,
            string? senderName,
            string? body)
        {
            await using var cmd = new OracleCommand(@"
                INSERT INTO PATIENT_MSG (THREAD_ID, SENDER_TYPE, SENDER_NAME, BODY)
                VALUES (:thread_id, :sender_type, :sender_name, :body)
                RETURNING MESSAGE_ID INTO :message_id", conn);

            if (transaction != null)
            {
                cmd.Transaction = transaction;
            }

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("thread_id", threadId));
            cmd.Parameters.Add(new OracleParameter("sender_type", senderType));
            cmd.Parameters.Add(new OracleParameter("sender_name", senderName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("body", body ?? (object)DBNull.Value));

            var messageOut = new OracleParameter("message_id", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(messageOut);

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(messageOut.Value.ToString());
        }

        private static async Task TouchThreadInternalAsync(OracleConnection conn, int threadId)
        {
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_MSG_THREAD
                SET UPDATED_AT = SYSDATE
                WHERE THREAD_ID = :thread_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("thread_id", threadId));
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<List<MessageAttachmentItem>> GetAttachmentsAsync(OracleConnection conn, int messageId)
        {
            var attachments = new List<MessageAttachmentItem>();

            await using var cmd = new OracleCommand(@"
                SELECT ATTACHMENT_ID, MESSAGE_ID, FILE_NAME, FILE_PATH, CONTENT_TYPE, FILE_SIZE
                FROM PATIENT_MSG_ATTACHMENT
                WHERE MESSAGE_ID = :message_id
                ORDER BY ATTACHMENT_ID", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("message_id", messageId));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var filePath = reader["FILE_PATH"]?.ToString() ?? string.Empty;
                attachments.Add(new MessageAttachmentItem
                {
                    AttachmentId = Convert.ToInt32(reader["ATTACHMENT_ID"]),
                    MessageId = Convert.ToInt32(reader["MESSAGE_ID"]),
                    FileName = reader["FILE_NAME"]?.ToString() ?? string.Empty,
                    FileUrl = filePath.StartsWith('/') ? filePath : $"/{filePath.TrimStart('/')}",
                    ContentType = reader["CONTENT_TYPE"]?.ToString(),
                    FileSize = reader["FILE_SIZE"] != DBNull.Value
                        ? Convert.ToInt64(reader["FILE_SIZE"])
                        : null,
                });
            }

            return attachments;
        }
    }
}
