using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class GuestService : IGuestService
    {
        private readonly IGuestRepository _guestRepository;
        private readonly IPatientRepository _patientRepository;

        public GuestService(
            IGuestRepository guestRepository,
            IPatientRepository patientRepository)
        {
            _guestRepository = guestRepository;
            _patientRepository = patientRepository;
        }

        public Task<GuestProfileRecord?> GetProfileByMobileAsync(string mobileNumber)
        {
            return _guestRepository.GetByMobileAsync(mobileNumber);
        }

        public Task<GuestProfileRecord> SaveProfileAsync(GuestProfileRequest request)
        {
            return _guestRepository.UpsertProfileAsync(request);
        }

        public Task<List<PatientAppointment>> GetAppointmentsByPhoneAsync(string phoneNumber)
        {
            return _patientRepository.GetAppointmentsByPhoneAsync(phoneNumber);
        }

        public Task<bool> CancelAppointmentAsync(
            string appointmentId,
            string phoneNumber,
            string reason)
        {
            return _patientRepository.CancelGuestAppointmentAsync(
                appointmentId,
                phoneNumber,
                reason);
        }
    }
}
