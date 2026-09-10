using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class PatientService : IPatientService
    {
        private readonly IPatientRepository _repo;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<PatientService> _logger;

        public PatientService(
            IPatientRepository repo,
            IPushNotificationService pushNotificationService,
            ILogger<PatientService> logger)
        {
            _repo = repo;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
        }

        public Task<PatientProfileResponse?> GetPatientProfileAsync(string MR_NO, int visitPageNumber = 1, int visitPageSize = 20)
        {
            return _repo.GetPatientProfileAsync(MR_NO, visitPageNumber, visitPageSize);
        }

        public async Task<List<PatientProfile>> GetPatientAsync(string MR_NO)
        {
            return await _repo.GetPatientAsync(MR_NO);
        }

        public Task<List<LabReportModel>> GetPatientReports(string MR_NO)
        {
            return _repo.GetPatientReports(MR_NO);
        }

        public Task<List<LabReportModel>> GetGastroReports(string MR_NO)
        {
            return _repo.GetGastroReports(MR_NO);
        }

        public Task<List<LabReportModel>> GetRadiology(string MR_NO)
        {
            return _repo.GetRadiology(MR_NO);
        }

        public Task<List<PrescriptionModel>> GetPrescriptions(string MR_NO)
        {
            return _repo.GetPrescriptions(MR_NO);
        }

        public async Task<int> InsertAppointment(AppointmentModel model)
        {
            var rowsAffected = await _repo.InsertAppointment(model);

            if (rowsAffected > 0 && !string.IsNullOrWhiteSpace(model.mrno))
            {
                try
                {
                    await _pushNotificationService.SendAppointmentReminderAsync(new SendPatientNotificationRequest
                    {
                        MrNo = model.mrno,
                        Title = "Appointment Request Received",
                        Body = "Your appointment request has been received. We will update you soon.",
                        AppointmentId = model.weekId.ToString(),
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send appointment push notification for MR No {MrNo}", model.mrno);
                }
            }

            return rowsAffected;
        }

        public async Task<bool> UpdatePatientPassword(string mrno, string patientPassword)
        {
            var result = await _repo.UpdatePatientPassword(mrno, patientPassword);
            return result > 0;
        }

        public async Task<bool> UpdatePatientProfileAsync(UpdatePatientProfileRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MrNo))
            {
                return false;
            }

            var updated = await _repo.UpdatePatientProfileAsync(request);

            if (updated)
            {
                try
                {
                    await _pushNotificationService.SendProfileUpdatedNotificationAsync(
                        request.MrNo,
                        request.FirstName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send profile update push notification for MR No {MrNo}", request.MrNo);
                }
            }

            return updated;
        }

        public Task<List<PatientAppointment>> GetAppointments(string MR_NO)
        {
            return _repo.GetAppointments(MR_NO);
        }

        public async Task<bool> CancelAppointmentAsync(
            string appointmentId,
            CancelAppointmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Reason))
            {
                return false;
            }

            var rows = await _repo.CancelAppointmentAsync(
                appointmentId,
                request.MrNo,
                request.Reason.Trim());

            return rows > 0;
        }

        public async Task<bool> RequestRescheduleAsync(
            string appointmentId,
            RescheduleAppointmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Reason)
                || request.WeekId <= 0
                || string.IsNullOrWhiteSpace(request.AppointmentTime))
            {
                return false;
            }

            var rows = await _repo.RequestRescheduleAsync(appointmentId, request);
            return rows > 0;
        }

        public async Task<PagedResult<PatientDischargeHistory>> GetDischargeHistoryAsync(
            string MR_NO,
            int pageNumber = 1,
            int pageSize = 10)
        {
            var skip = (pageNumber - 1) * pageSize;
            var totalRecords = await _repo.GetDischargeHistoryCountAsync(MR_NO);
            var data = await _repo.GetDischargeHistory(MR_NO, skip, pageSize);

            return new PagedResult<PatientDischargeHistory>
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalRecords / pageSize) : 0,
                Data = data,
            };
        }
    }
}
