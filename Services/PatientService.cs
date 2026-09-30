using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class PatientService : IPatientService
    {
        private readonly IPatientRepository _repo;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IAppointmentPrepService _appointmentPrepService;
        private readonly IAppointmentConfirmationService _confirmationService;
        private readonly ILogger<PatientService> _logger;

        public PatientService(
            IPatientRepository repo,
            IPushNotificationService pushNotificationService,
            IAppointmentPrepService appointmentPrepService,
            IAppointmentConfirmationService confirmationService,
            ILogger<PatientService> logger)
        {
            _repo = repo;
            _pushNotificationService = pushNotificationService;
            _appointmentPrepService = appointmentPrepService;
            _confirmationService = confirmationService;
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

        public async Task<AppointmentBookingResult> InsertAppointment(AppointmentModel model)
        {
            var result = await _repo.InsertAppointment(model);

            if (result.RowsAffected > 0 && !string.IsNullOrWhiteSpace(result.AppointmentId))
            {
                try
                {
                    result.ConfirmationQr = await _confirmationService.EnsureQrAsync(result.AppointmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to create confirmation QR for appointment {AppointmentId}",
                        result.AppointmentId);
                }
            }

            if (result.RowsAffected > 0 && !string.IsNullOrWhiteSpace(model.mrno))
            {
                try
                {
                    await _pushNotificationService.SendToPatientAsync(
                        model.mrno,
                        "Appointment Request Received",
                        "Your appointment request has been received. We will update you soon.",
                        PushNotificationTypes.AppointmentRequestReceived,
                        new Dictionary<string, string>
                        {
                            ["screen"] = "appointments",
                            ["weekId"] = model.weekId.ToString(),
                            ["appointmentId"] = result.AppointmentId ?? string.Empty,
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send appointment push notification for MR No {MrNo}", model.mrno);
                }

                try
                {
                    int? alertId = null;
                    if (!string.IsNullOrWhiteSpace(result.AppointmentId))
                    {
                        alertId = await _appointmentPrepService.ScheduleForAppointmentAsync(result.AppointmentId);
                    }

                    alertId ??= await _appointmentPrepService.ScheduleFromBookingAsync(model);
                    if (alertId.HasValue)
                    {
                        _logger.LogInformation(
                            "Scheduled fasting prep alert #{AlertId} for MR {MrNo}",
                            alertId.Value,
                            model.mrno);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to schedule fasting prep alert for MR No {MrNo}", model.mrno);
                }
            }

            return result;
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

            if (rows > 0)
            {
                try
                {
                    await _appointmentPrepService.CancelForAppointmentAsync(appointmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cancel prep alerts for appointment {AppointmentId}", appointmentId);
                }
            }

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
            if (rows > 0)
            {
                try
                {
                    await _appointmentPrepService.ScheduleForAppointmentAsync(appointmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reschedule prep alert for appointment {AppointmentId}", appointmentId);
                }
            }

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
