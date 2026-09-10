using Dapper;
using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;
using System.Net;
using System.Text;

namespace HospitalMobileAPPApi.Repository
{
    public class PatientRepository : IPatientRepository
    {
        private readonly IConfiguration _configuration;
        public PatientRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task<PatientProfileResponse?> GetPatientProfileAsync(string MR_NO, int visitPageNumber, int visitPageSize)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            PatientDetails? profile = null;

            using (var profileCmd = new OracleCommand(@"
                SELECT
                    pm.MR_NO,
                    pm.FIRST_NAME,
                    pm.LAST_NAME,
                    pm.GENDER,
                    pi.DT_DOB,
                    pi.CNIC,
                    pi.CONTACT_NO,
                    pi.BLOOD_GROUP,
                    pi.EMAIL_ADDRESS
                FROM PATIENT_MST pm
                INNER JOIN PATIENT_INFORMATION pi
                    ON pi.MR_NO = pm.MR_NO
                WHERE pm.MR_NO = :mr_no", conn))
            {
                profileCmd.BindByName = true;
                profileCmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));

                using var reader = await profileCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                profile = new PatientDetails
                {
                    MrNo = reader["MR_NO"]?.ToString(),
                    FirstName = reader["FIRST_NAME"]?.ToString(),
                    LastName = reader["LAST_NAME"]?.ToString(),
                    Gender = reader["GENDER"]?.ToString(),
                    DateOfBirth = ReadDateTime(reader, "DT_DOB"),
                    CNIC = reader["CNIC"]?.ToString(),
                    ContactNo = reader["CONTACT_NO"]?.ToString(),
                    BloodGroup = reader["BLOOD_GROUP"]?.ToString(),
                    EmailAddress = reader["EMAIL_ADDRESS"]?.ToString(),
                };
            }

            var totalRecords = await GetVisitHistoryCountAsync(MR_NO, conn);
            var skip = (visitPageNumber - 1) * visitPageSize;
            var visitHistory = new List<PatientVisitHistoryItem>();

            using (var visitCmd = new OracleCommand(@"
                SELECT *
                FROM (
                    SELECT inner_q.*, ROWNUM AS rn
                    FROM (
                        SELECT
                            pv.PATIENT_VISIT_ID,
                            pv.VISIT_DATE,
                            pv.CHECK_IN,
                            pv.DR_OUT,
                            pv.DOCTOR_ID,
                            TRIM(d.FIRST_NAME || ' ' || NVL(d.LAST_NAME, '')) AS DOCTOR_NAME,
                            TRIM(adm.FIRST_NAME || ' ' || NVL(adm.LAST_NAME, '')) AS ADMISSION_OFFICER,
                            pr.DEPARTMENT,
                            COALESCE(
                                NULLIF(TRIM(d.FIRST_NAME || ' ' || NVL(d.LAST_NAME, '')), ''),
                                pr.DOCTOR
                            ) AS DOCTOR_DISPLAY_NAME,
                            CASE
                                WHEN pd.PATIENT_VISIT_ID IS NOT NULL AND NVL(pd.IS_CANCEL, 'N') = 'N' THEN 1
                                ELSE 0
                            END AS IS_DISCHARGED,
                            pd.DISCHARGE_ID
                        FROM PATIENT_VISIT pv
                        LEFT JOIN EMPLOYEE_MST d
                            ON d.EMPLOYEE_ID = pv.DOCTOR_ID
                        LEFT JOIN EMPLOYEE_MST adm
                            ON adm.EMPLOYEE_ID = pv.CREATED_BY
                        LEFT JOIN PATIENT_DISCHARGE pd
                            ON pd.PATIENT_VISIT_ID = pv.PATIENT_VISIT_ID
                            AND NVL(pd.IS_CANCEL, 'N') = 'N'
                        LEFT JOIN (
                            SELECT
                                PATIENT_VISIT_ID,
                                MAX(DEPARTMENT) AS DEPARTMENT,
                                MAX(DOCTOR) AS DOCTOR
                            FROM PRESCRIPTIONS
                            WHERE MR_NO = :mr_no
                            GROUP BY PATIENT_VISIT_ID
                        ) pr
                            ON pr.PATIENT_VISIT_ID = pv.PATIENT_VISIT_ID
                        WHERE pv.MR_NO = :mr_no
                        ORDER BY NVL(pv.VISIT_DATE, pv.CHECK_IN) DESC NULLS LAST
                    ) inner_q
                    WHERE ROWNUM <= :max_row
                )
                WHERE rn > :skip", conn))
            {
                visitCmd.BindByName = true;
                visitCmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));
                visitCmd.Parameters.Add(new OracleParameter("max_row", skip + visitPageSize));
                visitCmd.Parameters.Add(new OracleParameter("skip", skip));

                using var reader = await visitCmd.ExecuteReaderAsync();
                var serial = skip + 1;
                while (await reader.ReadAsync())
                {
                    visitHistory.Add(new PatientVisitHistoryItem
                    {
                        SerialNumber = serial++,
                        PatientVisitId = ReadInt(reader, "PATIENT_VISIT_ID"),
                        VisitDate = ReadDateTime(reader, "VISIT_DATE"),
                        CheckIn = ReadDateTime(reader, "CHECK_IN"),
                        DischargeDate = ReadDateTime(reader, "DR_OUT"),
                        DoctorId = ReadNullableInt(reader, "DOCTOR_ID"),
                        DoctorName = reader["DOCTOR_DISPLAY_NAME"]?.ToString(),
                        Department = reader["DEPARTMENT"]?.ToString(),
                        AdmissionOfficer = reader["ADMISSION_OFFICER"]?.ToString(),
                        AdmissionNo = null,
                        Disease = null,
                        PresentingComplaints = null,
                        IsDischarged = ReadInt(reader, "IS_DISCHARGED") == 1,
                        DischargeId = ReadNullableInt(reader, "DISCHARGE_ID"),
                    });
                }
            }

            return new PatientProfileResponse
            {
                Profile = profile,
                VisitHistory = new PagedResult<PatientVisitHistoryItem>
                {
                    PageNumber = visitPageNumber,
                    PageSize = visitPageSize,
                    TotalRecords = totalRecords,
                    TotalPages = visitPageSize > 0 ? (int)Math.Ceiling((double)totalRecords / visitPageSize) : 0,
                    Data = visitHistory,
                },
            };
        }

        public async Task<int> GetVisitHistoryCountAsync(string MR_NO)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();
            return await GetVisitHistoryCountAsync(MR_NO, conn);
        }

        private static async Task<int> GetVisitHistoryCountAsync(string MR_NO, OracleConnection conn)
        {
            await using var cmd = new OracleCommand(@"
                SELECT COUNT(*)
                FROM PATIENT_VISIT pv
                WHERE pv.MR_NO = :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<List<PatientProfile>> GetPatientAsync(string MR_NO)
        {
            var response = await GetPatientProfileAsync(MR_NO, 1, 1000);
            if (response == null)
            {
                return new List<PatientProfile>();
            }

            if (!response.VisitHistory.Data.Any())
            {
                return new List<PatientProfile>
                {
                    new PatientProfile
                    {
                        SerialNumber = 1,
                        FirstName = response.Profile.FirstName,
                        LastName = response.Profile.LastName,
                        Gender = response.Profile.Gender,
                        DateOfBirth = response.Profile.DateOfBirth,
                        CNIC = response.Profile.CNIC,
                        ContactNo = response.Profile.ContactNo,
                        BloodGroup = response.Profile.BloodGroup,
                        EmailAddress = response.Profile.EmailAddress,
                    }
                };
            }

            return response.VisitHistory.Data.Select(visit => new PatientProfile
            {
                SerialNumber = visit.SerialNumber,
                FirstName = response.Profile.FirstName,
                LastName = response.Profile.LastName,
                Gender = response.Profile.Gender,
                VisitDate = visit.VisitDate ?? visit.CheckIn,
                DateOfBirth = response.Profile.DateOfBirth,
                CNIC = response.Profile.CNIC,
                ContactNo = response.Profile.ContactNo,
                BloodGroup = response.Profile.BloodGroup,
                EmailAddress = response.Profile.EmailAddress,
                DoctorName = visit.DoctorName,
            }).ToList();
        }

        private static DateTime? ReadDateTime(OracleDataReader reader, string column)
        {
            var value = reader[column];
            return value == DBNull.Value ? null : Convert.ToDateTime(value);
        }

        private static int ReadInt(OracleDataReader reader, string column)
        {
            var value = reader[column];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static int? ReadNullableInt(OracleDataReader reader, string column)
        {
            var value = reader[column];
            return value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        public async Task<List<LabReportModel>> GetPatientReports(string MR_NO)
        {
            return await GetReportsByTestTypeAsync(MR_NO, "LABORATORY");
        }

        public async Task<List<LabReportModel>> GetGastroReports(string MR_NO)
        {
            return await GetReportsByTestTypeFilterAsync(
                MR_NO,
                @"(
                    UPPER(TRIM(TESTTYPE)) = 'GASTRO'
                    OR UPPER(TRIM(TESTTYPE)) LIKE '%GASTRO%'
                    OR UPPER(TRIM(TESTTYPE)) = 'ENDOSCOPY'
                )");
        }

        public async Task<List<LabReportModel>> GetRadiology(string MR_NO)
        {
            return await GetReportsByTestTypeFilterAsync(
                MR_NO,
                @"(
                    UPPER(TRIM(TESTTYPE)) NOT IN ('LABORATORY', 'GASTRO', 'ENDOSCOPY')
                    AND UPPER(TRIM(TESTTYPE)) NOT LIKE '%GASTRO%'
                )");
        }

        private async Task<List<LabReportModel>> GetReportsByTestTypeFilterAsync(string MR_NO, string testTypeFilterSql)
        {
            var reports = new List<LabReportModel>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            using var conn = new OracleConnection(connStr);
            using var cmd = new OracleCommand($@"
                SELECT
                    DT_SAMPLECOLLECTION,
                    PAT_DIAG_ID,
                    MODALITY_NM,
                    DIAGNOSTIC_NAME,
                    TESTTYPE
                FROM hmis.V_LABORATORY_GEN
                WHERE DT_SAMPLECOLLECTION IS NOT NULL
                  AND {testTypeFilterSql}
                  AND MR_NO = :mr_no
                  AND REPORT_STATUS = 'REPORT GENERATED'
                ORDER BY PAT_DIAG_ID DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                reports.Add(MapLabReport(reader));
            }

            return reports;
        }

        private async Task<List<LabReportModel>> GetReportsByTestTypeAsync(string MR_NO, string testType)
        {
            var reports = new List<LabReportModel>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            using var conn = new OracleConnection(connStr);
            using var cmd = new OracleCommand(@"
                SELECT
                    DT_SAMPLECOLLECTION,
                    PAT_DIAG_ID,
                    MODALITY_NM,
                    DIAGNOSTIC_NAME,
                    TESTTYPE
                FROM hmis.V_LABORATORY_GEN
                WHERE DT_SAMPLECOLLECTION IS NOT NULL
                  AND UPPER(TRIM(TESTTYPE)) = UPPER(TRIM(:test_type))
                  AND MR_NO = :mr_no
                  AND REPORT_STATUS = 'REPORT GENERATED'
                ORDER BY PAT_DIAG_ID DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("test_type", testType));
            cmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                reports.Add(MapLabReport(reader));
            }

            return reports;
        }

        private static LabReportModel MapLabReport(OracleDataReader reader)
        {
            return new LabReportModel
            {
                DT_SAMPLECOLLECTION = reader["DT_SAMPLECOLLECTION"] != DBNull.Value
                    ? Convert.ToDateTime(reader["DT_SAMPLECOLLECTION"])
                    : null,
                PAT_DIAG_ID = reader["PAT_DIAG_ID"] != DBNull.Value
                    ? Convert.ToInt32(reader["PAT_DIAG_ID"])
                    : 0,
                MODALITY_NM = reader["MODALITY_NM"]?.ToString(),
                DIAGNOSTIC_NAME = reader["DIAGNOSTIC_NAME"]?.ToString(),
                TESTTYPE = reader["TESTTYPE"]?.ToString(),
            };
        }

        public async Task<List<PrescriptionModel>> GetPrescriptions(string MR_NO)
        {
            var reports = new List<PrescriptionModel>();

            var connStr = _configuration.GetConnectionString("HMISConnection");

            using var conn = new OracleConnection(connStr);
            using var cmd = new OracleCommand(@"SELECT PATIENT_VISIT_ID, DOCTOR, VISIT_DATE, DEPARTMENT
                                        FROM PRESCRIPTIONS
                                        WHERE MR_NO = :mr_no
                                        GROUP BY DOCTOR, VISIT_DATE, DEPARTMENT, PATIENT_VISIT_ID
                                        ORDER BY VISIT_DATE DESC NULLS LAST", conn);

            cmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                reports.Add(new PrescriptionModel
                {
                    PAT_VISIT_ID = reader["PATIENT_VISIT_ID"] != DBNull.Value
                        ? Convert.ToInt32(reader["PATIENT_VISIT_ID"])
                        : 0,

                    DOCTOR = reader["DOCTOR"]?.ToString(),
                    VISIT_DATE = reader["VISIT_DATE"] != DBNull.Value
                    ? Convert.ToDateTime(reader["VISIT_DATE"])
                    : (DateTime?)null,
                    DEPARTMENT = reader["DEPARTMENT"]?.ToString(),

                });
            }

            return reports;
        }

        private async Task SendSms(string number, string message)
        {
            try
            {
                string urlSms = "http://172.20.10.50:81/api/values/?_Send_to=";
                string apiUrl = urlSms + number + "&_Msg=" + WebUtility.UrlEncode(message);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "GET";
                request.ContentType = "application/json";

                using HttpWebResponse response = await request.GetResponseAsync() as HttpWebResponse;
                using StreamReader reader = new StreamReader(response.GetResponseStream());
                string result = await reader.ReadToEndAsync();

            }
            catch (Exception ex)
            {

            }

        }

        //private async Task<string> SendSms(string number, string message)
        //{
        //    string returnMsg = "";

        //    try
        //    {
        //        string url = "http://172.20.10.50:81/api/values";

        //        var requestBody = new
        //        {
        //            _Send_to = number,
        //            from = "93000",
        //            _Msg = message
        //        };

        //        string json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);

        //        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        //        request.Method = "POST";
        //        request.ContentType = "application/json";

        //        byte[] data = Encoding.UTF8.GetBytes(json);
        //        request.ContentLength = data.Length;

        //        using (Stream stream = await request.GetRequestStreamAsync())
        //        {
        //            await stream.WriteAsync(data, 0, data.Length);
        //        }

        //        using HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync();

        //        using StreamReader reader = new StreamReader(response.GetResponseStream());
        //        returnMsg = await reader.ReadToEndAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        returnMsg = ex.Message;
        //    }

        //    return returnMsg;
        //}

        public async Task<int> InsertAppointment(AppointmentModel model)
        {
            if (string.IsNullOrWhiteSpace(model.phoneNo))
            {
                return 0;
            }

            try
            {
                var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");

                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmdInsert = new OracleCommand(@"
            INSERT INTO APPOINTMENT (
                NAME, PHONE, MRNUM, EMAIL, WEEK_ID, APPOINTMENTTIME, STATUS, DOCTOR_ID, DEPARTMENT_ID, PURPOSE, CREATED_AT, IS_ACTIVE, ENTRY_DATE
            ) VALUES (
                :name, :phone, :mrno, :email, :week_id, :appointment_time, :status, :doctor_id, :department_id, :purpose, :created_at, :is_active, :entry_date
            )", conn);

                cmdInsert.BindByName = true;

                cmdInsert.Parameters.Add("name", OracleDbType.Varchar2).Value = model.name ?? string.Empty;
                cmdInsert.Parameters.Add("phone", OracleDbType.Varchar2).Value = model.phoneNo;
                cmdInsert.Parameters.Add("mrno", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(model.mrno) ? (object)DBNull.Value : model.mrno;
                cmdInsert.Parameters.Add("email", OracleDbType.Varchar2).Value = model.email ?? (object)DBNull.Value;
                cmdInsert.Parameters.Add("week_id", OracleDbType.Int32).Value = model.weekId;
                cmdInsert.Parameters.Add("appointment_time", OracleDbType.Varchar2).Value = model.appointment_time;
                cmdInsert.Parameters.Add("status", OracleDbType.Varchar2).Value = model.status;
                cmdInsert.Parameters.Add("doctor_id", OracleDbType.Int32).Value = model.doctorId;
                cmdInsert.Parameters.Add("department_id", OracleDbType.Int32).Value = model.departmentId;
                cmdInsert.Parameters.Add("purpose", OracleDbType.Varchar2).Value = model.purpose;
                cmdInsert.Parameters.Add("created_at", OracleDbType.Date).Value = model.createdAt ?? DateTime.Now;
                cmdInsert.Parameters.Add("is_active", OracleDbType.Char).Value = model.isActive ?? "Y";
                cmdInsert.Parameters.Add("entry_date", OracleDbType.Date).Value = model.entryDate ?? DateTime.Now;

                var number = await cmdInsert.ExecuteNonQueryAsync();
                if (number > 0)
                {
                    const string sms = "Your appointment request has been received. We will update you later.";
                    await SendSms(model.phoneNo, sms);
                }

                return number;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> UpdatePatientProfileAsync(UpdatePatientProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return false;
            }

            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            await using var transaction = conn.BeginTransaction();

            try
            {
                await using (var mstCmd = new OracleCommand(@"
                    UPDATE PATIENT_MST
                    SET FIRST_NAME = :first_name,
                        LAST_NAME = :last_name,
                        GENDER = :gender
                    WHERE MR_NO = :mr_no", conn))
                {
                    mstCmd.Transaction = transaction;
                    mstCmd.BindByName = true;
                    mstCmd.Parameters.Add(new OracleParameter("first_name", request.FirstName ?? (object)DBNull.Value));
                    mstCmd.Parameters.Add(new OracleParameter("last_name", request.LastName ?? (object)DBNull.Value));
                    mstCmd.Parameters.Add(new OracleParameter("gender", NormalizeGender(request.Gender) ?? (object)DBNull.Value));
                    mstCmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));

                    var mstRows = await mstCmd.ExecuteNonQueryAsync();
                    if (mstRows == 0)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }
                }

                await using (var infoCmd = new OracleCommand(@"
                    UPDATE PATIENT_INFORMATION
                    SET DT_DOB = :date_of_birth,
                        CNIC = :cnic,
                        CONTACT_NO = :contact_no,
                        BLOOD_GROUP = :blood_group,
                        EMAIL_ADDRESS = :email_address
                    WHERE MR_NO = :mr_no", conn))
                {
                    infoCmd.Transaction = transaction;
                    infoCmd.BindByName = true;
                    infoCmd.Parameters.Add(new OracleParameter("date_of_birth", request.DateOfBirth ?? (object)DBNull.Value));
                    infoCmd.Parameters.Add(new OracleParameter("cnic", request.CNIC ?? (object)DBNull.Value));
                    infoCmd.Parameters.Add(new OracleParameter("contact_no", request.ContactNo ?? (object)DBNull.Value));
                    infoCmd.Parameters.Add(new OracleParameter("blood_group", request.BloodGroup ?? (object)DBNull.Value));
                    infoCmd.Parameters.Add(new OracleParameter("email_address", request.EmailAddress ?? (object)DBNull.Value));
                    infoCmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));

                    var infoRows = await infoCmd.ExecuteNonQueryAsync();
                    if (infoRows == 0)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static string? NormalizeGender(string? gender)
        {
            if (string.IsNullOrWhiteSpace(gender))
            {
                return null;
            }

            var value = gender.Trim().ToUpperInvariant();
            return value switch
            {
                "M" or "MALE" => "M",
                "F" or "FEMALE" => "F",
                _ => gender.Trim(),
            };
        }

        public async Task<int> UpdatePatientPassword(string mrno, string patientPassword)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");
            int number = 0;
            try
            {
                await using var conn = new OracleConnection(connStr);
                await using var cmd = new OracleCommand(@"
            UPDATE PATIENT_MST
            SET PATIENT_PASSWORD = :patientPassword
            WHERE MR_NO = :mrno
        ", conn);

                cmd.Parameters.Add(new OracleParameter("patientPassword", patientPassword));
                cmd.Parameters.Add(new OracleParameter("mrno", mrno));

                await conn.OpenAsync();

                return await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {

            }
            return number;

        }

        public async Task<List<PatientAppointment>> GetAppointments(string MR_NO)
        {
            var doctors = new List<PatientAppointment>();
            int serial = 1;

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");

            using var conn = new OracleConnection(connStr);
            using var cmd = new OracleCommand(@"
                SELECT a.*, d.DOCTOR_NAME FROM APPOINTMENT a
            INNER JOIN DOCTOR d
            ON a.DOCTOR_ID = d.DOCTOR_ID WHERE MRNUM = :mr_no ORDER BY CREATED_AT DESC", conn);
            cmd.Parameters.Add(new OracleParameter("mr_no", MR_NO));
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {

                doctors.Add(new PatientAppointment
                {
                    //SerialNumber = serial++,
                    AppointmentId = reader["APPOINTMENT_ID"]?.ToString(),
                    Name = reader["NAME"]?.ToString(),
                    PhoneNo = reader["PHONE"]?.ToString(),
                    MRNo = reader["MRNUM"]?.ToString(),
                    Email = reader["EMAIL"]?.ToString(),
                    weekId = Convert.ToInt32(reader["WEEK_ID"]),
                    AppointmentTime = reader["APPOINTMENTTIME"]?.ToString(),
                    Status = reader["STATUS"]?.ToString(),
                    DoctorName = reader["DOCTOR_NAME"]?.ToString(),
                    DoctorId = reader["DOCTOR_ID"] != DBNull.Value
                        ? Convert.ToInt32(reader["DOCTOR_ID"])
                        : 0,
                    DepartmentId = reader["DEPARTMENT_ID"] != DBNull.Value
                        ? Convert.ToInt32(reader["DEPARTMENT_ID"])
                        : 0,
                    purpose = reader["PURPOSE"]?.ToString(),
                    CreatedAt = reader["CREATED_AT"] != DBNull.Value
                        ? DateTime.Parse(reader["CREATED_AT"].ToString())
                        : (DateTime?)null,
                });
            }

            return doctors;
        }

        public async Task<int> CancelAppointmentAsync(string appointmentId, string mrNo, string reason)
        {
            if (string.IsNullOrWhiteSpace(appointmentId) || string.IsNullOrWhiteSpace(mrNo))
            {
                return 0;
            }

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            var note = $"[CANCELLED BY PATIENT: {reason.Trim()}]";

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE APPOINTMENT
                SET STATUS = 'Cancelled',
                    IS_ACTIVE = 'N',
                    PURPOSE = CASE
                        WHEN PURPOSE IS NULL OR TRIM(PURPOSE) = '' THEN :note
                        ELSE PURPOSE || ' | ' || :note
                    END
                WHERE APPOINTMENT_ID = :appointment_id
                  AND MRNUM = :mr_no
                  AND NVL(IS_ACTIVE, 'Y') = 'Y'
                  AND UPPER(NVL(STATUS, 'PENDING')) NOT IN ('CANCELLED', 'COMPLETED')", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("note", OracleDbType.Varchar2).Value = note;
            cmd.Parameters.Add("appointment_id", OracleDbType.Varchar2).Value = appointmentId.Trim();
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = mrNo.Trim();

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> RequestRescheduleAsync(
            string appointmentId,
            RescheduleAppointmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(appointmentId) || string.IsNullOrWhiteSpace(request.MrNo))
            {
                return 0;
            }

            var connStr = _configuration.GetConnectionString("HOS_WEB_MVC_LIVE");
            var note =
                $"[RESCHEDULE REQUEST: weekId={request.WeekId}, time={request.AppointmentTime}, reason={request.Reason.Trim()}]";

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                UPDATE APPOINTMENT
                SET STATUS = 'Reschedule Pending',
                    PURPOSE = CASE
                        WHEN PURPOSE IS NULL OR TRIM(PURPOSE) = '' THEN :note
                        ELSE PURPOSE || ' | ' || :note
                    END
                WHERE APPOINTMENT_ID = :appointment_id
                  AND MRNUM = :mr_no
                  AND NVL(IS_ACTIVE, 'Y') = 'Y'
                  AND UPPER(NVL(STATUS, 'PENDING')) NOT IN ('CANCELLED', 'COMPLETED', 'RESCHEDULE PENDING')", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add("note", OracleDbType.Varchar2).Value = note;
            cmd.Parameters.Add("appointment_id", OracleDbType.Varchar2).Value = appointmentId.Trim();
            cmd.Parameters.Add("mr_no", OracleDbType.Varchar2).Value = request.MrNo.Trim();

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<PatientDischargeHistory>> GetDischargeHistory(string MR_NO, int skip, int take)
        {
            try
            {
                using var conn = new OracleConnection(_configuration.GetConnectionString("HMISConnection"));

                string query = @"
   SELECT * FROM (
        SELECT 
            a.*,
            ROWNUM AS rn
        FROM (
            SELECT p.MR_NO, p.PATIENT_VISIT_ID, p.CHECK_IN, p.DR_OUT, 
d.FIRST_NAME AS DOCTOR_NAME, a.FIRST_NAME  AS ADMISSION_OFFICER
FROM PATIENT_VISIT p INNER JOIN
EMPLOYEE_MST d 
ON p.DOCTOR_ID = d.EMPLOYEE_ID 
INNER JOIN EMPLOYEE_MST a 
ON a.EMPLOYEE_ID = p.CREATED_BY
INNER JOIN PATIENT_DISCHARGE pd
ON pd.PATIENT_VISIT_ID = p.PATIENT_VISIT_ID
WHERE MR_NO = :MR_NO AND DR_OUT IS NOT NULL AND pd.PATIENT_VISIT_ID IS NOT NULL AND NVL(pd.IS_CANCEL, 'N') = 'N' ORDER BY p.DR_OUT DESC
        ) a
        WHERE ROWNUM <= :skip + :take
    )
    WHERE rn > :skip";

                var result = await conn.QueryAsync<PatientDischargeHistory>(query, new { MR_NO, skip, take });

                return result.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in GetDischargeHistory: " + ex.Message);
                throw;
            }
        }

        public async Task<int> GetDischargeHistoryCountAsync(string MR_NO)
        {
            using var conn = new OracleConnection(_configuration.GetConnectionString("HMISConnection"));

            const string query = @"
                SELECT COUNT(*)
                FROM PATIENT_VISIT p
                INNER JOIN PATIENT_DISCHARGE pd
                    ON pd.PATIENT_VISIT_ID = p.PATIENT_VISIT_ID
                WHERE p.MR_NO = :MR_NO
                  AND p.DR_OUT IS NOT NULL
                  AND pd.PATIENT_VISIT_ID IS NOT NULL
                  AND NVL(pd.IS_CANCEL, 'N') = 'N'";

            return await conn.ExecuteScalarAsync<int>(query, new { MR_NO });
        }
        //public async Task<List<PatientDischargeHistory>> GetDischargeHistory(string MR_NO)
        //{
        //    try
        //    {
        //        using var conn = new OracleConnection(_configuration.GetConnectionString("HMISConnection"));

        //        string query = @"
        //    SELECT 
        //        p.MR_NO,
        //        p.PATIENT_VISIT_ID,
        //        p.CHECK_IN,
        //        p.DR_OUT,
        //        d.FIRST_NAME AS DOCTOR_NAME,
        //        a.FIRST_NAME AS ADMISSION_OFFICER
        //    FROM PATIENT_VISIT p
        //    INNER JOIN EMPLOYEE_MST d 
        //        ON p.DOCTOR_ID = d.EMPLOYEE_ID
        //    INNER JOIN EMPLOYEE_MST a 
        //        ON a.EMPLOYEE_ID = p.CREATED_BY
        //    WHERE p.MR_NO = :MR_NO 
        //      AND p.DR_OUT IS NOT NULL ORDER BY p.DR_OUT DESC";

        //        var result = await conn.QueryAsync<PatientDischargeHistory>(query, new { MR_NO });

        //        return result.ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        // Logging (use your logger instead)
        //        Console.WriteLine("Error in GetDischargeHistory: " + ex.Message);
        //        throw;
        //    }
        //}

    }
}