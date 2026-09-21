using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public interface IAnalyticsService
    {
        Task<UserAnalyticsDto> GetUserStatisticsAsync(DateTime? from, DateTime? to);
        Task<VisitAnalyticsDto> GetVisitAnalyticsAsync(DateTime? from, DateTime? to);
        Task<EngagementAnalyticsDto> GetEngagementMetricsAsync(DateTime? from, DateTime? to);
        Task<AppSessionStartResult> StartSessionAsync(string mrNo, StartAppSessionRequest request);
        Task<bool> EndSessionAsync(string mrNo, EndAppSessionRequest request);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsRepository _repository;

        public AnalyticsService(IAnalyticsRepository repository)
        {
            _repository = repository;
        }

        public Task<UserAnalyticsDto> GetUserStatisticsAsync(DateTime? from, DateTime? to)
        {
            var range = AnalyticsDateRange.Normalize(from, to);
            return _repository.GetUserStatisticsAsync(range.From, range.To);
        }

        public Task<VisitAnalyticsDto> GetVisitAnalyticsAsync(DateTime? from, DateTime? to)
        {
            var range = AnalyticsDateRange.Normalize(from, to);
            return _repository.GetVisitAnalyticsAsync(range.From, range.To);
        }

        public Task<EngagementAnalyticsDto> GetEngagementMetricsAsync(DateTime? from, DateTime? to)
        {
            var range = AnalyticsDateRange.Normalize(from, to);
            return _repository.GetEngagementMetricsAsync(range.From, range.To);
        }

        public Task<AppSessionStartResult> StartSessionAsync(string mrNo, StartAppSessionRequest request) =>
            _repository.StartSessionAsync(mrNo, request);

        public Task<bool> EndSessionAsync(string mrNo, EndAppSessionRequest request) =>
            _repository.EndSessionAsync(mrNo, request);
    }
}
