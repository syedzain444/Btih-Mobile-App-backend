using HospitalMobileAPPApi.Configuration;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Services
{
    /// <summary>
    /// Polls for due Radiology/Gastro fasting prep alerts and discovers new eligible appointments.
    /// </summary>
    public class AppointmentPrepReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AppointmentPrepSettings _settings;
        private readonly ILogger<AppointmentPrepReminderBackgroundService> _logger;

        public AppointmentPrepReminderBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<AppointmentPrepSettings> settings,
            ILogger<AppointmentPrepReminderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.EnableServerPush)
            {
                _logger.LogInformation("Appointment prep (fasting) background push is disabled");
                return;
            }

            // Brief startup delay so DB connections / schema warm-up can finish.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var prep = scope.ServiceProvider.GetRequiredService<IAppointmentPrepService>();
                await prep.EnsureSchemaAsync(stoppingToken);
                _logger.LogInformation("Appointment prep alert schema verified");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not ensure appointment prep schema at worker start");
            }

            var interval = TimeSpan.FromSeconds(Math.Max(30, _settings.PollIntervalSeconds));
            _logger.LogInformation(
                "Appointment prep reminder worker started (interval {Seconds}s)",
                interval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var prep = scope.ServiceProvider.GetRequiredService<IAppointmentPrepService>();
                    await prep.QueueCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Appointment prep reminder background worker failed");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
