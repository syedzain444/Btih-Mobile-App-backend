using System.Globalization;
using System.Text.RegularExpressions;
using HospitalMobileAPPApi.Configuration;
using HospitalMobileAPPApi.Models;
using HospitalMobileAPPApi.Repository;
using Microsoft.Extensions.Options;

namespace HospitalMobileAPPApi.Services
{
    public interface IAppointmentPrepService
    {
        Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
        Task QueueCycleAsync(CancellationToken cancellationToken = default);
        Task<int?> ScheduleForAppointmentAsync(string appointmentId);
        Task<int?> ScheduleFromBookingAsync(AppointmentModel model);
        Task CancelForAppointmentAsync(string appointmentId);
        Task<PushSendResult> SendImmediateAsync(SendFastingReminderRequest request);
        string? ResolvePrepKind(string? specializationName, string? purpose, string? doctorName = null);
    }

    public class AppointmentPrepService : IAppointmentPrepService
    {
        private readonly IAppointmentPrepRepository _repository;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly AppointmentPrepSettings _settings;
        private readonly ILogger<AppointmentPrepService> _logger;

        private static readonly Regex SlotPattern = new(
            @"^\s*(?<day>Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\s*[:\-]?\s*(?<from>\d{1,2}:\d{2}\s*(?:AM|PM))\s*(?:-|–|to)\s*(?<to>\d{1,2}:\d{2}\s*(?:AM|PM))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TimeOnlyPattern = new(
            @"(?<from>\d{1,2}:\d{2}\s*(?:AM|PM))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public AppointmentPrepService(
            IAppointmentPrepRepository repository,
            IPushNotificationService pushNotificationService,
            IOptions<AppointmentPrepSettings> settings,
            ILogger<AppointmentPrepService> logger)
        {
            _repository = repository;
            _pushNotificationService = pushNotificationService;
            _settings = settings.Value;
            _logger = logger;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) =>
            _repository.EnsureSchemaAsync(cancellationToken);

        public async Task QueueCycleAsync(CancellationToken cancellationToken = default)
        {
            await DiscoverAndScheduleAsync(cancellationToken);
            await ProcessDueAlertsAsync(cancellationToken);
        }

        public async Task<int?> ScheduleForAppointmentAsync(string appointmentId)
        {
            var ctx = await _repository.GetAppointmentContextAsync(appointmentId);
            if (ctx == null || string.IsNullOrWhiteSpace(ctx.MrNo))
            {
                return null;
            }

            return await ScheduleInternalAsync(ctx);
        }

        public async Task<int?> ScheduleFromBookingAsync(AppointmentModel model)
        {
            if (string.IsNullOrWhiteSpace(model.mrno))
            {
                return null;
            }

            var specialization = await _repository.GetSpecializationNameAsync(model.doctorId);
            var prepKind = ResolvePrepKind(specialization, model.purpose);
            if (prepKind == null)
            {
                return null;
            }

            // Prefer the freshly inserted appointment row when available.
            var latest = await _repository.GetLatestAppointmentForMrNoAsync(
                model.mrno,
                model.doctorId,
                model.departmentId);

            if (latest != null)
            {
                latest.SpecializationName ??= specialization;
                latest.Purpose ??= model.purpose;
                latest.AppointmentTimeLabel ??= model.appointment_time;
                return await ScheduleInternalAsync(latest);
            }

            // Fallback synthetic schedule before DB id is visible.
            var bookedAt = model.createdAt ?? model.entryDate ?? DateTime.Now;
            if (!TryResolveAppointmentAt(model.appointment_time, bookedAt, out var appointmentAt))
            {
                _logger.LogWarning(
                    "Could not resolve appointment datetime from '{Label}' for MR {MrNo}",
                    model.appointment_time,
                    model.mrno);
                return null;
            }

            var rule = GetRule(prepKind);
            var sendAt = appointmentAt.AddHours(-Math.Max(1, rule.HoursBefore));
            if (sendAt < DateTime.Now.AddMinutes(-1))
            {
                sendAt = DateTime.Now;
            }

            var alert = BuildAlert(
                appointmentId: $"PENDING-{model.mrno}-{model.doctorId}-{appointmentAt:yyyyMMddHHmm}",
                mrNo: model.mrno!,
                prepKind: prepKind,
                departmentHint: specialization ?? prepKind,
                doctorName: null,
                appointmentAt: appointmentAt,
                sendAt: sendAt,
                rule: rule,
                timeLabel: model.appointment_time);

            return await _repository.UpsertPendingAlertAsync(alert);
        }

        public Task CancelForAppointmentAsync(string appointmentId) =>
            _repository.CancelByAppointmentIdAsync(appointmentId);

        public async Task<PushSendResult> SendImmediateAsync(SendFastingReminderRequest request)
        {
            var kind = NormalizePrepKind(request.PrepKind) ?? AppointmentPrepKinds.Radiology;
            var rule = GetRule(kind);
            var title = string.IsNullOrWhiteSpace(request.Title) ? rule.Title : request.Title.Trim();
            var body = string.IsNullOrWhiteSpace(request.Body) ? rule.Instructions : request.Body.Trim();

            return await _pushNotificationService.SendToPatientAsync(
                request.MrNo.Trim(),
                title,
                body,
                PushNotificationTypes.AppointmentFastingReminder,
                new Dictionary<string, string>
                {
                    ["screen"] = "appointments",
                    ["prepKind"] = kind,
                    ["category"] = NotificationCategories.Appointments,
                    ["priority"] = "high",
                    ["appointmentId"] = request.AppointmentId ?? string.Empty,
                });
        }

        public string? ResolvePrepKind(string? specializationName, string? purpose, string? doctorName = null)
        {
            var haystack = string.Join(' ',
                specializationName ?? string.Empty,
                purpose ?? string.Empty,
                doctorName ?? string.Empty).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(haystack))
            {
                return null;
            }

            if (Matches(haystack, _settings.Gastro.MatchKeywords))
            {
                return AppointmentPrepKinds.Gastro;
            }

            if (Matches(haystack, _settings.Radiology.MatchKeywords))
            {
                return AppointmentPrepKinds.Radiology;
            }

            return null;
        }

        private async Task DiscoverAndScheduleAsync(CancellationToken cancellationToken)
        {
            List<PrepEligibleAppointment> candidates;
            try
            {
                candidates = await _repository.GetEligibleAppointmentsAsync(_settings.LookaheadDays);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load appointments for prep discovery");
                return;
            }

            foreach (var appt in candidates)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    await ScheduleInternalAsync(appt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to schedule prep alert for appointment {AppointmentId}",
                        appt.AppointmentId);
                }
            }
        }

        private async Task ProcessDueAlertsAsync(CancellationToken cancellationToken)
        {
            List<AppointmentPrepAlert> due;
            try
            {
                due = await _repository.GetDuePendingAlertsAsync(DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load due prep alerts");
                return;
            }

            foreach (var alert in due)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var result = await _pushNotificationService.SendToPatientAsync(
                        alert.MrNo,
                        alert.Title,
                        alert.Body,
                        PushNotificationTypes.AppointmentFastingReminder,
                        new Dictionary<string, string>
                        {
                            ["screen"] = "appointments",
                            ["prepKind"] = alert.PrepKind,
                            ["appointmentId"] = alert.AppointmentId,
                            ["appointmentAt"] = alert.AppointmentAt.ToString("o"),
                            ["category"] = NotificationCategories.Appointments,
                            ["priority"] = "high",
                        });

                    if (result.Sent > 0 || result.Persisted)
                    {
                        await _repository.MarkSentAsync(alert.AlertId);
                        _logger.LogInformation(
                            "Sent {PrepKind} fasting reminder alert #{AlertId} to {MrNo} for appointment {AppointmentId}",
                            alert.PrepKind,
                            alert.AlertId,
                            alert.MrNo,
                            alert.AppointmentId);
                    }
                    else
                    {
                        var err = result.Errors.Count > 0
                            ? string.Join("; ", result.Errors.Take(3))
                            : "No device tokens and persist failed";
                        await _repository.MarkFailedAsync(alert.AlertId, err);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed sending prep alert {AlertId}", alert.AlertId);
                    try
                    {
                        await _repository.MarkFailedAsync(alert.AlertId, ex.Message);
                    }
                    catch
                    {
                        // ignore secondary failure
                    }
                }
            }
        }

        private async Task<int?> ScheduleInternalAsync(PrepEligibleAppointment appt)
        {
            if (string.IsNullOrWhiteSpace(appt.MrNo) || string.IsNullOrWhiteSpace(appt.AppointmentId))
            {
                return null;
            }

            var status = (appt.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (status is "CANCELLED" or "CANCELED" or "REJECTED")
            {
                await _repository.CancelByAppointmentIdAsync(appt.AppointmentId);
                return null;
            }

            var prepKind = ResolvePrepKind(appt.SpecializationName, appt.Purpose, appt.DoctorName);
            if (prepKind == null)
            {
                return null;
            }

            var bookedAt = appt.EntryDate ?? appt.CreatedAt ?? DateTime.Now;
            if (!TryResolveAppointmentAt(appt.AppointmentTimeLabel, bookedAt, out var appointmentAt))
            {
                _logger.LogDebug(
                    "Skipping prep schedule for {AppointmentId}: unparseable time '{Label}'",
                    appt.AppointmentId,
                    appt.AppointmentTimeLabel);
                return null;
            }

            // Ignore appointments that already passed.
            if (appointmentAt < DateTime.Now.AddHours(-2))
            {
                return null;
            }

            var rule = GetRule(prepKind);
            var sendAt = appointmentAt.AddHours(-Math.Max(1, rule.HoursBefore));
            if (sendAt < DateTime.Now.AddMinutes(-1))
            {
                sendAt = DateTime.Now;
            }

            var alert = BuildAlert(
                appointmentId: appt.AppointmentId,
                mrNo: appt.MrNo!,
                prepKind: prepKind,
                departmentHint: appt.SpecializationName ?? prepKind,
                doctorName: appt.DoctorName,
                appointmentAt: appointmentAt,
                sendAt: sendAt,
                rule: rule,
                timeLabel: appt.AppointmentTimeLabel);

            var id = await _repository.UpsertPendingAlertAsync(alert);
            return id > 0 ? id : null;
        }

        private AppointmentPrepAlert BuildAlert(
            string appointmentId,
            string mrNo,
            string prepKind,
            string? departmentHint,
            string? doctorName,
            DateTime appointmentAt,
            DateTime sendAt,
            PrepRuleSettings rule,
            string? timeLabel)
        {
            var whenText = appointmentAt.ToString("ddd, dd MMM yyyy h:mm tt", CultureInfo.InvariantCulture);
            var doctorPart = string.IsNullOrWhiteSpace(doctorName) ? string.Empty : $" with {doctorName.Trim()}";
            var slotPart = string.IsNullOrWhiteSpace(timeLabel) ? whenText : $"{whenText} ({timeLabel.Trim()})";

            var body =
                $"{rule.Instructions} Your appointment is scheduled for {slotPart}{doctorPart}.";

            return new AppointmentPrepAlert
            {
                AppointmentId = appointmentId,
                MrNo = mrNo.Trim(),
                PrepKind = prepKind,
                DepartmentHint = departmentHint,
                DoctorName = doctorName,
                AppointmentAt = appointmentAt,
                SendAt = sendAt,
                Title = rule.Title,
                Body = body,
                Status = AppointmentPrepAlertStatuses.Pending,
            };
        }

        private PrepRuleSettings GetRule(string prepKind) =>
            string.Equals(prepKind, AppointmentPrepKinds.Gastro, StringComparison.OrdinalIgnoreCase)
                ? _settings.Gastro
                : _settings.Radiology;

        private static string? NormalizePrepKind(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var value = raw.Trim().ToUpperInvariant();
            if (value.Contains("GASTRO") || value.Contains("ENDOSCOP"))
            {
                return AppointmentPrepKinds.Gastro;
            }

            if (value.Contains("RAD") || value.Contains("IMAGING"))
            {
                return AppointmentPrepKinds.Radiology;
            }

            return value is AppointmentPrepKinds.Gastro or AppointmentPrepKinds.Radiology ? value : null;
        }

        private static bool Matches(string haystack, IEnumerable<string> keywords)
        {
            foreach (var keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword)) continue;
                if (haystack.Contains(keyword.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves "Friday: 10:00 AM - 12:00 PM" (or similar) into the next concrete local datetime
        /// on/after the booking date.
        /// </summary>
        internal static bool TryResolveAppointmentAt(
            string? appointmentTimeLabel,
            DateTime bookedAt,
            out DateTime appointmentAt)
        {
            appointmentAt = default;
            if (string.IsNullOrWhiteSpace(appointmentTimeLabel))
            {
                return false;
            }

            var label = appointmentTimeLabel.Trim();
            DayOfWeek? day = null;
            TimeSpan? start = null;

            var slotMatch = SlotPattern.Match(label);
            if (slotMatch.Success)
            {
                if (Enum.TryParse<DayOfWeek>(slotMatch.Groups["day"].Value, true, out var parsedDay))
                {
                    day = parsedDay;
                }

                if (TryParseTime(slotMatch.Groups["from"].Value, out var fromTs))
                {
                    start = fromTs;
                }
            }
            else
            {
                foreach (DayOfWeek dow in Enum.GetValues(typeof(DayOfWeek)))
                {
                    if (label.Contains(dow.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        day = dow;
                        break;
                    }
                }

                var timeMatch = TimeOnlyPattern.Match(label);
                if (timeMatch.Success && TryParseTime(timeMatch.Groups["from"].Value, out var fromTs))
                {
                    start = fromTs;
                }
            }

            if (day == null || start == null)
            {
                // Absolute datetime fallback
                if (DateTime.TryParse(label, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var absolute)
                    || DateTime.TryParse(label, out absolute))
                {
                    appointmentAt = absolute;
                    return true;
                }

                return false;
            }

            var date = bookedAt.Date;
            var guard = 0;
            while (date.DayOfWeek != day.Value && guard < 8)
            {
                date = date.AddDays(1);
                guard++;
            }

            appointmentAt = date.Add(start.Value);

            // If the resolved slot already passed relative to booking moment, roll to next week.
            if (appointmentAt < bookedAt.AddMinutes(-30))
            {
                appointmentAt = appointmentAt.AddDays(7);
            }

            return true;
        }

        private static bool TryParseTime(string raw, out TimeSpan time)
        {
            time = default;
            var formats = new[] { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" };
            if (DateTime.TryParseExact(
                    raw.Trim(),
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                time = dt.TimeOfDay;
                return true;
            }

            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
                   && Assign(out time, dt.TimeOfDay);

            static bool Assign(out TimeSpan target, TimeSpan value)
            {
                target = value;
                return true;
            }
        }
    }
}
