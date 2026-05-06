using BocconiLMS.Data;

namespace BocconiLMS.Services;

public class LessonReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LessonReminderHostedService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromHours(24);

    public LessonReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<LessonReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lesson reminder service started.");

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SendRemindersAsync(stoppingToken);
            await Task.Delay(_period, stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running daily lesson reminder check.");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settings    = scope.ServiceProvider.GetRequiredService<SettingsRepository>();

            var emailEnabled   = (await settings.GetAsync("Email:Enabled"))                  == "true";
            var coursesNotifOn = (await settings.GetAsync("Notifications:CoursesEnabled"))   == "true";

            if (!emailEnabled)
            {
                _logger.LogDebug("Lesson reminders skipped: email sending is disabled.");
                return;
            }
            if (!coursesNotifOn)
            {
                _logger.LogDebug("Lesson reminders skipped: course notifications are disabled.");
                return;
            }

            var enrollments  = scope.ServiceProvider.GetRequiredService<EnrollmentRepository>();
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

            var reminders = await enrollments.GetIncompleteEnrollmentsForReminderAsync();
            _logger.LogInformation("Found {Count} enrollments with incomplete lessons.", reminders.Count);

            foreach (var r in reminders)
            {
                if (ct.IsCancellationRequested) break;
                await emailService.SendLessonReminderAsync(r.UserEmail, r.UserFirstName, r.CourseTitle, r.IncompleteLessons);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lesson reminder sending.");
        }
    }
}
