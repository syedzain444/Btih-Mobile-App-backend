using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Repository
{
    public interface IDoctorRepository
    {
        //Task<List<DoctorInfo>> GetDoctorsAsync();
        Task<(List<DoctorInfo> doctors, int totalCount)> GetDoctorsAsync(int pageNumber, int pageSize);
        Task<List<DoctorSchedule>> GetDoctorScheduleAsync(int id);
        Task<List<SpecializationInfo>> GetDoctorSpecialization();

    }
}
