using HospitalMobileAPPApi.Configuration;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Services
{
    public class SmsService : ISmsService
    {
        private readonly SmsSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SmsService> _logger;

        public SmsService(
            IOptions<SmsSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<SmsService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            var number = NormalizePhone(phoneNumber);
            if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            try
            {
                var apiUrl = _settings.BaseUrl + number + "&_Msg=" + Uri.EscapeDataString(message);
                var client = _httpClientFactory.CreateClient(nameof(SmsService));
                using var response = await client.GetAsync(apiUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "SMS gateway returned {StatusCode} for {PhoneNumber}",
                        (int)response.StatusCode,
                        number);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS sending failed for {PhoneNumber}", number);
                return false;
            }
        }

        internal static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return string.Empty;
            }

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length >= 12)
            {
                digits = "0" + digits[2..];
            }
            else if (digits.Length == 10 && digits[0] == '3')
            {
                digits = "0" + digits;
            }

            return digits;
        }
    }
}
