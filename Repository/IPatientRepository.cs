using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IPatientRepository
    {
        Task<PatientProfileResponse?> GetPatientProfileAsync(string MR_NO, int visitPageNumber, int visitPageSize);
        Task<int> GetVisitHistoryCountAsync(string MR_NO);
        Task<List<PatientProfile>> GetPatientAsync(string MR_NO);
        Task<List<LabReportModel>> GetPatientReports(string MR_NO);
        Task<List<LabReportModel>> GetGastroReports(string MR_NO);
        Task<List<LabReportModel>> GetRadiology(string MR_NO);
        Task<List<PrescriptionModel>> GetPrescriptions(string MR_NO);
        Task<int> InsertAppointment(AppointmentModel model);
        Task<int> UpdatePatientPassword(string mrno, string patientPassword);
        Task<bool> UpdatePatientProfileAsync(UpdatePatientProfileRequest request);
        Task<List<PatientAppointment>> GetAppointments(string MR_NO);
        Task<List<PatientAppointment>> GetAppointmentsByPhoneAsync(string phoneNumber);
        Task<bool> CancelGuestAppointmentAsync(string appointmentId, string phoneNumber, string reason);
        Task<int> CancelAppointmentAsync(string appointmentId, string mrNo, string reason);
        Task<int> RequestRescheduleAsync(string appointmentId, RescheduleAppointmentRequest request);
        Task<List<PatientDischargeHistory>> GetDischargeHistory(string MR_NO, int skip, int take);
        Task<int> GetDischargeHistoryCountAsync(string MR_NO);
    }
}
