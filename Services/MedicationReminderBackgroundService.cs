using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Services;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Services
{
    public class MedicationReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ReminderSettings _settings;
        private readonly ILogger<MedicationReminderBackgroundService> _logger;

        public MedicationReminderBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<ReminderSettings> settings,
            ILogger<MedicationReminderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.EnableServerPush)
            {
                _logger.LogInformation("Medication reminder background push is disabled");
                return;
            }

            var interval = TimeSpan.FromSeconds(Math.Max(30, _settings.PollIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    await reminderService.ProcessDueRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Medication reminder background worker failed");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
