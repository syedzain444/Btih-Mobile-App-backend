using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using HospitalMobileAPPApi.Models;

namespace HospitalMobileAPPApi.Services
{
    public class FcmPushSender : IFcmPushSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FcmPushSender> _logger;
        private readonly object _initLock = new();
        private bool _initialized;

        public FcmPushSender(IConfiguration configuration, ILogger<FcmPushSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<FcmSendResult> SendAsync(
            string deviceToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data = null)
        {
            if (!_configuration.GetValue<bool>("Firebase:Enabled"))
            {
                _logger.LogWarning("Firebase push notifications are disabled in configuration.");
                return new FcmSendResult
                {
                    Success = false,
                    ErrorCode = "Disabled",
                    ErrorMessage = "Firebase push notifications are disabled in configuration.",
                };
            }

            if (!EnsureInitialized(out var initError))
            {
                return new FcmSendResult
                {
                    Success = false,
                    ErrorCode = "InitializationFailed",
                    ErrorMessage = initError,
                };
            }

            var message = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body,
                },
                Data = data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default",
                    },
                },
            };

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                return new FcmSendResult { Success = true };
            }
            catch (FirebaseMessagingException ex)
            {
                _logger.LogWarning(ex, "FCM send failed for token ending in {TokenSuffix}. Code: {ErrorCode}",
                    deviceToken.Length > 8 ? deviceToken[^8..] : deviceToken,
                    ex.MessagingErrorCode);

                return new FcmSendResult
                {
                    Success = false,
                    ErrorCode = ex.MessagingErrorCode.ToString(),
                    ErrorMessage = ex.Message,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected FCM send failure.");
                return new FcmSendResult
                {
                    Success = false,
                    ErrorCode = "UnexpectedError",
                    ErrorMessage = ex.Message,
                };
            }
        }

        private bool EnsureInitialized(out string? errorMessage)
        {
            errorMessage = null;

            if (_initialized && FirebaseApp.DefaultInstance != null)
            {
                return true;
            }

            lock (_initLock)
            {
                if (_initialized && FirebaseApp.DefaultInstance != null)
                {
                    return true;
                }

                var credentialsPath = _configuration["Firebase:CredentialsPath"];
                if (string.IsNullOrWhiteSpace(credentialsPath))
                {
                    errorMessage = "Firebase:CredentialsPath is not configured.";
                    _logger.LogError(errorMessage);
                    return false;
                }

                var fullPath = Path.IsPathRooted(credentialsPath)
                    ? credentialsPath
                    : Path.Combine(AppContext.BaseDirectory, credentialsPath);

                if (!File.Exists(fullPath))
                {
                    errorMessage = $"Firebase credentials file not found at {fullPath}";
                    _logger.LogError(errorMessage);
                    return false;
                }

                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(fullPath),
                    });
                }

                _initialized = true;
                return true;
            }
        }
    }
}
