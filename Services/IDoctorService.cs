using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public interface IDoctorService
    {
        //Task<List<DoctorInfo>> GetDoctorsAsync();
        Task<List<DoctorSchedule>> GetDoctorScheduleAsync(int id);
        Task<List<SpecializationInfo>> GetDoctorSpecialization();
       // Task<(List<DoctorInfo>, int totalCount)> GetDoctorsAsync(int pageNumber, int pageSize);
        Task<(List<DoctorInfo> doctors, int totalCount)> GetDoctorsAsync(int pageNumber, int pageSize);

    }
}
