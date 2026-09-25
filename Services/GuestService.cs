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

        public Task<GuestAppointmentRecord> BookAppointmentAsync(GuestBookAppointmentRequest request)
        {
            return _guestRepository.InsertAppointmentAsync(request);
        }

        public async Task<List<PatientAppointment>> GetAppointmentsByPhoneAsync(string phoneNumber)
        {
            // Prefer dedicated guest appointment store; fall back to HMIS APPOINTMENT by phone.
            var guestRows = await _guestRepository.GetAppointmentsByMobileAsync(phoneNumber);
            if (guestRows.Count > 0)
            {
                return guestRows.Select(MapToPatientAppointment).ToList();
            }

            return await _patientRepository.GetAppointmentsByPhoneAsync(phoneNumber);
        }

        public async Task<bool> CancelAppointmentAsync(
            string appointmentId,
            string phoneNumber,
            string reason)
        {
            if (int.TryParse(appointmentId, out var guestApptId))
            {
                // Prefer guest table cancel when id is numeric (GUEST_APPOINTMENT_ID).
                var cancelledGuest = await _guestRepository.CancelAppointmentAsync(
                    guestApptId,
                    phoneNumber,
                    reason);
                if (cancelledGuest)
                {
                    return true;
                }
            }

            // Also try HMIS APPOINTMENT cancel (legacy / dual-write).
            return await _patientRepository.CancelGuestAppointmentAsync(
                appointmentId,
                phoneNumber,
                reason);
        }

        private static PatientAppointment MapToPatientAppointment(GuestAppointmentRecord row)
        {
            return new PatientAppointment
            {
                AppointmentId = row.GuestAppointmentId.ToString(),
                Name = row.FullName,
                PhoneNo = row.MobileNumber,
                MRNo = string.Empty,
                Email = "guest@example.com",
                weekId = row.WeekId ?? 0,
                AppointmentTime = row.AppointmentTime,
                Status = row.Status,
                DoctorName = row.DoctorName ?? string.Empty,
                DoctorId = row.DoctorId,
                DepartmentId = row.DepartmentId ?? 0,
                purpose = row.Purpose ?? "Guest Appointment",
                CreatedAt = row.CreatedAt,
            };
        }
    }
}
