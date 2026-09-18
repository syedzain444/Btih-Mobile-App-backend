using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class RecentActivityService : IRecentActivityService
    {
        public const int MaxItemsPerPatient = 3;

        private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
        {
            "doctor",
            "appointment",
            "medicalReport",
            "discharge",
            "visit",
            "bill",
        };

        private readonly IRecentActivityRepository _repository;

        public RecentActivityService(IRecentActivityRepository repository)
        {
            _repository = repository;
        }

        public async Task<PatientRecentActivityRecord> RecordAsync(RecordRecentActivityRequest request)
        {
            var mrNo = request.MrNo.Trim();
            var activityKey = request.ActivityKey.Trim();
            var kind = request.Kind.Trim();
            var title = request.Title.Trim();

            if (string.IsNullOrWhiteSpace(mrNo) ||
                string.IsNullOrWhiteSpace(activityKey) ||
                string.IsNullOrWhiteSpace(kind) ||
                string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("mrNo, activityKey, kind, and title are required.");
            }

            if (!AllowedKinds.Contains(kind))
            {
                throw new ArgumentException(
                    "kind must be one of: doctor, appointment, medicalReport, discharge, visit, bill.");
            }

            if (activityKey.Length > 120)
            {
                activityKey = activityKey[..120];
            }

            if (title.Length > 200)
            {
                title = title[..200];
            }

            var subtitle = (request.Subtitle ?? string.Empty).Trim();
            if (subtitle.Length > 200)
            {
                subtitle = subtitle[..200];
            }

            var record = new PatientRecentActivityRecord
            {
                MrNo = mrNo,
                ActivityKey = activityKey,
                Kind = kind,
                Title = title,
                Subtitle = subtitle,
                PayloadJson = RecentActivityRepository.SerializePayload(request.Payload),
                ViewedAt = request.ViewedAt?.ToLocalTime() ?? DateTime.Now,
            };

            var saved = await _repository.UpsertAsync(record);
            await _repository.TrimToLimitAsync(mrNo, MaxItemsPerPatient);
            return saved;
        }

        public Task<List<PatientRecentActivityRecord>> GetLatestAsync(string mrNo, int limit = 3)
        {
            if (limit < 1)
            {
                limit = MaxItemsPerPatient;
            }

            if (limit > 20)
            {
                limit = 20;
            }

            return _repository.GetLatestAsync(mrNo.Trim(), limit);
        }

        public Task<int> ClearAsync(string mrNo)
        {
            return _repository.ClearAsync(mrNo.Trim());
        }
    }
}
