using HospitalMobileAPPApi.Models;
using Oracle.ManagedDataAccess.Client;

namespace HospitalMobileAPPApi.Repository
{
    public class MedicationRepository : IMedicationRepository
    {
        private readonly IConfiguration _configuration;

        public MedicationRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<CurrentMedicationItem>> GetCurrentMedicationsAsync(string mrNo)
        {
            var medications = new List<CurrentMedicationItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT *
                FROM (
                    SELECT
                        NVL(p.PP_ID, p.PATIENT_VISIT_ID) AS MEDICATION_ID,
                        p.PP_ID,
                        p.MEDICINE_ID,
                        p.MEDICINE,
                        p.DOSAGE,
                        p.DOSE_WHEN,
                        p.DAYS,
                        p.PERDAY,
                        p.FREQ_DEFINITION,
                        p.ROUTE_NAME,
                        p.REMARKS,
                        p.DOCTOR,
                        p.DEPARTMENT,
                        p.PATIENT_VISIT_ID,
                        p.VISIT_DATE,
                        ROW_NUMBER() OVER (
                            PARTITION BY NVL(TO_CHAR(p.MEDICINE_ID), p.MEDICINE)
                            ORDER BY p.VISIT_DATE DESC NULLS LAST, p.PP_ID DESC NULLS LAST
                        ) AS rn
                    FROM PRESCRIPTIONS p
                    WHERE p.MR_NO = :mr_no
                      AND p.MEDICINE IS NOT NULL
                      AND p.VISIT_DATE >= ADD_MONTHS(SYSDATE, -6)
                ) q
                WHERE q.rn = 1
                ORDER BY q.VISIT_DATE DESC NULLS LAST", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                medications.Add(MapMedication(reader));
            }

            return medications;
        }

        public async Task<MedicationDetailResponse?> GetMedicationDetailAsync(string mrNo, int medicationId)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT
                    NVL(p.PP_ID, p.PATIENT_VISIT_ID) AS MEDICATION_ID,
                    p.PP_ID,
                    p.MEDICINE_ID,
                    p.MEDICINE,
                    p.DOSAGE,
                    p.DOSE_WHEN,
                    p.DAYS,
                    p.PERDAY,
                    p.FREQ_DEFINITION,
                    p.ROUTE_NAME,
                    p.REMARKS,
                    p.DOCTOR,
                    p.DEPARTMENT,
                    p.PATIENT_VISIT_ID,
                    p.VISIT_DATE,
                    p.QTY,
                    p.PHARMACY_NAME
                FROM PRESCRIPTIONS p
                WHERE p.MR_NO = :mr_no
                  AND (p.PP_ID = :medication_id OR p.PATIENT_VISIT_ID = :medication_id)
                  AND ROWNUM = 1", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));
            cmd.Parameters.Add(new OracleParameter("medication_id", medicationId));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            var item = MapMedication(reader);
            return new MedicationDetailResponse
            {
                MedicationId = item.MedicationId,
                PpId = item.PpId,
                MedicineId = item.MedicineId,
                MedicineName = item.MedicineName,
                Dosage = item.Dosage,
                DoseWhen = item.DoseWhen,
                Days = item.Days,
                PerDay = item.PerDay,
                Frequency = item.Frequency,
                Route = item.Route,
                Remarks = item.Remarks,
                Doctor = item.Doctor,
                Department = item.Department,
                PatientVisitId = item.PatientVisitId,
                VisitDate = item.VisitDate,
                Quantity = reader["QTY"] != DBNull.Value ? Convert.ToDecimal(reader["QTY"]) : null,
                PharmacyName = reader["PHARMACY_NAME"]?.ToString(),
            };
        }

        public async Task<int> CreateRefillRequestAsync(RefillRequestPayload request, CurrentMedicationItem medication)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                INSERT INTO PATIENT_MED_REFILL_REQUEST (
                    MR_NO, MEDICATION_ID, MEDICATION_NAME, PP_ID, PATIENT_VISIT_ID, QUANTITY, NOTES
                ) VALUES (
                    :mr_no, :medication_id, :medication_name, :pp_id, :patient_visit_id, :quantity, :notes
                )
                RETURNING REFILL_ID INTO :refill_id", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", request.MrNo));
            cmd.Parameters.Add(new OracleParameter("medication_id", medication.MedicationId));
            cmd.Parameters.Add(new OracleParameter("medication_name", medication.MedicineName));
            cmd.Parameters.Add(new OracleParameter("pp_id", medication.PpId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("patient_visit_id", medication.PatientVisitId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("quantity", request.Quantity ?? (object)DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("notes", request.Notes ?? (object)DBNull.Value));

            var refillOut = new OracleParameter("refill_id", OracleDbType.Int32)
            {
                Direction = System.Data.ParameterDirection.Output,
            };
            cmd.Parameters.Add(refillOut);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(refillOut.Value.ToString());
        }

        public async Task<RefillRequestItem?> GetRefillRequestAsync(int refillId, string mrNo)
        {
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT REFILL_ID, MR_NO, MEDICATION_ID, MEDICATION_NAME, PP_ID, PATIENT_VISIT_ID,
                       QUANTITY, NOTES, STATUS, STATUS_MESSAGE, CREATED_AT, UPDATED_AT
                FROM PATIENT_MED_REFILL_REQUEST
                WHERE REFILL_ID = :refill_id
                  AND MR_NO = :mr_no", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("refill_id", refillId));
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            return MapRefill(reader);
        }

        public async Task<List<RefillRequestItem>> GetRefillRequestsAsync(string mrNo)
        {
            var items = new List<RefillRequestItem>();
            var connStr = _configuration.GetConnectionString("HMISConnection");

            await using var conn = new OracleConnection(connStr);
            await using var cmd = new OracleCommand(@"
                SELECT REFILL_ID, MR_NO, MEDICATION_ID, MEDICATION_NAME, PP_ID, PATIENT_VISIT_ID,
                       QUANTITY, NOTES, STATUS, STATUS_MESSAGE, CREATED_AT, UPDATED_AT
                FROM PATIENT_MED_REFILL_REQUEST
                WHERE MR_NO = :mr_no
                ORDER BY CREATED_AT DESC", conn);

            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter("mr_no", mrNo));

            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                items.Add(MapRefill(reader));
            }

            return items;
        }

        private static CurrentMedicationItem MapMedication(OracleDataReader reader)
        {
            return new CurrentMedicationItem
            {
                MedicationId = Convert.ToInt32(reader["MEDICATION_ID"]),
                PpId = reader["PP_ID"] != DBNull.Value ? Convert.ToInt32(reader["PP_ID"]) : null,
                MedicineId = reader["MEDICINE_ID"] != DBNull.Value ? Convert.ToInt32(reader["MEDICINE_ID"]) : null,
                MedicineName = reader["MEDICINE"]?.ToString() ?? string.Empty,
                Dosage = reader["DOSAGE"]?.ToString(),
                DoseWhen = reader["DOSE_WHEN"]?.ToString(),
                Days = reader["DAYS"] != DBNull.Value ? Convert.ToDecimal(reader["DAYS"]) : null,
                PerDay = reader["PERDAY"] != DBNull.Value ? Convert.ToDecimal(reader["PERDAY"]) : null,
                Frequency = reader["FREQ_DEFINITION"]?.ToString(),
                Route = reader["ROUTE_NAME"]?.ToString(),
                Remarks = reader["REMARKS"]?.ToString(),
                Doctor = reader["DOCTOR"]?.ToString(),
                Department = reader["DEPARTMENT"]?.ToString(),
                PatientVisitId = reader["PATIENT_VISIT_ID"] != DBNull.Value
                    ? Convert.ToInt32(reader["PATIENT_VISIT_ID"])
                    : null,
                VisitDate = reader["VISIT_DATE"] != DBNull.Value
                    ? Convert.ToDateTime(reader["VISIT_DATE"])
                    : null,
            };
        }

        private static RefillRequestItem MapRefill(OracleDataReader reader)
        {
            return new RefillRequestItem
            {
                RefillId = Convert.ToInt32(reader["REFILL_ID"]),
                MrNo = reader["MR_NO"]?.ToString() ?? string.Empty,
                MedicationId = reader["MEDICATION_ID"] != DBNull.Value
                    ? Convert.ToInt32(reader["MEDICATION_ID"])
                    : null,
                MedicationName = reader["MEDICATION_NAME"]?.ToString(),
                PpId = reader["PP_ID"] != DBNull.Value ? Convert.ToInt32(reader["PP_ID"]) : null,
                PatientVisitId = reader["PATIENT_VISIT_ID"] != DBNull.Value
                    ? Convert.ToInt32(reader["PATIENT_VISIT_ID"])
                    : null,
                Quantity = reader["QUANTITY"] != DBNull.Value ? Convert.ToInt32(reader["QUANTITY"]) : null,
                Notes = reader["NOTES"]?.ToString(),
                Status = reader["STATUS"]?.ToString() ?? "PENDING",
                StatusMessage = reader["STATUS_MESSAGE"]?.ToString(),
                CreatedAt = Convert.ToDateTime(reader["CREATED_AT"]),
                UpdatedAt = Convert.ToDateTime(reader["UPDATED_AT"]),
            };
        }
    }
}
