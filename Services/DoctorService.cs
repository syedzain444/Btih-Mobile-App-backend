using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly IDoctorRepository _repo;

        public DoctorService(IDoctorRepository repo)
        {
            _repo = repo;
        }

        //public async Task<List<DoctorInfo>> GetDoctorsAsync()
        //{
        //    // Business rules go here later
        //    return await _repo.GetDoctorsAsync();
        //}
        public async Task<(List<DoctorInfo> doctors, int totalCount)> GetDoctorsAsync(int pageNumber, int pageSize)
        {
            return await _repo.GetDoctorsAsync(pageNumber, pageSize);
        }

        public async Task<List<DoctorSchedule>> GetDoctorScheduleAsync(int doctorId)
        {
            // Business rules go here later
            return await _repo.GetDoctorScheduleAsync(doctorId);
        }

        public async Task<List<SpecializationInfo>> GetDoctorSpecialization()
        {
            // Business rules go here later
            return await _repo.GetDoctorSpecialization();
        }
    }
}
