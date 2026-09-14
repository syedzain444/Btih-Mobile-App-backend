using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public interface IPatientProfilePhotoService
    {
        Task<string?> GetImagePathAsync(string mrNo);
        Task<string> SavePhotoAsync(
            string mrNo,
            IFormFile file,
            string webRootPath);
        Task<bool> RemovePhotoAsync(string mrNo, string webRootPath);
    }

    public class PatientProfilePhotoService : IPatientProfilePhotoService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp",
        };

        private const long MaxBytes = 5 * 1024 * 1024;
        private const string UploadSubPath = "uploads/profiles";

        private readonly IPatientProfilePhotoRepository _repository;
        private readonly ILogger<PatientProfilePhotoService> _logger;

        public PatientProfilePhotoService(
            IPatientProfilePhotoRepository repository,
            ILogger<PatientProfilePhotoService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public Task<string?> GetImagePathAsync(string mrNo)
        {
            return _repository.GetImagePathAsync(mrNo);
        }

        public async Task<string> SavePhotoAsync(
            string mrNo,
            IFormFile file,
            string webRootPath)
        {
            if (file.Length <= 0)
            {
                throw new InvalidOperationException("Photo file is empty");
            }

            if (file.Length > MaxBytes)
            {
                throw new InvalidOperationException("Photo must be 5 MB or smaller");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Only JPG, PNG, or WEBP photos are allowed");
            }

            var previousPath = await _repository.GetImagePathAsync(mrNo);

            var safeMr = mrNo.Replace('/', '_').Replace('\\', '_').Trim();
            var folder = Path.Combine(webRootPath, UploadSubPath, safeMr);
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(folder, storedName);
            await using (var stream = File.Create(physicalPath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = $"{UploadSubPath}/{safeMr}/{storedName}".Replace('\\', '/');
            await _repository.UpsertAsync(mrNo, relativePath, file.ContentType, file.Length);

            TryDeleteRelativeFile(webRootPath, previousPath);
            return $"/{relativePath}";
        }

        public async Task<bool> RemovePhotoAsync(string mrNo, string webRootPath)
        {
            var previousPath = await _repository.GetImagePathAsync(mrNo);
            var cleared = await _repository.ClearAsync(mrNo);
            if (cleared)
            {
                TryDeleteRelativeFile(webRootPath, previousPath);
            }

            return cleared;
        }

        private void TryDeleteRelativeFile(string webRootPath, string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            try
            {
                var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(webRootPath, normalized);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete previous profile photo {Path}", relativePath);
            }
        }
    }
}
