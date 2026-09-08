using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class ReminderRepository : IReminderRepository
    {
        private readonly IConfiguration _configuration;

        public ReminderRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<MedicationReminderItem>> GetRemindersAsync(string mrNo)
        {
            var reminders = new List<MedicationReminderItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT REMINDER_ID, MR_NO, MEDICATION_ID, MEDICATION_NAME, REMINDER_TIME,
                       DAYS_OF_WEEK, IS_ENABLED, CREATED_AT, UPDATED_AT
                FROM PATIENT_MED_REMINDER
                WHERE MR_NO = :mr_no
                ORDER BY REMINDER_TIME", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                reminders.Add(MapReminder(reader));
            }

            return reminders;
        }

        public async Task<int> CreateReminderAsync(MedicationReminderRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO PATIENT_MED_REMINDER (
                    MR_NO, MEDICATION_ID, MEDICATION_NAME, REMINDER_TIME, DAYS_OF_WEEK, IS_ENABLED
                ) VALUES (
                    :mr_no, :medication_id, :medication_name, :reminder_time, :days_of_week, :is_enabled
                )
                RETURNING REMINDER_ID INTO :reminder_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));
            cmd.Parameters.Add(new OracleParameter("medication_id", request.MedicationId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("medication_name", request.MedicationName));
            cmd.Parameters.Add(new OracleParameter("reminder_time", request.ReminderTime));
            cmd.Parameters.Add(new OracleParameter("days_of_week", request.DaysOfWeek ?? "1234567"));
            cmd.Parameters.Add(new OracleParameter("is_enabled", request.IsEnabled ? "Y" : "N"));

            var reminderOut = new OracleParameter("reminder_id", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(reminderOut);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(reminderOut.Value.ToString());
        }

        public async Task<bool> UpdateReminderAsync(int reminderId, UpdateMedicationReminderRequest request)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_MED_REMINDER
                SET MEDICATION_NAME = NVL(:medication_name, MEDICATION_NAME),
                    REMINDER_TIME = NVL(:reminder_time, REMINDER_TIME),
                    DAYS_OF_WEEK = NVL(:days_of_week, DAYS_OF_WEEK),
                    IS_ENABLED = NVL(:is_enabled, IS_ENABLED),
                    UPDATED_AT = SYSDATE
                WHERE REMINDER_ID = :reminder_id
                  AND MR_NO = :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("medication_name", request.MedicationName ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("reminder_time", request.ReminderTime ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("days_of_week", request.DaysOfWeek ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("is_enabled", request.IsEnabled.HasValue
                ? (request.IsEnabled.Value ? "Y" : "N")
                : (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("reminder_id", reminderId));
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteReminderAsync(int reminderId, string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                DELETE FROM PATIENT_MED_REMINDER
                WHERE REMINDER_ID = :reminder_id
                  AND MR_NO = :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("reminder_id", reminderId));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<List<DueMedicationReminder>> GetDueRemindersAsync(string currentTime, int dayOfWeek)
        {
            var due = new List<DueMedicationReminder>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT REMINDER_ID, MR_NO, MEDICATION_NAME, REMINDER_TIME
                FROM PATIENT_MED_REMINDER
                WHERE IS_ENABLED = 'Y'
                  AND REMINDER_TIME = :current_time
                  AND INSTR(DAYS_OF_WEEK, :day_of_week) > 0
                  AND (LAST_TRIGGERED_DATE IS NULL OR TRUNC(LAST_TRIGGERED_DATE) < TRUNC(SYSDATE))", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("current_time", currentTime));
            cmd.Parameters.Add(new OracleParameter("day_of_week", dayOfWeek.ToString()));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                due.Add(new DueMedicationReminder
                {
                    ReminderId = Convert.ToInt32(reader["REMINDER_ID"]),
                    MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                    MedicationName = reader["MEDICATION_NAME"]?.ToString() ?? string.Empty,
                    ReminderTime = reader["REMINDER_TIME"]?.ToString() ?? string.Empty,
                });
            }

            return due;
        }

        public async Task MarkReminderTriggeredAsync(int reminderId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE PATIENT_MED_REMINDER
                SET LAST_TRIGGERED_DATE = SYSDATE,
                    UPDATED_AT = SYSDATE
                WHERE REMINDER_ID = :reminder_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("reminder_id", reminderId));

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private static MedicationReminderItem MapReminder(OracleDataReader reader)
        {
            return new MedicationReminderItem
            {
                ReminderId = Convert.ToInt32(reader["REMINDER_ID"]),
                MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                MedicationId = reader["MEDICATION_ID"] != DBNull.Value
                    ? Convert.ToInt32(reader["MEDICATION_ID"])
                    : null,
                MedicationName = reader["MEDICATION_NAME"]?.ToString() ?? string.Empty,
                ReminderTime = reader["REMINDER_TIME"]?.ToString() ?? string.Empty,
                DaysOfWeek = reader["DAYS_OF_WEEK"]?.ToString() ?? "1234567",
                IsEnabled = (reader["IS_ENABLED"]?.ToString() ?? "Y") == "Y",
                CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
            };
        }
    }
}
