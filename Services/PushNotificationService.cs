using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;

namespace HospitalMobileAPPApi.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IPushNotificationRepository _repository;
        private readonly IFcmPushSender _fcmPushSender;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(
            IPushNotificationRepository repository,
            IFcmPushSender fcmPushSender,
            ILogger<PushNotificationService> logger)
        {
            _repository = repository;
            _fcmPushSender = fcmPushSender;
            _logger = logger;
        }

        public Task RegisterDeviceTokenAsync(RegisterDeviceTokenRequest request)
        {
            return _repository.RegisterDeviceTokenAsync(request);
        }

        public Task UnregisterDeviceTokenAsync(UnregisterDeviceTokenRequest request)
        {
            return _repository.UnregisterDeviceTokenAsync(request);
        }

        public async Task<PushSendResult> SendProfileUpdatedNotificationAsync(string mrNo, string? firstName)
        {
            var displayName = string.IsNullOrWhiteSpace(firstName) ? "Patient" : firstName.Trim();

            return await SendToPatientAsync(
                mrNo,
                "Profile Updated",
                $"Hi {displayName}, your profile has been updated successfully.",
                PushNotificationTypes.ProfileUpdated,
                new Dictionary<string, string>
                {
                    ["screen"] = "profile",
                });
        }

        public async Task<PushSendResult> SendAppointmentReminderAsync(SendPatientNotificationRequest request)
        {
            var data = new Dictionary<string, string>
            {
                ["screen"] = "appointments",
            };

            if (!string.IsNullOrWhiteSpace(request.AppointmentId))
            {
                data["appointmentId"] = request.AppointmentId;
            }

            return await SendToPatientAsync(
                request.MrNo,
                request.Title,
                request.Body,
                PushNotificationTypes.AppointmentReminder,
                data);
        }

        public async Task<PushSendResult> SendReportReadyNotificationAsync(SendPatientNotificationRequest request)
        {
            var data = new Dictionary<string, string>
            {
                ["screen"] = "reports",
            };

            if (!string.IsNullOrWhiteSpace(request.ReportType))
            {
                data["reportType"] = request.ReportType;
            }

            if (!string.IsNullOrWhiteSpace(request.ReportId))
            {
                data["reportId"] = request.ReportId;
            }

            return await SendToPatientAsync(
                request.MrNo,
                request.Title,
                request.Body,
                PushNotificationTypes.ReportReady,
                data);
        }

        public async Task<PushSendResult> SendToPatientAsync(
            string mrNo,
            string title,
            string body,
            string notificationType,
            Dictionary<string, string>? data = null)
        {
            var result = new PushSendResult();
            var tokens = await _repository.GetActiveTokensAsync(mrNo);
            result.TotalTokens = tokens.Count;

            if (tokens.Count == 0)
            {
                _logger.LogInformation("No active device tokens found for MR No {MrNo}", mrNo);
                return result;
            }

            data ??= new Dictionary<string, string>();
            data["type"] = notificationType;
            data["mrNo"] = mrNo;

            foreach (var token in tokens)
            {
                var sendResult = await _fcmPushSender.SendAsync(token.DeviceToken, title, body, data);
                if (sendResult.Success)
                {
                    result.Sent++;
                    continue;
                }

                result.Failed++;
                result.Errors.Add(
                    $"Token ...{GetTokenSuffix(token.DeviceToken)}: {sendResult.ErrorCode} - {sendResult.ErrorMessage}");

                if (ShouldDeactivateToken(sendResult.ErrorCode))
                {
                    await _repository.DeactivateTokenAsync(mrNo, token.DeviceToken);
                }
            }

            return result;
        }

        private static string GetTokenSuffix(string deviceToken)
        {
            return deviceToken.Length > 8 ? deviceToken[^8..] : deviceToken;
        }

        private static bool ShouldDeactivateToken(string? errorCode)
        {
            return errorCode is "Unregistered"
                or "InvalidArgument"
                or "SenderIdMismatch";
        }
    }
}
