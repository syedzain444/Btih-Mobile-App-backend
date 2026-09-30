using HospitalMobileAPPApi.Helpers;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public interface IPatientProfilePhotoService
    {
        Task<string?> GetImagePathAsync(string mrNo);
        Task<string> SavePhotoAsync(string mrNo, IFormFile file);
        Task<bool> RemovePhotoAsync(string mrNo);
    }

    public class PatientProfilePhotoService : IPatientProfilePhotoService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp",
        };

        private const long MaxBytes = 5 * 1024 * 1024;

        private readonly IPatientProfilePhotoRepository _repository;
        private readonly IMobilePortalSchemaService _schemaService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PatientProfilePhotoService> _logger;

        public PatientProfilePhotoService(
            IPatientProfilePhotoRepository repository,
            IMobilePortalSchemaService schemaService,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<PatientProfilePhotoService> logger)
        {
            _repository = repository;
            _schemaService = schemaService;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        public Task<string?> GetImagePathAsync(string mrNo)
        {
            return _repository.GetImagePathAsync(mrNo);
        }

        public async Task<string> SavePhotoAsync(string mrNo, IFormFile file)
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
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = file.ContentType?.ToLowerInvariant() switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg",
                };
            }

            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Only JPG, PNG, or WEBP photos are allowed");
            }

            await _schemaService.EnsureProfilePhotoSchemaAsync();

            var storageRoot = ProfilePhotoStorage.EnsureRoot(_configuration, _environment);
            var previousPath = await _repository.GetImagePathAsync(mrNo);

            var safeMr = mrNo.Replace('/', '_').Replace('\\', '_').Trim();
            if (string.IsNullOrWhiteSpace(safeMr))
            {
                throw new InvalidOperationException("Invalid MR number");
            }

            var folder = Path.Combine(storageRoot, safeMr);
            Directory.CreateDirectory(folder);

            var storedName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(folder, storedName);

            try
            {
                await using (var stream = new FileStream(
                    physicalPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await file.CopyToAsync(stream);
                    await stream.FlushAsync();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied writing profile photo to {Path}", physicalPath);
                throw new InvalidOperationException(
                    "Server cannot write profile photos. Ask IT to grant modify rights on the profile photo folder.",
                    ex);
            }

            if (!File.Exists(physicalPath) || new FileInfo(physicalPath).Length <= 0)
            {
                throw new InvalidOperationException("Photo file could not be written to disk");
            }

            var relativePath = $"{ProfilePhotoStorage.RelativePrefix}/{safeMr}/{storedName}"
                .Replace('\\', '/');

            try
            {
                await _repository.UpsertAsync(mrNo, relativePath, file.ContentType, file.Length);
            }
            catch (Exception ex)
            {
                TryDeletePhysical(physicalPath);
                _logger.LogError(ex, "Failed to persist profile photo metadata for {MrNo}", mrNo);
                throw new InvalidOperationException(
                    "Could not save profile photo metadata. Ensure PATIENT_PROFILE_PHOTO is installed.",
                    ex);
            }

            TryDeleteRelativeFile(previousPath);
            return $"/{relativePath}";
        }

        public async Task<bool> RemovePhotoAsync(string mrNo)
        {
            await _schemaService.EnsureProfilePhotoSchemaAsync();
            var previousPath = await _repository.GetImagePathAsync(mrNo);
            var cleared = await _repository.ClearAsync(mrNo);
            if (cleared)
            {
                TryDeleteRelativeFile(previousPath);
            }

            return cleared;
        }

        private static void TryDeletePhysical(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // ignore cleanup failures
            }
        }

        private void TryDeleteRelativeFile(string? relativePath)
        {
            var storageRoot = ProfilePhotoStorage.GetRoot(_configuration, _environment);
            var fullPath = ProfilePhotoStorage.ResolvePhysicalPath(
                relativePath,
                storageRoot,
                _environment);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return;
            }

            try
            {
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
