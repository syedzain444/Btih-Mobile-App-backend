using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IPatientService
    {
        Task<PatientProfileResponse?> GetPatientProfileAsync(string MR_NO, int visitPageNumber = 1, int visitPageSize = 20);
        Task<List<PatientProfile>> GetPatientAsync(string MR_NO);
        Task<List<LabReportModel>> GetPatientReports(string MR_NO);
        Task<List<LabReportModel>> GetGastroReports(string MR_NO);
        Task<List<LabReportModel>> GetRadiology(string MR_NO);
        Task<List<PrescriptionModel>> GetPrescriptions(string MR_NO);
        Task<int> InsertAppointment(AppointmentModel model);
        Task<bool> UpdatePatientPassword(string mrno, string patientPassword);
        Task<bool> UpdatePatientProfileAsync(UpdatePatientProfileRequest request);
        Task<List<PatientAppointment>> GetAppointments(string mrno);
        Task<bool> CancelAppointmentAsync(string appointmentId, CancelAppointmentRequest request);
        Task<bool> RequestRescheduleAsync(string appointmentId, RescheduleAppointmentRequest request);
        Task<PagedResult<PatientDischargeHistory>> GetDischargeHistoryAsync(string MR_NO, int pageNumber = 1, int pageSize = 10);
    }
}
